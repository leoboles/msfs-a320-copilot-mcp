namespace A320Copilot.Mcp;

public sealed class TelemetrySettings
{
    public string Mode { get; set; } = "Mock";

    public bool UseMock => Mode.Equals("Mock", StringComparison.OrdinalIgnoreCase);

    public void Validate()
    {
        if (!UseMock && !Mode.Equals("Real", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Telemetry:Mode must be Mock or Real.");
    }
}
