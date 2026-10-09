using System.ComponentModel;
using System.Text.Json;
using A320Copilot.Bridge;
using A320Copilot.Domain;
using ModelContextProtocol.Server;

namespace A320Copilot.Mcp;

[McpServerToolType]
public sealed class CapabilityTools(TelemetrySettings settings)
{
    [McpServerTool(Name = "get_capabilities"), Description(
        "Returns the implemented read-only capabilities and per-field catalog without connecting to MSFS or SimBridge. " +
        "Includes source variables, units, meaning, documentation and validation limits. Static support does not prove live " +
        "availability: check each parameter Quality and snapshot Freshness. Unvalidated/unknown values cannot certify checklist items.")]
    public string GetCapabilities() => JsonSerializer.Serialize(new
    {
        ContractVersion = 1,
        ConfiguredMode = settings.UseMock ? "Mock" : "Real",
        AircraftControlSupported = false,
        Aircraft = new
        {
            IntendedSimulator = "MSFS 2024, Windows x64",
            IntendedAircraft = "FlyByWire A320neo / A32NX",
            Identification = "Title screening requires FlyByWire and A320/A320neo/A32NX; not proof of version or LVAR existence.",
            ValidatedAircraftVersion = (string?)null,
            CompatibilityEvidence = "docs/local-validation.md: dated local reads; no full version/position validation."
        },
        BasicFields = new[]
        {
            new { Field = "AircraftTitle", SimVar = "TITLE", Unit = "text", Meaning = "reported_title_not_version_identity" },
            new { Field = "AltitudeFeet", SimVar = "PLANE ALTITUDE", Unit = "feet", Meaning = "geometric_msl_not_indicated_altitude" },
            new { Field = "IndicatedAirspeedKnots", SimVar = "AIRSPEED INDICATED", Unit = "knots", Meaning = "indicated_airspeed_not_ground_speed" },
            new { Field = "HeadingDegreesTrue", SimVar = "PLANE HEADING DEGREES TRUE", Unit = "degrees", Meaning = "true_heading_normalized_0_to_360" },
            new { Field = "IsOnGround", SimVar = "SIM ON GROUND", Unit = "bool", Meaning = "reported_on_ground_state" }
        },
        SystemFields = FlyByWireParameters.All,
        QualityStates = new Dictionary<string, string>
        {
            [FieldQuality.Known] = "Validated for the applicable aircraft/version with recorded evidence; no production system field currently qualifies.",
            [FieldQuality.Unvalidated] = "Finite reported value; variable existence and cockpit positions are not established, including zero.",
            [FieldQuality.Unavailable] = "Unknown: unsupported aircraft or non-finite value. Value is null, never synthetic zero.",
            [FieldQuality.Stale] = "Snapshot age exceeds the configured limit. State reads reject stale/disconnected data; use monitor status."
        },
        Mcdu = new { Source = "simbridge", Scope = "left_mcdu_screen_only", ReadOnly = true, Validation = "transport_receipt_not_producer_timestamp" },
        Checklists = new { Persisted = true, TelemetryConfirmationRequiresQuality = FieldQuality.Known, CrossPhaseDependencies = false },
        Unsupported = new[] { "fuel_pressure_and_faults", "effective_electrical_bus_supply", "fire_test_success",
            "ecam_warnings_and_engine_avail", "doors_and_ground_equipment", "thrust_levers", "ground_speed", "aircraft_commands" }
    });
}
