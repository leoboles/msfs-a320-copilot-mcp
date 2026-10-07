namespace A320Copilot.Bridge;

public sealed class SimBridgeSettings
{
    public string McduWebSocketUrl { get; set; } = "ws://localhost:8380/interfaces/v1/mcdu";
    public int TimeoutSeconds { get; set; } = 10;

    public void Validate()
    {
        if (!Uri.TryCreate(McduWebSocketUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("SimBridge:McduWebSocketUrl must be a ws:// or wss:// URL without credentials.");
        if (TimeoutSeconds is < 1 or > 120)
            throw new ArgumentException("SimBridge:TimeoutSeconds must be between 1 and 120.");
    }
}
