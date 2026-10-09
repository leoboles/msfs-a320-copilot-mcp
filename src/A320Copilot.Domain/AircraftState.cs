namespace A320Copilot.Domain;

/// <summary>A single telemetry sample. Units are explicit; time is UTC.</summary>
public sealed record AircraftState(
    DateTimeOffset CapturedAtUtc,
    string AircraftTitle,
    double AltitudeFeet,
    double IndicatedAirspeedKnots,
    double HeadingDegreesTrue,
    bool IsOnGround)
{
    public AircraftSystemsState? Systems { get; init; }
}

public sealed record AircraftParameter(double Value, string Unit, string SimVar);

public sealed record AircraftSystemsState(
    string Validation,
    IReadOnlyDictionary<string, AircraftParameter> Overhead,
    IReadOnlyDictionary<string, AircraftParameter> Engines)
{
    public IReadOnlyDictionary<string, AircraftParameter> Fuel { get; init; } = new Dictionary<string, AircraftParameter>();
    public IReadOnlyDictionary<string, AircraftParameter> Controls { get; init; } = new Dictionary<string, AircraftParameter>();
}
