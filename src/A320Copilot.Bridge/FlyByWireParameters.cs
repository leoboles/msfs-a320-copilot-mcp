namespace A320Copilot.Bridge;

/// <summary>Read-only mappings from the official A32NX Flight Deck and Systems APIs.
/// LVARs use SDK 'number' units; Unit describes their documented interpretation.</summary>
public static class FlyByWireParameters
{
    public sealed record Parameter(string Group, string Name, string SimVar, string Unit, string SdkUnit = "number");
    public static IReadOnlyList<Parameter> All { get; } = Array.AsReadOnly(new Parameter[]
    {
        O("Battery1Auto", "A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO"),
        O("Battery2Auto", "A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO"),
        O("Battery1Fault", "A32NX_OVHD_ELEC_BAT_1_PB_HAS_FAULT"),
        O("Battery2Fault", "A32NX_OVHD_ELEC_BAT_2_PB_HAS_FAULT"),
        O("Battery1Voltage", "A32NX_ELEC_BAT_1_POTENTIAL", "volts"),
        O("Battery2Voltage", "A32NX_ELEC_BAT_2_POTENTIAL", "volts"),
        O("ExternalPowerOn", "A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON"),
        O("ExternalPowerAvailable", "A32NX_EXT_PWR_AVAIL:1"),
        O("ApuMasterOn", "A32NX_OVHD_APU_MASTER_SW_PB_IS_ON"),
        O("ApuMasterFault", "A32NX_OVHD_APU_MASTER_SW_PB_HAS_FAULT"),
        O("ApuStartOn", "A32NX_OVHD_APU_START_PB_IS_ON"),
        O("ApuAvailable", "A32NX_OVHD_APU_START_PB_IS_AVAILABLE"),
        O("ApuBleedOn", "A32NX_OVHD_PNEU_APU_BLEED_PB_IS_ON"),
        O("Pack1On", "A32NX_OVHD_COND_PACK_1_PB_IS_ON"),
        O("Pack2On", "A32NX_OVHD_COND_PACK_2_PB_IS_ON"),
        O("Adirs1Mode", "A32NX_OVHD_ADIRS_IR_1_MODE_SELECTOR_KNOB", "enum:0=OFF,1=NAV,2=ATT"),
        O("Adirs2Mode", "A32NX_OVHD_ADIRS_IR_2_MODE_SELECTOR_KNOB", "enum:0=OFF,1=NAV,2=ATT"),
        O("Adirs3Mode", "A32NX_OVHD_ADIRS_IR_3_MODE_SELECTOR_KNOB", "enum:0=OFF,1=NAV,2=ATT"),
        E(1, "State", "STATE", "enum:0=OFF,1=ON,2=STARTING,3=RESTARTING,4=SHUTTING"),
        E(1, "N1", "N1", "percent"), E(1, "N2", "N2", "percent"),
        E(1, "Egt", "EGT", "celsius"), E(1, "FuelFlow", "FF", "kg/hour"),
        E(2, "State", "STATE", "enum:0=OFF,1=ON,2=STARTING,3=RESTARTING,4=SHUTTING"),
        E(2, "N1", "N1", "percent"), E(2, "N2", "N2", "percent"),
        E(2, "Egt", "EGT", "celsius"), E(2, "FuelFlow", "FF", "kg/hour"),
        F("Left1", "PUMP", 2, "Active"), F("Left1", "PUMP", 2, "Switch"),
        F("Left2", "PUMP", 5, "Active"), F("Left2", "PUMP", 5, "Switch"),
        F("Right1", "PUMP", 3, "Active"), F("Right1", "PUMP", 3, "Switch"),
        F("Right2", "PUMP", 6, "Active"), F("Right2", "PUMP", 6, "Switch"),
        F("Center1", "VALVE", 9, "Open"), F("Center1", "VALVE", 9, "Switch"),
        F("Center2", "VALVE", 10, "Open"), F("Center2", "VALVE", 10, "Switch"),
        new("Controls", "ParkingBrakeLever", "L:A32NX_PARK_BRAKE_LEVER_POS", "bool:0=false,1=true"),
        new("Controls", "Engine1MasterOn", "FUELSYSTEM VALVE SWITCH:1", "bool:0=false,1=true", "Bool"),
        new("Controls", "Engine2MasterOn", "FUELSYSTEM VALVE SWITCH:2", "bool:0=false,1=true", "Bool"),
        new("Controls", "Engine1IgnitionMode", "TURB ENG IGNITION SWITCH EX1:1", "enum:0=CRANK,1=NORM,2=IGN_START"),
        new("Controls", "Engine2IgnitionMode", "TURB ENG IGNITION SWITCH EX1:2", "enum:0=CRANK,1=NORM,2=IGN_START"),
        new("Controls", "FlapsHandleIndex", "L:A32NX_FLAPS_HANDLE_INDEX", "enum:0=UP,1=1,2=2,3=3,4=FULL"),
        new("Controls", "SpeedBrakeHandle", "L:A32NX_SPOILERS_HANDLE_POSITION", "ratio:0=retracted,1=full"),
        new("Controls", "SpoilersArmed", "L:A32NX_SPOILERS_ARMED", "bool:0=false,1=true"),
        new("Controls", "TransponderMode", "L:A32NX_TRANSPONDER_MODE", "enum:0=STBY,1=AUTO,2=ON"),
        new("Controls", "AltitudeReportingOn", "L:A32NX_SWITCH_ATC_ALT", "bool:0=false,1=true"),
        new("Controls", "TcasMode", "L:A32NX_SWITCH_TCAS_POSITION", "enum:0=STBY,1=TA,2=TA_RA"),
        new("Controls", "NoseLightSelector", "L:LIGHTING_LANDING_1", "enum:0=TO,1=TAXI,2=OFF"),
        new("Controls", "BeaconOn", "LIGHT BEACON", "bool:0=false,1=true", "Bool"),
        new("Controls", "RunwayTurnOffLeftOn", "CIRCUIT SWITCH ON:21", "bool:0=false,1=true", "Bool"),
        new("Controls", "RunwayTurnOffRightOn", "CIRCUIT SWITCH ON:22", "bool:0=false,1=true", "Bool"),
        new("Controls", "ApuFireTestPressed", "L:A32NX_FIRE_TEST_APU", "bool:0=false,1=true"),
        new("Controls", "Engine1FireTestPressed", "L:A32NX_FIRE_TEST_ENG1", "bool:0=false,1=true"),
        new("Controls", "Engine2FireTestPressed", "L:A32NX_FIRE_TEST_ENG2", "bool:0=false,1=true")
    });

    private static Parameter O(string name, string variable, string unit = "bool:0=false,1=true")
        => new("Overhead", name, "L:" + variable, unit);
    private static Parameter E(int engine, string name, string variable, string unit)
        => new("Engines", $"Engine{engine}{name}", $"L:A32NX_ENGINE_{variable}:{engine}", unit);
    private static Parameter F(string name, string kind, int index, string state)
        => new("Fuel", name + state, $"FUELSYSTEM {kind} {state.ToUpperInvariant()}:{index}",
            state == "Open" ? "ratio:0=closed,1=fully_open" : "bool:0=false,1=true",
            state == "Open" ? "Percent Over 100" : "Bool");
}
