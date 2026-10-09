using A320Copilot.Bridge;
using A320Copilot.Domain;
using A320Copilot.Mcp;
using System.Text.Json;
using Xunit;

namespace A320Copilot.Tests;

public sealed class TelemetryMonitorTests
{
    private static AircraftState Sample(double altitude = 100, double heading = 359.5, bool battery = true, string title = "FlyByWire A320")
        => new(DateTimeOffset.UtcNow, title, altitude, 0, heading, true)
        {
            Systems = new("test", new Dictionary<string, AircraftParameter>
            { ["Battery1Auto"] = new(battery ? 1 : 0, "bool", "L:BAT") }, new Dictionary<string, AircraftParameter>())
        };

    [Fact]
    public void FreshnessUsesMonotonicTimeAndEmitsOncePerTransition()
    {
        var clock = new ManualClock();
        var store = new TelemetryMonitorStore(new(), clock);
        store.BeginConnection(); store.Publish(Sample());
        var status = store.GetStatus();
        Assert.Equal("fresh", status.Validity);
        Assert.NotNull(store.GetFreshSnapshot());
        clock.Utc = clock.Utc.AddHours(-5); // Wall clock correction must not affect sample age.
        clock.Advance(3001);
        Assert.Equal("stale", store.GetStatus().Validity);
        Assert.Equal(3001, store.GetStatus().AgeMilliseconds);
        Assert.Null(store.GetFreshSnapshot());
        Assert.Single(store.GetEvents().Events, e => e.Type == "data_stale");
        store.Publish(Sample());
        Assert.Equal("fresh", store.GetStatus().Validity);
        Assert.Single(store.GetEvents().Events, e => e.Type == "data_fresh");
    }

    [Fact]
    public void DisconnectInvalidatesImmediatelyAndReconnectResetsBaseline()
    {
        var store = new TelemetryMonitorStore(new());
        store.BeginConnection(); store.Publish(Sample());
        store.Disconnected("connection lost");
        Assert.Null(store.GetFreshSnapshot());
        Assert.Equal("disconnected", store.GetStatus().Validity);
        Assert.Equal("connection lost", store.GetStatus().LastError);
        store.BeginConnection(); store.Publish(Sample(battery: false));
        Assert.Equal(2, store.GetStatus().SessionId);
        Assert.Null(store.GetStatus().LastError);
        Assert.DoesNotContain(store.GetEvents().Events, e => e.Type == "parameter_changed");
        Assert.Equal(2, store.GetEvents().Events.Count(e => e.Type == "baseline_created"));
    }

    [Fact]
    public void ChangeDetectionHandlesJitterHeadingWrapAndAccumulatedMovement()
    {
        var store = new TelemetryMonitorStore(new());
        store.BeginConnection(); store.Publish(Sample());
        store.Publish(Sample(106, 0.5));
        Assert.DoesNotContain(store.GetEvents().Events, e => e.Type == "parameter_changed");
        store.Publish(Sample(111, 1.6, false));
        var changes = store.GetEvents().Events.Where(e => e.Type == "parameter_changed").ToArray();
        Assert.Equal(3, changes.Length);
        var battery = Assert.Single(changes, e => e.Field == "Overhead.Battery1Auto");
        Assert.Equal(1d, battery.PreviousValue);
        Assert.Equal(0d, battery.Value);
    }

    [Fact]
    public void AircraftReloadResetsComparisons()
    {
        var store = new TelemetryMonitorStore(new());
        store.BeginConnection(); store.Publish(Sample());
        store.Publish(Sample(battery: false, title: "Another aircraft"));
        Assert.Contains(store.GetEvents().Events, e => e.Type == "aircraft_changed");
        Assert.DoesNotContain(store.GetEvents().Events, e => e.Type == "parameter_changed");
    }

    [Fact]
    public void BoundedJournalReportsGapsAndSupportsPaging()
    {
        var store = new TelemetryMonitorStore(new() { EventCapacity = 10 });
        store.BeginConnection(); store.Publish(Sample());
        for (var i = 0; i < 30; i++) store.Publish(Sample(battery: i % 2 == 0));
        var page = store.GetEvents(0, 2);
        Assert.True(page.HistoryTruncated);
        Assert.True(page.HasMore);
        Assert.Equal(2, page.Events.Count);
        var next = store.GetEvents(page.NextAfterSequence, 200);
        Assert.False(next.HistoryTruncated);
        Assert.DoesNotContain(next.Events, e => e.Sequence <= page.NextAfterSequence);
        Assert.Empty(store.GetEvents(next.NextAfterSequence).Events);
        Assert.Throws<ArgumentException>(() => store.GetEvents(long.MaxValue));
    }

    [Fact]
    public void FuelAndControlChangesAreIncludedInTheEventJournal()
    {
        var store = new TelemetryMonitorStore(new());
        var initial = Sample() with { Systems = Sample().Systems! with
        {
            Fuel = new Dictionary<string, AircraftParameter> { ["Left1Switch"] = new(0, "bool", "FUELSYSTEM PUMP SWITCH:2") },
            Controls = new Dictionary<string, AircraftParameter> { ["ParkingBrakeLever"] = new(1, "bool", "L:A32NX_PARK_BRAKE_LEVER_POS") }
        }};
        store.BeginConnection(); store.Publish(initial);
        store.Publish(initial with { Systems = initial.Systems! with
        {
            Fuel = new Dictionary<string, AircraftParameter> { ["Left1Switch"] = new(1, "bool", "FUELSYSTEM PUMP SWITCH:2") },
            Controls = new Dictionary<string, AircraftParameter> { ["ParkingBrakeLever"] = new(0, "bool", "L:A32NX_PARK_BRAKE_LEVER_POS") }
        }});
        var changes = store.GetEvents().Events;
        Assert.Contains(changes, e => e.Type == "parameter_changed" && e.Field == "Fuel.Left1Switch");
        Assert.Contains(changes, e => e.Type == "parameter_changed" && e.Field == "Controls.ParkingBrakeLever");
    }

    [Fact]
    public void MockToolsNeverStartRealMonitor()
    {
        using var source = new SimConnectAircraftStateSource(new(), () => throw new InvalidOperationException("Must not open"));
        var tools = new MonitorTools(new() { Mode = "Mock" }, source);
        using var status = JsonDocument.Parse(tools.GetMonitorStatus());
        Assert.False(status.RootElement.GetProperty("Running").GetBoolean());
        using var events = JsonDocument.Parse(tools.GetRecentEvents());
        Assert.Empty(events.RootElement.GetProperty("Events").EnumerateArray());
    }

    [Fact]
    public async Task WorkerRecoversAndReusesNewConnection()
    {
        var attempts = 0;
        using var recovered = new RecoverySession();
        using var source = new SimConnectAircraftStateSource(new() { ReconnectDelayMilliseconds = 50 },
            () => Interlocked.Increment(ref attempts) == 1 ? throw new IOException("simulator unavailable") : recovered);
        source.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (source.GetStatus().Validity != "fresh") await Task.Delay(10, timeout.Token);
        var snapshot = await source.ReadSnapshotAsync();
        Assert.Equal(2, snapshot.Freshness.ConnectionAttempts);
        Assert.Equal("fresh", snapshot.Freshness.Validity);
        Assert.Contains(source.GetEvents().Events, e => e.Type == "disconnected");
        Assert.Contains(source.GetEvents().Events, e => e.Type == "connected");
    }

    private sealed class RecoverySession : ISimConnectSession
    {
        public void RequestSample() { }
        public AircraftState? ReadNext() => Sample();
        public void Dispose() { }
    }

    [Fact]
    public async Task StaleStateBecomesMcpErrorWithoutReturningOldState()
    {
        using var source = new SimConnectAircraftStateSource(new() { MaximumSampleAgeMilliseconds = 100 }, () => new OneShotSession());
        var reader = new TelemetryReader(new() { Mode = "Real" }, source);
        using var fresh = JsonDocument.Parse(await reader.ReadAsync());
        Assert.Equal("fresh", fresh.RootElement.GetProperty("Freshness").GetProperty("Validity").GetString());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (source.GetStatus().Validity == "fresh") await Task.Delay(10, deadline.Token);
        var error = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => new AircraftTools(reader).GetAircraftState(default));
        Assert.Contains("stale", error.Message);
        Assert.Contains("No mock data was returned", error.Message);
    }

    [Fact]
    public async Task SilentConnectionIsDisposedAndReconnected()
    {
        var attempts = 0;
        var silent = new OneShotSession();
        using var source = new SimConnectAircraftStateSource(new()
            { TimeoutSeconds = 1, MaximumSampleAgeMilliseconds = 100, ReconnectDelayMilliseconds = 50 },
            () => Interlocked.Increment(ref attempts) == 1 ? silent : new RecoverySession());
        await source.ReadAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (source.GetStatus().SessionId < 2 || source.GetStatus().Validity != "fresh")
            await Task.Delay(10, deadline.Token);
        Assert.True(silent.Disposed);
        var events = source.GetEvents().Events;
        Assert.Contains(events, e => e.Type == "data_stale");
        Assert.Contains(events, e => e.Type == "disconnected");
        Assert.Equal(2, events.Count(e => e.Type == "baseline_created"));
    }

    private sealed class OneShotSession : ISimConnectSession
    {
        private bool sent;
        public bool Disposed { get; private set; }
        public void RequestSample() { }
        public AircraftState? ReadNext() { if (sent) return null; sent = true; return Sample(); }
        public void Dispose() => Disposed = true;
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc { get; set; } = DateTimeOffset.UtcNow;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => Utc;
        public void Advance(int milliseconds) { timestamp += milliseconds; Utc = Utc.AddMilliseconds(milliseconds); }
    }
}
