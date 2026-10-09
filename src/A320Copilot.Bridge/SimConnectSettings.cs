namespace A320Copilot.Bridge;

public sealed class SimConnectSettings
{
    public string LibraryPath { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaximumSampleAgeMilliseconds { get; set; } = 3000;
    public int ReconnectDelayMilliseconds { get; set; } = 2000;
    public int EventCapacity { get; set; } = 500;

    public void Validate()
    {
        if (TimeoutSeconds is < 1 or > 60)
            throw new ArgumentException("SimConnect:TimeoutSeconds must be between 1 and 60.");
        if (MaximumSampleAgeMilliseconds is < 100 or > 60000)
            throw new ArgumentException("SimConnect:MaximumSampleAgeMilliseconds must be between 100 and 60000.");
        if (ReconnectDelayMilliseconds is < 50 or > 60000)
            throw new ArgumentException("SimConnect:ReconnectDelayMilliseconds must be between 50 and 60000.");
        if (EventCapacity is < 10 or > 10000)
            throw new ArgumentException("SimConnect:EventCapacity must be between 10 and 10000.");
        if (!string.IsNullOrEmpty(LibraryPath) && !Path.IsPathFullyQualified(LibraryPath))
            throw new ArgumentException("SimConnect:LibraryPath must be an absolute path to the official x64 SimConnect.dll.");
    }
}
