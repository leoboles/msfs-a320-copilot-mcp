using System.ComponentModel;
using System.Text.Json;
using A320Copilot.Bridge;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class MonitorTools(TelemetrySettings settings, SimConnectAircraftStateSource source)
{
    [McpServerTool(Name = "get_monitor_status"), Description(
        "Reports persistent SimConnect connection, monitor ID, session ID, attempts, sample age and " +
        "fresh/stale/unavailable/disconnected validity. LastReceivedAtUtc is local receipt time. " +
        "Connected alone does not mean data is fresh. Mock mode disables the real monitor.")]
    public string GetMonitorStatus()
        => settings.UseMock ? JsonSerializer.Serialize(new { Source = "mock", Running = false, Validity = "disabled_in_mock_mode" })
            : JsonSerializer.Serialize(source.GetStatus());

    [McpServerTool(Name = "get_recent_events"), Description(
        "Reads a bounded in-memory journal of connection, freshness and reported parameter changes. " +
        "Use NextAfterSequence as the next cursor, tied to MonitorId; reset on a new MonitorId. " +
        "HistoryTruncated signals overwritten events. Events are observations, not procedural/safety alerts. " +
        "Reconnects and aircraft changes create a new baseline without inferring transitions across the gap.")]
    public string GetRecentEvents(long afterSequence = 0, int limit = 100)
    {
        if (afterSequence < 0 || limit is < 1 or > 200)
            throw new ModelContextProtocol.McpException("afterSequence must be >= 0 and limit between 1 and 200.");
        if (settings.UseMock) return JsonSerializer.Serialize(new { Source = "mock", Running = false, Events = Array.Empty<object>() });
        try { return JsonSerializer.Serialize(source.GetEvents(afterSequence, limit)); }
        catch (ArgumentException exception) { throw new ModelContextProtocol.McpException(exception.Message); }
    }
}
