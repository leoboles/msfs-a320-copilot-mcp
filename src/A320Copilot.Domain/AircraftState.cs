namespace A320Copilot.Domain;

/// <summary>A single telemetry sample. Units are explicit; time is UTC.</summary>
public sealed record AircraftState(
    DateTimeOffset CapturedAtUtc,
    string AircraftTitle,
    double AltitudeFeet,
    double IndicatedAirspeedKnots,
    double HeadingDegreesTrue,
    bool IsOnGround);
