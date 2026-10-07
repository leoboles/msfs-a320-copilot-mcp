# SimConnect integration plan

## Extension point

Implement IAircraftStateSource in SimConnectAircraftStateSource. The current class throws NotSupportedException and does not load native libraries.

Live integration must run on Windows and be validated against the MSFS 2024 SDK. The repository does not include proprietary DLLs or an unofficial NuGet wrapper. Compatibility between the managed wrapper and .NET 10 still needs verification; if necessary, isolate the adapter in a separate Windows process.

## First increment

- Open a session and handle an unavailable simulator.
- Implement the message/event processing required by the selected wrapper.
- Define and register data structures and units; request samples for the user's aircraft.
- Map aircraft title, altitude in feet, indicated airspeed in knots, true heading in degrees, and on-ground status.
- Handle shutdown, disconnection, cancellation, resource disposal, and expired samples.
- Validate each field against the cockpit and add mapping tests that do not require the simulator.

Investigate FlyByWire-specific variables after standard telemetry. SimBridge and SimConnect are separate integrations; the SimBridge MCDU reader does not replace general SimConnect telemetry.

## Official references

- [MSFS 2024 SimConnect SDK](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/simconnect-sdk/)
- [SimConnect in MSFS 2024](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/)

Follow the documentation and examples for the installed SDK version.
