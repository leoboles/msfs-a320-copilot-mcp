using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Mcp;
using Xunit;

namespace A320Copilot.Tests;

public sealed class CapabilityTests
{
    [Theory]
    [InlineData("Mock")]
    [InlineData("Real")]
    public void CatalogIsAvailableWithoutSimulatorAndDoesNotClaimValidation(string mode)
    {
        using var json = JsonDocument.Parse(new CapabilityTools(new() { Mode = mode }).GetCapabilities());
        Assert.Equal(mode, json.RootElement.GetProperty("ConfiguredMode").GetString());
        Assert.False(json.RootElement.GetProperty("AircraftControlSupported").GetBoolean());
        var fields = json.RootElement.GetProperty("SystemFields").EnumerateArray().ToArray();
        Assert.Equal(FlyByWireParameters.All.Count, fields.Length);
        Assert.Equal(fields.Length, fields.Select(f => f.GetProperty("Field").GetString()).Distinct().Count());
        Assert.All(fields, field =>
        {
            Assert.Equal("unvalidated", field.GetProperty("Validation").GetString());
            Assert.Equal(JsonValueKind.Null, field.GetProperty("ValidatedAircraftVersion").ValueKind);
            Assert.Equal(JsonValueKind.Null, field.GetProperty("CockpitValidationEvidence").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(field.GetProperty("Unit").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(field.GetProperty("SimVar").GetString()));
        });
    }
}
