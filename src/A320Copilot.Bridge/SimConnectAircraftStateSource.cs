using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>Boundary for a future Windows SimConnect adapter. No SDK is loaded yet.</summary>
public sealed class SimConnectAircraftStateSource : IAircraftStateSource
{
    public ValueTask<AircraftState> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(
            "SimConnect integration is not implemented. Use --demo for synthetic telemetry.");
    }
}
