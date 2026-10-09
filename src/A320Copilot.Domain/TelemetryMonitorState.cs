namespace A320Copilot.Domain;

public sealed record RealAircraftTelemetry(string Source, bool SimulatorConnected,
    TelemetryMonitorStatus Freshness, AircraftState State);

public sealed record TelemetryMonitorStatus(
    string Source, string MonitorId, bool Running, string ConnectionState,
    long SessionId, long ConnectionAttempts, long SamplesReceived,
    string Validity, double? AgeMilliseconds, int MaximumSampleAgeMilliseconds,
    DateTimeOffset? LastReceivedAtUtc, string? LastError, long LatestEventSequence);

public sealed record TelemetryChangeEvent(
    long Sequence, DateTimeOffset OccurredAtUtc, string Type, long SessionId,
    string? Field, object? PreviousValue, object? Value, string Description);

public sealed record TelemetryEventPage(
    string Source, string MonitorId, long OldestAvailableSequence, long LatestSequence,
    long NextAfterSequence, bool HistoryTruncated, bool HasMore,
    IReadOnlyList<TelemetryChangeEvent> Events);
