using System.ComponentModel;
using System.Text.Json;
using A320Copilot.Bridge;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class AircraftTools
{
    [McpServerTool(Name = "get_aircraft_state"), Description(
        "Returns MOCK data for a fictional FlyByWire A320 powered off in a hangar. " +
        "Use to practice cockpit conversations. Never live MSFS telemetry. Read-only static scenario.")]
    public string GetAircraftState() => JsonSerializer.Serialize(HangarScenario.Create());
}
