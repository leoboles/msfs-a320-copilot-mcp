using System.ComponentModel;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class AircraftTools(TelemetryReader reader)
{
    [McpServerTool(Name = "get_aircraft_state"), Description(
        "Reads aircraft state using the configured telemetry mode. Mock returns a fictional " +
        "powered-off A320 in a hangar. Real reads the latest valid sample from the persistent SimConnect " +
        "monitor, includes Freshness age/session metadata, and reports an error for stale/disconnected data " +
        "without substituting mock data. Real includes FlyByWire overhead " +
        "and engine LVAR values plus Fuel and Controls (parking brake, engine selectors, flaps/spoilers, " +
        "transponder/lights and fire-test buttons) under State.Systems, with units and variable names. Fuel separates " +
        "four wing pumps from two center transfer valves; switch command is not proof of pressure or flow. " +
        "Each parameter includes Quality and QualityReason. Unavailable fields have Value=null; " +
        "unvalidated values (including zero) require cockpit confirmation. Use get_capabilities for the catalog. " +
        "A fire-test button value does not prove successful indications. These " +
        "are reported values, not a full cockpit validation; zero alone does not prove LVAR availability. " +
        "Battery AUTO is a pushbutton mode, not proof a battery powers a bus. Check Source before interpreting data.")]
    public async Task<string> GetAircraftState(CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or
            NotSupportedException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            throw new ModelContextProtocol.McpException(
                $"Real telemetry unavailable: {exception.Message} " +
                "No mock data was returned. Set Telemetry:Mode to Mock to practice.");
        }
    }
}
