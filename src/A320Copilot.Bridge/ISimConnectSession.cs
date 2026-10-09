using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>A read-only native subscription. All methods, including disposal, run on one worker thread.</summary>
public interface ISimConnectSession : IDisposable
{
    /// <summary>Define fields once and subscribe to one sample per second.</summary>
    void RequestSample();
    AircraftState? ReadNext();
}
