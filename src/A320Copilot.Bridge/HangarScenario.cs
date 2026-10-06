namespace A320Copilot.Bridge;

/// <summary>Fixed fictional scenario, independent of the simulator.</summary>
public static class HangarScenario
{
    public static HangarState Create() => new(
        "mock", false, "cold_and_dark_in_hangar", DateTimeOffset.UtcNow,
        "FlyByWire A320 (fictional)", "Fictional hangar",
        true, 0, 0, 0, false, false, false, false, false, false, false, true, true);
}

public sealed record HangarState(
    string Source, bool SimulatorConnected, string Scenario,
    DateTimeOffset GeneratedAtUtc, string Aircraft, string Location,
    bool IsOnGround, double GroundSpeedKnots, double IndicatedAirspeedKnots,
    double AltitudeAboveGroundFeet, bool Battery1On, bool Battery2On,
    bool ExternalPowerConnected, bool ExternalPowerOn, bool ApuRunning,
    bool Engine1Running, bool Engine2Running, bool ParkingBrakeSet, bool ChocksInstalled);
