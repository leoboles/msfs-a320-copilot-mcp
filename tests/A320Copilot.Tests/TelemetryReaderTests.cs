using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Domain;
using A320Copilot.Mcp;
using Xunit;

namespace A320Copilot.Tests;

public sealed class TelemetryReaderTests
{
    [Theory]
    [InlineData("Mock")]
    [InlineData("mock")]
    public async Task MockDoesNotReadRealSource(string mode)
    {
        var reader = new TelemetryReader(new() { Mode = mode }, new SimConnectAircraftStateSource());
        using var result = JsonDocument.Parse(await reader.ReadAsync());
        Assert.Equal("mock", result.RootElement.GetProperty("Source").GetString());
        Assert.False(result.RootElement.GetProperty("SimulatorConnected").GetBoolean());
    }

    [Fact]
    public async Task RealUsesInjectedSource()
    {
        var reader = new TelemetryReader(new() { Mode = "Real" }, new SampleSource());
        using var result = JsonDocument.Parse(await reader.ReadAsync());
        Assert.Equal("real", result.RootElement.GetProperty("Source").GetString());
        Assert.Equal(1234, result.RootElement.GetProperty("State").GetProperty("AltitudeFeet").GetDouble());
    }

    [Fact]
    public async Task RealFailureDoesNotFallBack()
    {
        var reader = new TelemetryReader(new() { Mode = "Real" }, new SimConnectAircraftStateSource());
        await Assert.ThrowsAsync<NotSupportedException>(async () => await reader.ReadAsync());
        var exception = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(
            () => new AircraftTools(reader).GetAircraftState(default));
        Assert.Contains("No mock data was returned", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Automatic")]
    public void RejectsInvalidMode(string mode)
    {
        Assert.Throws<ArgumentException>(() =>
            new TelemetryReader(new() { Mode = mode }, new SampleSource()));
    }

    private sealed class SampleSource : IAircraftStateSource
    {
        public ValueTask<AircraftState> ReadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new AircraftState(DateTimeOffset.UtcNow, "test", 1234, 100, 90, false));
    }
}
