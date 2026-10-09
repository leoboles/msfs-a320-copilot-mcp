using System.ComponentModel;
using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Domain;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class ChecklistTools(ChecklistStore store, TelemetryReader reader)
{
    [McpServerTool(Name = "list_checklist_templates"), Description("Lists versioned FBW simulator training sequences. Partial training checklists, not an airline SOP, flight readiness or ATC clearance. Never sends aircraft commands.")]
    public string Templates() => JsonSerializer.Serialize(ChecklistStore.Templates);

    [McpServerTool(Name = "start_checklist"), Description("Creates new pending checklist progress for a named flight/session. Call only when the user wants this checklist; never silently reuse confirmations from another flight. Does not control the aircraft.")]
    public string Start(string templateId, string flightLabel) => Result(() => store.Start(templateId, flightLabel));

    [McpServerTool(Name = "list_checklist_sessions"), Description("Lists locally persisted checklist sessions to explicitly resume the correct flight. Historical progress is not live aircraft state.")]
    public string Sessions() => Result(store.List);

    [McpServerTool(Name = "get_checklist"), Description("Returns persisted progress, evidence, revision and next pending item. Skipped items remain skipped and prevent AllItemsConfirmed. Historical confirmations do not prove current state or readiness; use fresh telemetry where relevant.")]
    public string Get(string sessionId) => Result(() => store.Get(sessionId));

    [McpServerTool(Name = "update_checklist_item"), Description("Records an explicit user confirmation or skip, or confirms a mapped switch from freshly validated real telemetry. status: confirmed/skipped/pending; source: user/telemetry. Never invent user confirmation. Telemetry cannot confirm fire-test success, clearance, equipment removal or full engine-start completion. expectedRevision prevents overwriting concurrent edits. Reopening an item resets subsequent progress. Does not send simulator commands.")]
    public async Task<string> Update(string sessionId, int expectedRevision, string itemId, string status,
        string source, string note, CancellationToken cancellationToken)
    {
        try { return await UpdateCore(sessionId, expectedRevision, itemId, status, source, note, cancellationToken); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException)
        { throw new ModelContextProtocol.McpException(exception.Message); }
    }

    private static string Result(Func<object> action)
    {
        try { return JsonSerializer.Serialize(action()); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or JsonException)
        { throw new ModelContextProtocol.McpException(exception.Message); }
    }

    private async Task<string> UpdateCore(string sessionId, int expectedRevision, string itemId, string status,
        string source, string note, CancellationToken cancellationToken)
    {
        if (status is not ("confirmed" or "skipped" or "pending")) throw new ArgumentException("Invalid checklist status.");
        if (source is not ("user" or "telemetry")) throw new ArgumentException("source must be user or telemetry.");
        ChecklistEvidence? evidence = status == "pending" ? null : new(source, DateTimeOffset.UtcNow, note);
        if (source == "telemetry" && status != "confirmed") throw new ArgumentException("Telemetry can only confirm a mapped item.");
        if (source == "telemetry")
        {
            var view = store.Get(sessionId);
            var item = view.Session.Template.Items.SingleOrDefault(i => i.Id == itemId) ?? throw new ArgumentException("Unknown item.");
            if (item.TelemetryField is null || item.ExpectedValue is null)
                throw new InvalidOperationException("This item requires user visual/aural or operational confirmation; no automatic completion mapping.");
            var json = await reader.ReadAsync(cancellationToken);
            var snapshot = JsonSerializer.Deserialize<RealAircraftTelemetry>(json);
            if (snapshot is null || snapshot.Source != "real" || !snapshot.SimulatorConnected || snapshot.Freshness is null ||
                snapshot.Freshness.Validity != "fresh" || snapshot.Freshness.ConnectionState != "connected" ||
                snapshot.State.Systems is null)
                throw new InvalidOperationException("Fresh real FlyByWire telemetry is required; mock/unavailable data cannot confirm a checklist.");
            var parts = item.TelemetryField.Split('.');
            var group = parts[0] switch
            {
                "Overhead" => snapshot.State.Systems.Overhead,
                "Fuel" => snapshot.State.Systems.Fuel,
                "Controls" => snapshot.State.Systems.Controls,
                _ => throw new InvalidOperationException("Unknown checklist telemetry group.")
            };
            if (!group.TryGetValue(parts[1], out var value) || value.Quality != FieldQuality.Known || value.Value is null)
                throw new InvalidOperationException("Checklist telemetry requires a known, validated field; unavailable, unvalidated or stale values require explicit user confirmation.");
            if (value.Value != item.ExpectedValue)
                throw new InvalidOperationException("Reported telemetry does not match this checklist item; no confirmation recorded.");
            evidence = new("telemetry", DateTimeOffset.UtcNow,
                note + " Validated reported control value; not proof of physical operation.",
                snapshot.State.CapturedAtUtc, snapshot.Freshness.MonitorId, snapshot.Freshness.SessionId, item.TelemetryField, value.Value);
        }
        return JsonSerializer.Serialize(store.Update(sessionId, expectedRevision, itemId, status, evidence));
    }
}
