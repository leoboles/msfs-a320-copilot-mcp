using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Domain;

namespace A320Copilot.Mcp;

public sealed class TelemetryReader
{
    private readonly bool useMock;
    private readonly IAircraftStateSource realSource;

    public TelemetryReader(TelemetrySettings settings, IAircraftStateSource realSource)
    {
        settings.Validate();
        useMock = settings.UseMock;
        this.realSource = realSource;
    }

    public async ValueTask<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (useMock)
            return JsonSerializer.Serialize(HangarScenario.Create());

        // No fallback: a failed real read must never be replaced with mock data.
        if (realSource is SimConnectAircraftStateSource monitor)
            return JsonSerializer.Serialize(await monitor.ReadSnapshotAsync(cancellationToken));
        var state = await realSource.ReadAsync(cancellationToken);
        return JsonSerializer.Serialize(new { Source = "real", SimulatorConnected = true, State = state });
    }
}
