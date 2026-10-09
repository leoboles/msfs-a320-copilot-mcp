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
        Assert.Equal(68.2, result.RootElement.GetProperty("State").GetProperty("Systems")
            .GetProperty("Engines").GetProperty("Engine1N2").GetProperty("Value").GetDouble());
    }

    [Fact]
    public async Task RealFailureDoesNotFallBack()
    {
        using var source = new SimConnectAircraftStateSource(new(), () => throw new IOException("Unavailable"));
        var reader = new TelemetryReader(new() { Mode = "Real" }, source);
        await Assert.ThrowsAsync<IOException>(async () => await reader.ReadAsync());
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
            => ValueTask.FromResult(new AircraftState(DateTimeOffset.UtcNow, "test", 1234, 100, 90, false)
            {
                Systems = new AircraftSystemsState("test", new Dictionary<string, AircraftParameter>(),
                    new Dictionary<string, AircraftParameter>
                    {
                        ["Engine1N2"] = new(68.2, "percent", "L:A32NX_ENGINE_N2:1")
                    })
            });
    }
}
