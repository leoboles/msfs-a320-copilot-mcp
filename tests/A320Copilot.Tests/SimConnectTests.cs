using System.Buffers.Binary;
using System.Text;
using A320Copilot.Bridge;
using A320Copilot.Domain;
using Xunit;

namespace A320Copilot.Tests;

public sealed class SimConnectTests
{
    private static byte[] Sample()
    {
        var bytes = new byte[SimConnectSampleParser.PayloadSize];
        Encoding.UTF8.GetBytes("FlyByWire A320").CopyTo(bytes, 0);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(256), 1234.5);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(264), 152.25);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(272), -10);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(280), -1);
        for (var i = 0; i < FlyByWireParameters.All.Count; i++)
            BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(284 + i * 8), i + 0.25);
        return bytes;
    }

    [Fact]
    public void ParsesPackedSdkPayloadAndNormalizesHeadingAndUtc()
    {
        var time = new DateTimeOffset(2026, 10, 8, 21, 0, 0, TimeSpan.FromHours(-3));
        var state = SimConnectSampleParser.Parse(Sample(), time);
        Assert.Equal("FlyByWire A320", state.AircraftTitle);
        Assert.Equal(1234.5, state.AltitudeFeet);
        Assert.Equal(152.25, state.IndicatedAirspeedKnots);
        Assert.Equal(350, state.HeadingDegreesTrue);
        Assert.True(state.IsOnGround);
        Assert.Equal(TimeSpan.Zero, state.CapturedAtUtc.Offset);
        Assert.Equal(time, state.CapturedAtUtc);
        Assert.NotNull(state.Systems);
        Assert.Equal(0.25, state.Systems.Overhead["Battery1Auto"].Value);
        Assert.Equal("L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO", state.Systems.Overhead["Battery1Auto"].SimVar);
        Assert.Equal(19.25, state.Systems.Engines["Engine1N1"].Value);
        Assert.Equal("percent", state.Systems.Engines["Engine1N1"].Unit);
        Assert.Equal(27.25, state.Systems.Engines["Engine2FuelFlow"].Value);
    }

    [Fact]
    public void FuelAndControlsHaveDistinctGroupsAndDocumentedIndices()
    {
        var state = SimConnectSampleParser.Parse(Sample(), DateTimeOffset.UtcNow);
        Assert.Equal(12, state.Systems!.Fuel.Count);
        Assert.Equal("FUELSYSTEM PUMP SWITCH:2", state.Systems.Fuel["Left1Switch"].SimVar);
        Assert.Equal("FUELSYSTEM VALVE SWITCH:9", state.Systems.Fuel["Center1Switch"].SimVar);
        Assert.Equal("FUELSYSTEM VALVE OPEN:10", state.Systems.Fuel["Center2Open"].SimVar);
        Assert.Equal("L:A32NX_PARK_BRAKE_LEVER_POS", state.Systems.Controls["ParkingBrakeLever"].SimVar);
        Assert.False(state.Systems.Engines.ContainsKey("ParkingBrakeLever"));
    }

    [Fact]
    public void DoesNotLabelOtherAircraftWithFlyByWireSystems()
    {
        var sample = Sample();
        sample.AsSpan(0, 256).Clear();
        Encoding.UTF8.GetBytes("Other aircraft").CopyTo(sample, 0);
        Assert.Null(SimConnectSampleParser.Parse(sample, DateTimeOffset.UtcNow).Systems);
    }

    [Fact]
    public void RejectsInvalidSystemData()
    {
        var sample = Sample();
        BinaryPrimitives.WriteDoubleLittleEndian(sample.AsSpan(284), double.PositiveInfinity);
        Assert.Throws<IOException>(() => SimConnectSampleParser.Parse(sample, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RejectsTruncatedOrNonFiniteSample()
    {
        Assert.Throws<IOException>(() => SimConnectSampleParser.Parse(new byte[283], DateTimeOffset.UtcNow));
        var sample = Sample();
        BinaryPrimitives.WriteDoubleLittleEndian(sample.AsSpan(256), double.NaN);
        Assert.Throws<IOException>(() => SimConnectSampleParser.Parse(sample, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ConcurrentReadsReuseSessionUntilMonitorIsDisposed()
    {
        var session = new Session { Result = SimConnectSampleParser.Parse(Sample(), DateTimeOffset.UtcNow) };
        var opens = 0;
        using var source = new SimConnectAircraftStateSource(new(), () => { Interlocked.Increment(ref opens); return session; });
        var reads = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => source.ReadAsync().AsTask()));
        Assert.All(reads, sample => Assert.Same(session.Result, sample));
        Assert.True(session.Requested);
        Assert.False(session.Disposed);
        Assert.Equal(1, opens);
        Assert.Equal(1, source.GetStatus().SessionId);
        source.Dispose();
        Assert.True(session.Disposed);
        Assert.Single(session.Threads.Distinct());
    }

    [Fact]
    public async Task DeadlineDoesNotReturnInvalidData()
    {
        var session = new Session();
        using var source = new SimConnectAircraftStateSource(new() { TimeoutSeconds = 1 }, () => session);
        await Assert.ThrowsAsync<TimeoutException>(async () => await source.ReadAsync());
        source.Dispose();
        Assert.True(session.Disposed);
    }

    [Fact]
    public async Task ClientCancellationDoesNotStopSharedMonitor()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new Session { OnRead = cancellation.Cancel };
        using var source = new SimConnectAircraftStateSource(new(), () => session);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.ReadAsync(cancellation.Token));
        Assert.False(session.Disposed);
        source.Dispose();
        Assert.True(session.Disposed);
    }

    private sealed class Session : ISimConnectSession
    {
        public AircraftState? Result { get; init; }
        public Action? OnRead { get; init; }
        public bool Requested { get; private set; }
        public bool Disposed { get; private set; }
        public System.Collections.Concurrent.ConcurrentBag<int> Threads { get; } = new();
        public void RequestSample() { Threads.Add(Environment.CurrentManagedThreadId); Requested = true; }
        public AircraftState? ReadNext() { Threads.Add(Environment.CurrentManagedThreadId); OnRead?.Invoke(); return Result; }
        public void Dispose() { Threads.Add(Environment.CurrentManagedThreadId); Disposed = true; }
    }
}
