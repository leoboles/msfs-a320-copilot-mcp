using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Domain;

// Bootstrap executable only. MCP transport and tools will be added separately.
if (args.Length != 1 || args[0] != "--demo")
{
    Console.Error.WriteLine("Bootstrap only; MCP and SimConnect are not implemented.");
    Console.Error.WriteLine("Usage: dotnet run --project src/A320Copilot.Mcp -- --demo");
    return 2;
}

IAircraftStateSource source = new DemoAircraftStateSource();
var state = await source.ReadAsync();
Console.WriteLine(JsonSerializer.Serialize(new { Source = "demo", State = state },
    new JsonSerializerOptions { WriteIndented = true }));
return 0;
