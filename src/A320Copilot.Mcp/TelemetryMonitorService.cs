using A320Copilot.Bridge;
using Microsoft.Extensions.Hosting;

namespace A320Copilot.Mcp;

public sealed class TelemetryMonitorService(TelemetrySettings settings, SimConnectAircraftStateSource source,
    SimBridgeMcduReader mcdu) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.UseMock) { source.Start(); mcdu.Start(); }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.WhenAll(source.StopAsync(cancellationToken), mcdu.StopAsync(cancellationToken));
}
