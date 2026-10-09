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

DemoAircraftStateSource produces static synthetic data with a current timestamp and a DEMO identifier. SimConnectAircraftStateSource owns a persistent read-only subscription on one background thread. TelemetryMonitorStore provides synchronized snapshots, monotonic sample age and a bounded event journal. There is no automatic fallback to demo data, and fictional state is never presented as a live connection.

The telemetry contract is an asynchronous, cancellable read of the latest valid sample. The host starts monitoring in Real mode and closes it at shutdown. Client cancellation does not stop the shared connection. Stale/disconnected data produces errors; receive failures and silence trigger reconnects. get_monitor_status and get_recent_events expose health and observations. See monitoring.md for defaults, thresholds, cursors and limitations. Failures and missing connections must not become zero values.

## Next steps

1. Compare mapped values with cockpit indications and manually changed switches.
2. Validate flight-session disconnect/reconnect and aircraft reload.
3. Cross-check the implemented fuel and cockpit controls against user-operated switches; see [implementation backlog](implementation-backlog.md). Extend validated mappings for pressure/faults, hydraulics and anti-ice.
4. Add richer per-variable availability metadata, flight phases and procedural rules on top of monitoring events.
5. Expand the implemented versioned training checklists and cross-phase rules as requirements develop; see [checklist storage and evidence](checklists.md). Aircraft control integration remains unimplemented.

In the stdio host, stdout is reserved for protocol messages and logs go to stderr. --demo prints JSON and exits. No command changes the simulator. The hangar scenario reports Source=mock and SimulatorConnected=false; batteries, engines, APU, and external power are off, with the parking brake set and chocks installed. The scenario cannot be mutated.

## Data source selection

The separate get_mcdu_state tool uses SimBridgeMcduReader in Real mode to read the left screen over a persistent WebSocket. It does not implement IAircraftStateSource because an MCDU screen is not general aircraft telemetry. One receive loop owns the socket; periodic requestUpdate messages confirm freshness even when the page is unchanged. McduMonitorStore owns synchronized screen snapshots, monotonic age and a bounded field-change journal. Queries reuse valid snapshots, reject stale/disconnected screens and do not open sockets. get_mcdu_status and get_recent_mcdu_events expose the independent MCDU monitor. Both monitors start only in Real mode and stop with the host. See [SimBridge integration](simbridge.md).

TelemetrySettings validates Telemetry:Mode (Mock or Real) at startup. TelemetryReader receives the mode and IAircraftStateSource through dependency injection. Mock reads HangarScenario; Real calls SimConnectAircraftStateSource and labels a response as real only after a successful read. MCP converts connection and read failures into tool errors without fallback. State.Systems exposes FlyByWire overhead and engine values with units, source variable names and a cockpit-validation limitation.

The host loads appsettings.json and optional appsettings.Local.json from the executable directory, accepts A320COPILOT_ environment variables, and applies --mock/--real last. The mode is fixed for the session; configuration changes require restarting the server. No MCP tool modifies these settings.
