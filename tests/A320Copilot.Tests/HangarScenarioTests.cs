using A320Copilot.Bridge;
using Xunit;

namespace A320Copilot.Tests;

public sealed class HangarScenarioTests
{
    [Fact]
    public void HangarIsExplicitlyMockAndPoweredOff()
    {
        var state = HangarScenario.Create();
        Assert.Equal("mock", state.Source);
        Assert.False(state.SimulatorConnected);
        Assert.True(state.IsOnGround);
        Assert.True(state.ParkingBrakeSet);
        Assert.True(state.ChocksInstalled);
        Assert.Equal(0, state.GroundSpeedKnots);
        Assert.Equal(0, state.AltitudeAboveGroundFeet);
        Assert.False(state.Battery1On || state.Battery2On || state.ExternalPowerOn ||
            state.ExternalPowerConnected || state.ApuRunning ||
            state.Engine1Running || state.Engine2Running);
    }
}
