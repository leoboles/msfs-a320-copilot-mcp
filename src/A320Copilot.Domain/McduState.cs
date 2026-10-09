using System.Text.Json;
using System.Text.Json.Serialization;

namespace A320Copilot.Domain;

/// <summary>Screen contents, not a general aircraft telemetry sample.</summary>
public sealed record McduState(
    string Source, bool IsMock, DateTimeOffset ReceivedAtUtc,
    string Scope, JsonElement Left)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McduMonitorStatus? Freshness { get; init; }
}

public sealed record McduMonitorStatus(
    string Source, string MonitorId, bool Running, string ConnectionState,
    long SessionId, long ConnectionAttempts, long ScreensReceived, long UpdateRequestsSent,
    string Validity, double? AgeMilliseconds, int MaximumScreenAgeMilliseconds,
    int RefreshIntervalMilliseconds, DateTimeOffset? LastReceivedAtUtc,
    string? LastError, long LatestEventSequence);
