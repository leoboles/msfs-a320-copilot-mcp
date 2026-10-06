using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args is ["--demo"])
{
    Console.WriteLine(JsonSerializer.Serialize(HangarScenario.Create(),
        new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (args is not ["--mock"])
{
    Console.Error.WriteLine("Usage: --mock (MCP stdio) or --demo (JSON). No live connection.");
    return 2;
}
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<AircraftTools>();
await builder.Build().RunAsync();
return 0;
