namespace A320Copilot.Domain;

/// <summary>Returns a sample or fails explicitly when telemetry is unavailable.</summary>
public interface IAircraftStateSource
{
    ValueTask<AircraftState> ReadAsync(CancellationToken cancellationToken = default);
}
