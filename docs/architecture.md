# Initial architecture

## Dependencies

```text
A320Copilot.Mcp -> A320Copilot.Bridge -> A320Copilot.Domain
              -> A320Copilot.Domain
A320Copilot.Tests -> A320Copilot.Bridge
                  -> A320Copilot.Mcp
```

Domain contains AircraftState and IAircraftStateSource. The model declares units and a UTC timestamp to avoid ambiguous interpretation. Bridge handles data access and keeps SDK details outside the domain. Mcp exposes get_aircraft_state through the official MCP SDK and stdio transport. HangarScenario is a fixed fictional scenario independent of the future live telemetry source.

## Current behavior

DemoAircraftStateSource produces static synthetic data with a current timestamp and a DEMO identifier. SimConnectAircraftStateSource explicitly rejects reads. There is no automatic fallback to demo data, and fictional state is never presented as a live connection.

The telemetry contract is an asynchronous, cancellable read. A future implementation may subscribe to events internally and return the latest valid sample; freshness and expiration rules must be defined first. Failures and missing connections must not become zero values.

## Next steps

1. Implement and validate SimConnect connection, message handling, and disconnection on Windows.
2. Map basic telemetry with explicit units and track sample age.
3. Verify FlyByWire-specific variables separately from standard SimVars.
4. Connect the existing aircraft-state MCP tool to live telemetry while retaining explicit mode selection.
5. Add checklists and then control integration as requirements develop.

In the stdio host, stdout is reserved for protocol messages and logs go to stderr. --demo prints JSON and exits. No command changes the simulator. The hangar scenario reports Source=mock and SimulatorConnected=false; batteries, engines, APU, and external power are off, with the parking brake set and chocks installed. The scenario cannot be mutated.

## Data source selection

The separate get_mcdu_state tool uses SimBridgeMcduReader in Real mode to read the left screen over WebSocket. It does not implement IAircraftStateSource because an MCDU screen is not general aircraft telemetry. Each call requests a fresh update and disposes the connection; failures never return a cached screen. See [SimBridge integration](simbridge.md).

TelemetrySettings validates Telemetry:Mode (Mock or Real) at startup. TelemetryReader receives the mode and IAircraftStateSource through dependency injection. Mock reads HangarScenario; Real calls SimConnectAircraftStateSource and labels a response as real only after a successful read. The current SimConnect implementation remains unavailable; MCP converts this limitation into a tool error without fallback.

The host loads appsettings.json from the executable directory, accepts A320COPILOT_Telemetry__Mode, and applies --mock/--real last. The mode is fixed for the session; configuration changes require restarting the server. No MCP tool modifies these settings.
