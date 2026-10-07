using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Mcp;
using A320Copilot.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args is ["--demo"])
{
    Console.WriteLine(JsonSerializer.Serialize(HangarScenario.Create(),
        new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (args.Length > 0 && args is not ["--mock"] && args is not ["--real"])
{
    Console.Error.WriteLine("Usage: no arguments (settings), --mock, --real, or --demo (mock JSON).");
    return 2;
}
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = AppContext.BaseDirectory,
    Args = []
});
builder.Configuration.AddEnvironmentVariables("A320COPILOT_");
var settings = new TelemetrySettings();
builder.Configuration.GetSection("Telemetry").Bind(settings);
if (args is ["--mock"]) settings.Mode = "Mock";
if (args is ["--real"]) settings.Mode = "Real";
var simBridgeSettings = new SimBridgeSettings();
builder.Configuration.GetSection("SimBridge").Bind(simBridgeSettings);
try
{
    settings.Validate();
    simBridgeSettings.Validate();
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(simBridgeSettings);
builder.Services.AddSingleton<Func<IMcduConnection>>(() => new McduWebSocketConnection());
builder.Services.AddSingleton<SimBridgeMcduReader>();
builder.Services.AddSingleton<IAircraftStateSource, SimConnectAircraftStateSource>();
builder.Services.AddSingleton<TelemetryReader>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<AircraftTools>().WithTools<McduTools>();
await builder.Build().RunAsync();
return 0;
