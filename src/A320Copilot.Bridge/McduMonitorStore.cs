using System.Text.Json;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>Immutable screen snapshots, monotonic freshness and independent client event cursors.</summary>
public sealed class McduMonitorStore
{
    private readonly object gate = new();
    private readonly SimBridgeSettings settings;
    private readonly TimeProvider clock;
    private readonly string monitorId = Guid.NewGuid().ToString("N");
    private readonly Queue<TelemetryChangeEvent> events = new();
    private Dictionary<string, JsonElement>? baseline;
    private McduState? screen;
    private long receivedTimestamp, sessionId, attempts, received, requests, sequence;
    private bool running, sessionHasScreen, staleAnnounced;
    private string connection = "stopped";
    private string? lastError;

    public McduMonitorStore(SimBridgeSettings settings, TimeProvider? clock = null)
    {
        settings.Validate();
        this.settings = settings;
        this.clock = clock ?? TimeProvider.System;
    }

    public void BeginConnection()
    {
        lock (gate)
        {
            running = true;
            connection = "connecting";
            sessionId++;
            attempts++;
            sessionHasScreen = false;
            baseline = null;
            Add("connecting", null, null, null, "Opening the read-only SimBridge MCDU connection.");
        }
    }

    public void SocketConnected()
    {
        lock (gate)
        {
            connection = "connected";
            Add("connected", null, null, null, "SimBridge WebSocket connected; a current screen is still required.");
        }
    }

    public void UpdateRequested() { lock (gate) requests++; }

    public void Publish(JsonElement left)
    {
        lock (gate)
        {
            var values = Flatten(left);
            if (baseline is null)
            {
                baseline = values;
                Add("baseline_created", null, null, values.GetValueOrDefault("title"),
                    "First screen in this session; no changes across connection gaps inferred.");
            }
            else
            {
                foreach (var (field, value) in values)
                {
                    var existed = baseline.TryGetValue(field, out var previous);
                    if (existed && !Changed(field, previous, value)) continue;
                    Add(field is "title" or "titleLeft" or "page" ? "page_changed" : "screen_changed",
                        field, existed ? previous : null, value, "Reported MCDU screen field changed.");
                    baseline[field] = value;
                }
                foreach (var field in baseline.Keys.Except(values.Keys).ToArray())
                {
                    Add("screen_changed", field, baseline[field], null, "Screen field is no longer present.");
                    baseline.Remove(field);
                }
            }
            if (staleAnnounced) Add("data_fresh", null, null, null, "Fresh MCDU updates resumed.");
            staleAnnounced = false;
            sessionHasScreen = true;
            connection = "connected";
            lastError = null;
            received++;
            receivedTimestamp = clock.GetTimestamp();
            screen = new("simbridge", false, clock.GetUtcNow(), "left_mcdu_screen_only", left.Clone());
        }
    }

    public void CheckFreshness()
    {
        lock (gate)
        {
            if (connection == "connected" && sessionHasScreen &&
                Age() > settings.MaximumScreenAgeMilliseconds && !staleAnnounced)
            {
                staleAnnounced = true;
                Add("data_stale", null, null, null, "Last screen exceeded its maximum age; do not use it as current.");
            }
        }
    }

    public void Disconnected(string error)
    {
        lock (gate)
        {
            connection = "disconnected";
            sessionHasScreen = false;
            baseline = null;
            lastError = error;
            Add("disconnected", null, null, null, error);
        }
    }

    public void Stopped()
    {
        lock (gate)
        {
            running = false;
            connection = "stopped";
            sessionHasScreen = false;
            baseline = null;
            Add("stopped", null, null, null, "MCDU monitor stopped.");
        }
    }

    private double? Age() => screen is null ? null : Math.Max(0, clock.GetElapsedTime(receivedTimestamp).TotalMilliseconds);

    public McduMonitorStatus GetStatus()
    {
        lock (gate)
        {
            CheckFreshness();
            var age = Age();
            var validity = connection is "disconnected" or "stopped" ? "disconnected"
                : !sessionHasScreen ? "unavailable"
                : age > settings.MaximumScreenAgeMilliseconds ? "stale" : "fresh";
            return new("simbridge", monitorId, running, connection, sessionId, attempts, received, requests,
                validity, age, settings.MaximumScreenAgeMilliseconds, settings.RefreshIntervalMilliseconds,
                screen?.ReceivedAtUtc, lastError, sequence);
        }
    }

    public McduState? GetFreshSnapshot()
    {
        lock (gate)
        {
            var status = GetStatus();
            return status.Validity == "fresh" && screen is not null ? screen with { Freshness = status } : null;
        }
    }

    public TelemetryEventPage GetEvents(long afterSequence = 0, int limit = 100)
    {
        if (afterSequence < 0 || limit is < 1 or > 200)
            throw new ArgumentException("afterSequence must be >= 0 and limit between 1 and 200.");
        lock (gate)
        {
            CheckFreshness();
            if (afterSequence > sequence)
                throw new ArgumentException("Cursor exceeds this MCDU monitor's sequence. Reset after MonitorId changes.");
            var oldest = events.TryPeek(out var first) ? first.Sequence : sequence + 1;
            var selected = events.Where(e => e.Sequence > afterSequence).Take(limit).ToArray();
            var next = selected.Length == 0 ? afterSequence : selected[^1].Sequence;
            return new("simbridge", monitorId, oldest, sequence, next, afterSequence < oldest - 1, next < sequence, selected);
        }
    }

    private void Add(string type, string? field, object? previous, object? value, string description)
    {
        events.Enqueue(new(++sequence, clock.GetUtcNow(), type, sessionId, field, previous, value, description));
        while (events.Count > settings.EventCapacity) events.Dequeue();
    }

    private static Dictionary<string, JsonElement> Flatten(JsonElement left)
    {
        var values = new Dictionary<string, JsonElement>();
        foreach (var field in left.EnumerateObject())
        {
            if (field.Name == "lines" && field.Value.ValueKind == JsonValueKind.Array)
            {
                var row = 0;
                foreach (var line in field.Value.EnumerateArray()) values[$"lines[{row++}]"] = line.Clone();
            }
            else values[field.Name] = field.Value.Clone();
        }
        return values;
    }

    private static bool Changed(string field, JsonElement previous, JsonElement current)
    {
        if (field is "displayBrightness" or "integralBrightness" &&
            previous.ValueKind == JsonValueKind.Number && current.ValueKind == JsonValueKind.Number &&
            previous.TryGetDouble(out var a) && current.TryGetDouble(out var b))
            return Math.Abs(a - b) >= 0.01;
        return !JsonElement.DeepEquals(previous, current);
    }
}
