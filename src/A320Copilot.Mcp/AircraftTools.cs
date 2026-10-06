using System.ComponentModel;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class AircraftTools(TelemetryReader reader)
{
    [McpServerTool(Name = "get_aircraft_state"), Description(
        "Reads aircraft state using the configured telemetry mode. Mock returns a fictional " +
        "powered-off A320 in a hangar. Real requests SimConnect telemetry and reports an error " +
        "when unavailable; it never substitutes mock data. Check Source before interpreting data.")]
    public async Task<string> GetAircraftState(CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken);
        }
        catch (NotSupportedException)
        {
            throw new ModelContextProtocol.McpException(
                "Real telemetry unavailable: SimConnect integration is not implemented. " +
                "No mock data was returned. Set Telemetry:Mode to Mock to practice.");
        }
    }
}
