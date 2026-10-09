using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>Thread-safe snapshots and a bounded event journal. Freshness uses monotonic time.</summary>
public sealed class TelemetryMonitorStore
{
    private readonly object gate = new();
    private readonly SimConnectSettings settings;
    private readonly TimeProvider clock;
    private readonly string monitorId = Guid.NewGuid().ToString("N");
    private readonly Queue<TelemetryChangeEvent> events = new();
    private Dictionary<string, object>? baseline;
    private AircraftState? sample;
    private long receivedTimestamp, attempts, sessionId, samples, sequence;
    private DateTimeOffset? receivedAt;
    private bool running, staleAnnounced;
    private string connection = "stopped";
    private string? lastError;

    public TelemetryMonitorStore(SimConnectSettings settings, TimeProvider? clock = null)
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
            attempts++;
            sessionId++;
            baseline = null;
            Add("connecting", null, null, null, "Opening a read-only SimConnect session.");
        }
    }

    public void Publish(AircraftState state)
    {
        lock (gate)
        {
            var priorConnection = connection;
            connection = "connected";
            lastError = null;
            if (priorConnection != "connected") Add("connected", null, null, null, "Received aircraft data from SimConnect.");
            var values = Flatten(state);
            var aircraftChanged = baseline is not null && !Equals(baseline["AircraftTitle"], state.AircraftTitle);
            if (baseline is null || aircraftChanged)
            {
                if (aircraftChanged) Add("aircraft_changed", "AircraftTitle", baseline!["AircraftTitle"], state.AircraftTitle, "Aircraft changed; parameter comparison baseline reset.");
                baseline = values;
                Add("baseline_created", null, null, state.AircraftTitle, "First sample for this aircraft/session; no switch transitions inferred.");
            }
            else
            {
                foreach (var (field, value) in values)
                {
                    if (!baseline.TryGetValue(field, out var old))
                        Add("parameter_available", field, null, value, "Parameter appeared in the sample.");
                    else if (!Changed(field, old, value)) continue;
                    else Add("parameter_changed", field, old, value, "Reported value changed beyond its event threshold.");
                    baseline[field] = value;
                }
                foreach (var field in baseline.Keys.Except(values.Keys).ToArray())
                {
                    Add("parameter_unavailable", field, baseline[field], null, "Parameter is no longer present.");
                    baseline.Remove(field);
                }
            }
            if (staleAnnounced) Add("data_fresh", null, null, null, "Fresh samples resumed.");
            staleAnnounced = false;
            sample = state;
            samples++;
            receivedAt = clock.GetUtcNow();
            receivedTimestamp = clock.GetTimestamp();
        }
    }

    public void CheckFreshness()
    {
        lock (gate)
        {
            if (connection == "connected" && sample is not null && Age() > settings.MaximumSampleAgeMilliseconds && !staleAnnounced)
            {
                staleAnnounced = true;
                Add("data_stale", null, null, null, "No sample within the maximum age; last state must not be used as current.");
            }
        }
    }

    public void Disconnected(string error)
    {
        lock (gate)
        {
            connection = "disconnected";
            lastError = error;
            baseline = null;
            Add("disconnected", null, null, null, error);
        }
    }

    public void Stopped()
    {
        lock (gate)
        {
            running = false;
            connection = "stopped";
            baseline = null;
            Add("stopped", null, null, null, "Telemetry monitor stopped.");
        }
    }

    private double? Age() => sample is null ? null : Math.Max(0, clock.GetElapsedTime(receivedTimestamp).TotalMilliseconds);

    public TelemetryMonitorStatus GetStatus()
    {
        lock (gate)
        {
            CheckFreshness();
            var age = Age();
            var validity = sample is null ? "unavailable" : connection != "connected" ? "disconnected"
                : age > settings.MaximumSampleAgeMilliseconds ? "stale" : "fresh";
            return new("real", monitorId, running, connection, sessionId, attempts, samples, validity,
                age, settings.MaximumSampleAgeMilliseconds, receivedAt, lastError, sequence);
        }
    }

    public AircraftState? GetFreshSample()
    {
        lock (gate) return GetStatus().Validity == "fresh" ? sample : null;
    }

    public RealAircraftTelemetry? GetFreshSnapshot()
    {
        lock (gate)
        {
            var status = GetStatus();
            return status.Validity == "fresh" && sample is not null ? new("real", true, status, sample) : null;
        }
    }

    public TelemetryEventPage GetEvents(long afterSequence = 0, int limit = 100)
    {
        if (afterSequence < 0 || limit is < 1 or > 200) throw new ArgumentException("afterSequence must be >= 0 and limit between 1 and 200.");
        lock (gate)
        {
            CheckFreshness();
            if (afterSequence > sequence) throw new ArgumentException("Cursor exceeds this monitor's sequence. Check MonitorId and reset the cursor after a process restart.");
            var oldest = events.TryPeek(out var first) ? first.Sequence : sequence + 1;
            var selected = events.Where(e => e.Sequence > afterSequence).Take(limit).ToArray();
            var next = selected.Length == 0 ? afterSequence : selected[^1].Sequence;
            return new("real", monitorId, oldest, sequence, next, afterSequence < oldest - 1,
                next < sequence, selected);
        }
    }

    private void Add(string type, string? field, object? old, object? value, string description)
    {
        events.Enqueue(new(++sequence, clock.GetUtcNow(), type, sessionId, field, old, value, description));
        while (events.Count > settings.EventCapacity) events.Dequeue();
    }

    private static Dictionary<string, object> Flatten(AircraftState state)
    {
        var values = new Dictionary<string, object>
        {
            ["AircraftTitle"] = state.AircraftTitle, ["AltitudeFeet"] = state.AltitudeFeet,
            ["IndicatedAirspeedKnots"] = state.IndicatedAirspeedKnots,
            ["HeadingDegreesTrue"] = state.HeadingDegreesTrue, ["IsOnGround"] = state.IsOnGround
        };
        if (state.Systems is not null)
        {
            foreach (var (key, p) in state.Systems.Overhead) values["Overhead." + key] = p.Value;
            foreach (var (key, p) in state.Systems.Engines) values["Engines." + key] = p.Value;
            foreach (var (key, p) in state.Systems.Fuel) values["Fuel." + key] = p.Value;
            foreach (var (key, p) in state.Systems.Controls) values["Controls." + key] = p.Value;
        }
        return values;
    }

    private static bool Changed(string field, object old, object value)
    {
        if (old is not double a || value is not double b) return !Equals(old, value);
        var difference = Math.Abs(b - a);
        if (field == "HeadingDegreesTrue") difference = Math.Min(difference, 360 - difference);
        var threshold = field switch
        {
            "AltitudeFeet" => 10, "IndicatedAirspeedKnots" => 1, "HeadingDegreesTrue" => 2,
            _ when field.EndsWith("Voltage", StringComparison.Ordinal) => 0.2,
            _ when field.StartsWith("Engines.", StringComparison.Ordinal) && field.EndsWith("Egt", StringComparison.Ordinal) => 5,
            _ when field.StartsWith("Engines.", StringComparison.Ordinal) && field.EndsWith("FuelFlow", StringComparison.Ordinal) => 20,
            _ when field.StartsWith("Engines.", StringComparison.Ordinal) && (field.EndsWith("N1", StringComparison.Ordinal) || field.EndsWith("N2", StringComparison.Ordinal)) => 1,
            _ => 0
        };
        return threshold == 0 ? difference > 0 : difference >= threshold;
    }
}
