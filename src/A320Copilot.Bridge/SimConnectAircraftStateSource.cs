using System.Diagnostics;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>One background thread owns the persistent session. Client cancellation never closes it.</summary>
public sealed class SimConnectAircraftStateSource : IAircraftStateSource, IDisposable
{
    private readonly object gate = new();
    private readonly SimConnectSettings settings;
    private readonly Func<ISimConnectSession> sessionFactory;
    private readonly TelemetryMonitorStore store;
    private readonly CancellationTokenSource lifetime = new();
    private Task? worker;
    private bool disposed;

    public SimConnectAircraftStateSource() : this(new SimConnectSettings()) { }
    public SimConnectAircraftStateSource(SimConnectSettings settings)
        : this(settings, () => new NativeSimConnectSession(settings)) { }
    public SimConnectAircraftStateSource(SimConnectSettings settings, Func<ISimConnectSession> sessionFactory)
    {
        settings.Validate();
        this.settings = settings;
        this.sessionFactory = sessionFactory;
        store = new(settings);
    }

    public void Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            worker ??= Task.Factory.StartNew(Run, CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    private void Run()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                try
                {
                    store.BeginConnection();
                    using var session = sessionFactory();
                    lifetime.Token.ThrowIfCancellationRequested();
                    session.RequestSample();
                    var lastSample = Stopwatch.GetTimestamp();
                    while (!lifetime.IsCancellationRequested)
                    {
                        var sample = session.ReadNext();
                        if (sample is not null)
                        {
                            store.Publish(sample);
                            lastSample = Stopwatch.GetTimestamp();
                        }
                        store.CheckFreshness();
                        if (Stopwatch.GetElapsedTime(lastSample).TotalSeconds >= settings.TimeoutSeconds)
                            throw new TimeoutException("No SimConnect samples within the receive timeout; reconnecting.");
                        lifetime.Token.WaitHandle.WaitOne(10);
                    }
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    if (lifetime.IsCancellationRequested) break;
                    store.Disconnected(exception.Message);
                }
                if (!lifetime.IsCancellationRequested)
                    lifetime.Token.WaitHandle.WaitOne(settings.ReconnectDelayMilliseconds);
            }
        }
        finally { store.Stopped(); }
    }

    public TelemetryMonitorStatus GetStatus() { Start(); return store.GetStatus(); }
    public TelemetryEventPage GetEvents(long afterSequence = 0, int limit = 100)
    {
        Start();
        return store.GetEvents(afterSequence, limit);
    }

    public async ValueTask<AircraftState> ReadAsync(CancellationToken cancellationToken = default)
        => (await ReadSnapshotAsync(cancellationToken)).State;

    public async ValueTask<RealAircraftTelemetry> ReadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Start();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var snapshot = store.GetFreshSnapshot();
                if (snapshot is not null) return snapshot;
                var status = store.GetStatus();
                if (status.Validity == "stale")
                    throw new IOException("SimConnect data is stale; no current aircraft state was returned. Check get_monitor_status.");
                if (status.ConnectionState == "disconnected")
                    throw new IOException($"SimConnect disconnected: {status.LastError}. Automatic reconnection is active.");
                await Task.Delay(20, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for valid SimConnect telemetry. Check get_monitor_status.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? pending;
        lock (gate)
        {
            lifetime.Cancel();
            pending = worker;
        }
        if (pending is not null) await pending.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        StopAsync().GetAwaiter().GetResult();
        lifetime.Dispose();
    }
}
