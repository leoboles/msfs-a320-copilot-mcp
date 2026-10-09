using System.Text.RegularExpressions;

namespace A320Copilot.Bridge;

/// <summary>Conservative title screening, not proof of aircraft version or LVAR existence.</summary>
public static partial class AircraftCompatibility
{
    public static bool IsA32Nx(string title) =>
        FlyByWire().IsMatch(title) && A320().IsMatch(title) && !OtherAirbus().IsMatch(title);

    [GeneratedRegex(@"\bFlyByWire\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FlyByWire();
    [GeneratedRegex(@"\b(?:A320(?:neo)?|A32NX)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex A320();
    [GeneratedRegex(@"\b(?:A380(?:X)?|A319|A321(?:neo)?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OtherAirbus();
}
