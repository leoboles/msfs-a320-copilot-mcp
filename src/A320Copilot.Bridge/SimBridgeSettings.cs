namespace A320Copilot.Bridge;

public sealed class SimBridgeSettings
{
    public string McduWebSocketUrl { get; set; } = "ws://localhost:8380/interfaces/v1/mcdu";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaximumScreenAgeMilliseconds { get; set; } = 3000;
    public int RefreshIntervalMilliseconds { get; set; } = 1000;
    public int ReconnectDelayMilliseconds { get; set; } = 2000;
    public int EventCapacity { get; set; } = 500;

    public void Validate()
    {
        if (!Uri.TryCreate(McduWebSocketUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("SimBridge:McduWebSocketUrl must be a ws:// or wss:// URL without credentials.");
        if (TimeoutSeconds is < 1 or > 120)
            throw new ArgumentException("SimBridge:TimeoutSeconds must be between 1 and 120.");
        if (MaximumScreenAgeMilliseconds is < 100 or > 120000)
            throw new ArgumentException("SimBridge:MaximumScreenAgeMilliseconds must be between 100 and 120000.");
        if (RefreshIntervalMilliseconds is < 100 or > 60000)
            throw new ArgumentException("SimBridge:RefreshIntervalMilliseconds must be between 100 and 60000.");
        if (ReconnectDelayMilliseconds is < 50 or > 60000)
            throw new ArgumentException("SimBridge:ReconnectDelayMilliseconds must be between 50 and 60000.");
        if (EventCapacity is < 10 or > 10000)
            throw new ArgumentException("SimBridge:EventCapacity must be between 10 and 10000.");
    }
}
