using System.Text.Json;

namespace A320Copilot.Domain;

/// <summary>Screen contents, not a general aircraft telemetry sample.</summary>
public sealed record McduState(
    string Source, bool IsMock, DateTimeOffset ReceivedAtUtc,
    string Scope, JsonElement Left);
