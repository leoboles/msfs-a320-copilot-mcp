using System.Diagnostics;
using System.Text.Json;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>One receive loop owns a persistent socket, independent of individual MCP requests.</summary>
public sealed class SimBridgeMcduReader : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly SimBridgeSettings settings;
    private readonly Func<IMcduConnection> connectionFactory;
    private readonly McduMonitorStore store;
    private readonly CancellationTokenSource lifetime = new();
    private Task? worker;
    private bool stopped, disposed;

    public SimBridgeMcduReader(SimBridgeSettings settings, Func<IMcduConnection> connectionFactory)
    {
        settings.Validate();
        this.settings = settings;
        this.connectionFactory = connectionFactory;
        store = new(settings);
    }

    public void Start()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed || stopped, this);
            worker ??= Task.Run(RunAsync);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                try
                {
                    store.BeginConnection();
                    await RunSessionAsync();
                }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    if (lifetime.IsCancellationRequested) break;
                    store.Disconnected(exception.Message);
                }
                try { await Task.Delay(settings.ReconnectDelayMilliseconds, lifetime.Token); }
                catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { break; }
            }
        }
        finally { store.Stopped(); }
    }

    private async Task RunSessionAsync()
    {
        var connection = connectionFactory();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        session.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        Task<string>? receive = null;
        try
        {
            await connection.ConnectAsync(new Uri(settings.McduWebSocketUrl), session.Token);
            store.SocketConnected();
            await RequestUpdateAsync();
            var lastRequest = Stopwatch.GetTimestamp();
            while (true)
            {
                session.Token.ThrowIfCancellationRequested();
                receive ??= connection.ReceiveAsync(session.Token);
                var completed = await Task.WhenAny(receive, Task.Delay(50, session.Token));
                session.Token.ThrowIfCancellationRequested();
                if (completed == receive)
                {
                    var message = await receive;
                    receive = null;
                    if (message == "mcduDisconnected")
                        throw new IOException("The aircraft MCDU disconnected from SimBridge.");
                    if (message == "mcduConnected")
                    {
                        await RequestUpdateAsync();
                        lastRequest = Stopwatch.GetTimestamp();
                    }
                    else if (message.StartsWith("update:", StringComparison.Ordinal))
                    {
                        using var document = JsonDocument.Parse(message["update:".Length..]);
                        if (!document.RootElement.TryGetProperty("left", out var left) ||
                            left.ValueKind != JsonValueKind.Object ||
                            !left.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array ||
                            !left.TryGetProperty("title", out var title) || title.ValueKind != JsonValueKind.String)
                            throw new IOException("Invalid SimBridge MCDU update: expected left screen with title and lines.");
                        store.Publish(left);
                        // Echoes/connection notices never extend freshness or the screen receive timeout.
                        session.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                    }
                }
                store.CheckFreshness();
                if (Stopwatch.GetElapsedTime(lastRequest).TotalMilliseconds >= settings.RefreshIntervalMilliseconds)
                {
                    await RequestUpdateAsync();
                    lastRequest = Stopwatch.GetTimestamp();
                }
            }

            async Task RequestUpdateAsync()
            {
                await connection.SendAsync("requestUpdate", session.Token);
                store.UpdateRequested();
            }
        }
        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for a fresh MCDU update; reconnecting to SimBridge.");
        }
        finally
        {
            session.Cancel();
            connection.Dispose();
            if (receive is not null)
            {
                try { await receive; }
                catch (Exception) { /* Observe the receive canceled/closed during session cleanup. */ }
            }
        }
    }

    public McduMonitorStatus GetStatus() { Start(); return store.GetStatus(); }
    public TelemetryEventPage GetEvents(long afterSequence = 0, int limit = 100)
    {
        Start();
        return store.GetEvents(afterSequence, limit);
    }

    public async Task<McduState> ReadAsync(CancellationToken cancellationToken = default)
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
                    throw new IOException("SimBridge MCDU screen is stale; no current screen was returned. Check get_mcdu_status.");
                if (status.ConnectionState == "disconnected")
                    throw new IOException($"SimBridge MCDU disconnected: {status.LastError}. Automatic reconnection is active.");
                await Task.Delay(10, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for a fresh MCDU update. Check get_mcdu_status and the loaded aircraft.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task? pending;
        lock (gate)
        {
            if (!stopped) { stopped = true; lifetime.Cancel(); }
            pending = worker;
        }
        if (pending is not null) await pending.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
        }
        await StopAsync();
        lifetime.Dispose();
    }
}
