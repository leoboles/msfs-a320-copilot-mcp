# Read-only SimConnect telemetry

In Real mode a single background thread owns a persistent native SimConnect subscription. `get_aircraft_state` returns the latest valid sample with age/session metadata; stale or disconnected state is rejected. Failures and prolonged silence trigger automatic reconnection. Calls support cancellation without stopping other clients' monitoring. No simulator-write or event-transmit functions are imported. See [freshness, events and settings](monitoring.md).

## Setup

Use Windows x64 and an official x64 SimConnect.dll from your installed SDK. Place it beside the application or configure the absolute SimConnect:LibraryPath. SimConnect:TimeoutSeconds accepts 1–60. A320COPILOT_SimConnect__LibraryPath and A320COPILOT_SimConnect__TimeoutSeconds override configuration files. Restart the MCP after changes.

An optional appsettings.Local.json overrides base settings before environment variables. It is ignored by Git, copied to build output and excluded from publishing. This computer uses the official DLL installed at C:\XboxGames\Microsoft Flight Simulator\Content\SimConnect.dll. MSFS 2024 accepted this legacy client for the implemented features. SimConnect_internal.dll is not used. No proprietary DLL is committed or redistributed.

## Data

The response contains Source=real, SimulatorConnected=true and State. Basic fields are AircraftTitle (TITLE), AltitudeFeet (PLANE ALTITUDE, geometric MSL rather than barometric indicated altitude or AGL), IndicatedAirspeedKnots, HeadingDegreesTrue (normalized true heading rather than magnetic), IsOnGround and CapturedAtUtc (local UTC receipt time).

State.Systems.Overhead contains battery 1/2 AUTO mode, fault and voltage; external power ON/available; APU master ON/fault, START ON/available, APU BLEED ON; PACK 1/2 ON; and ADIRS 1/2/3 selector modes. State.Systems.Engines contains state, N1, N2, EGT and fuel flow for each engine. Each parameter has Value, Unit and SimVar. Systems is null for aircraft whose title does not contain FlyByWire.

Systems.Validation=reported_lvar_values_not_cross_checked_with_cockpit records the validation limit. A numeric response, especially zero, cannot prove that every LVAR exists or matches the installed aircraft version. Compare individual values with cockpit indications and manual changes. Battery AUTO is pushbutton mode, not proof the battery powers a bus. APU START ON and APU AVAIL are distinct. These are reported LVAR values, not inferences from the MCDU. This first catalog does not cover every overhead control; fuel pumps, hydraulics, anti-ice and flight-control pushbuttons remain future additions.

Missing libraries, wrong architecture, connection failures, SDK exceptions, simulator shutdown and missing fresh samples produce tool errors. An unsupported definition that triggers an SDK exception fails the read instead of substituting zero. The response associates data by request and definition IDs; the actual runtime ObjectID need not equal the USER=0 request alias.

## Verification

Build and test the solution, then run scripts/Test-Mcp.ps1 -Mode Mock or -Mode Real. Use -AircraftOnly to test real aircraft telemetry independently of SimBridge. This checkout uses -Dotnet ../.dotnet-sdk/dotnet.exe. The script exercises MCP initialization, discovery and tool calls, prints the real JSON and verifies overhead/engine fields are present. Unit tests cover packed binary mapping, system offsets, invalid data, cancellation, deadline disposal and JSON exposure.

Live reads on 2026-10-08 returned aircraft telemetry and nonzero engine start-up values through the full MCP path. See local-validation.md. Manual cockpit comparisons, flight-session disconnect/reconnect and reload validation remain outstanding. No automated control changes were made to create test states.

## Official references

- [MSFS 2024 SDK and legacy compatibility](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/simconnect-sdk/)
- [Data definitions and LVAR support](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/events-and-data/simconnect_addtodatadefinition/)
- [Packed data structure](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/structures-and-enumerations/simconnect_recv_simobject_data/)
- [A32NX Flight Deck API](https://docs.flybywiresim.com/aircraft/a32nx/a32nx-api/a32nx-flightdeck-api/)
- [A32NX Systems API](https://docs.flybywiresim.com/aircraft/a32nx/a32nx-api/a32nx-systems-api/)
