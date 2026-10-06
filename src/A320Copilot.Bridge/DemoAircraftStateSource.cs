using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>Synthetic data for development; never represents a live aircraft.</summary>
public sealed class DemoAircraftStateSource : IAircraftStateSource
{
    public ValueTask<AircraftState> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new AircraftState(
            DateTimeOffset.UtcNow, "DEMO - FlyByWire A320", 0, 0, 0, true));
    }
}
