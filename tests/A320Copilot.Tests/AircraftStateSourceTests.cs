using A320Copilot.Bridge;
using Xunit;

namespace A320Copilot.Tests;

public sealed class AircraftStateSourceTests
{
    [Fact]
    public async Task DemoSampleIsExplicitlySyntheticAndTimestamped()
    {
        var before = DateTimeOffset.UtcNow;
        var state = await new DemoAircraftStateSource().ReadAsync();
        Assert.StartsWith("DEMO", state.AircraftTitle);
        Assert.InRange(state.CapturedAtUtc, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, state.CapturedAtUtc.Offset);
    }

    [Fact]
    public async Task UnimplementedAdapterNeverReturnsFabricatedTelemetry()
    {
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await new SimConnectAircraftStateSource().ReadAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcesHonorCancellation(bool useSimConnect)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        A320Copilot.Domain.IAircraftStateSource source = useSimConnect
            ? new SimConnectAircraftStateSource()
            : new DemoAircraftStateSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await source.ReadAsync(cancellation.Token));
    }
}
