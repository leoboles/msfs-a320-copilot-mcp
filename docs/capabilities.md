# Capabilities and field quality

`get_capabilities` is the machine-readable catalog (contract version 1). It
works in Mock and Real without connecting to MSFS or SimBridge. The catalog
comes from the same `FlyByWireParameters.All` definitions as the native reader,
so each field has its source variable, SDK/readout units, meaning, official
reference, validation status and evidence placeholders. Consult it before
relying on a dated skill or validation note. Catalog support is distinct from
availability in the loaded aircraft.

## Scope and compatibility

The intended environment is MSFS 2024 on Windows x64 with FlyByWire
A320neo/A32NX. Title screening requires a FlyByWire token and an A320,
A320neo or A32NX token and rejects other Airbus variants. It is conservative
screening, not a trusted aircraft identity or version probe. A custom livery
with an unrecognized title needs investigation rather than automatic acceptance.
The FlyByWire A380X is unsupported by these mappings.

There is no recorded fully validated FlyByWire version or per-position cockpit
matrix. `ValidatedAircraftVersion` and `CockpitValidationEvidence` are null for
every system field. [Local validation](local-validation.md) records successful
reads on specific dates, without establishing all positions or variants.
The official [Flight Deck API](https://docs.flybywiresim.com/aircraft/a32nx/a32nx-api/a32nx-flightdeck-api/)
describes a development-version interface and warns that its table may lag
implementation. Engine mappings reference the
[Systems API](https://docs.flybywiresim.com/aircraft/a32nx/a32nx-api/a32nx-systems-api/).

## Quality contract

Every `State.Systems` parameter has `Value`, `Unit`, `SimVar`, `Quality` and
`QualityReason`. Existing group/field names are retained, but `Value` is now
nullable. Consumers must handle null and quality before comparing values.

| Quality | Meaning | Consumer action |
| --- | --- | --- |
| `known` | Validated for the applicable aircraft/version with recorded evidence. No production system field currently qualifies. | Interpret within its documented meaning and snapshot freshness. |
| `unvalidated` | Finite reported number; variable existence and cockpit positions are not established. This includes zero. | Describe as reported telemetry; require explicit visual confirmation for checklist completion. |
| `unavailable` | Unsupported aircraft or non-finite optional value. `Value=null`, with reason. | Treat as unknown; never replace with zero/OFF. |
| `stale` | Data exceeded the configured age limit. | Do not use as current. The current API rejects stale/disconnected snapshots instead of returning old field values; status reports `Validity=stale`. |

A finite zero remains visible as an **unvalidated reported value** because the
numeric transport cannot distinguish an absent LVAR from a legitimate zero.
No heuristic upgrades a value to `known`. Non-finite optional fields become
unavailable individually; invalid title, truncation or non-finite basic flight
data still rejects the whole sample. A catalog entry never implies a connected
source. Missing DLL, simulator disconnection and SDK exceptions remain explicit
Real errors without mock fallback.

On an unsupported aircraft, all 58 system entries remain present with null
values and `unavailable` quality, and `Systems.Validation=unsupported_aircraft`.
Basic simulator fields still report that aircraft's title/flight data.
The change journal omits unavailable numeric values and reports quality changes
as `Group.Field.Quality` alongside availability/value events.

## Implemented fields

The basic SimConnect fields are title (`TITLE`), geometric MSL altitude in feet
(`PLANE ALTITUDE`), IAS in knots (`AIRSPEED INDICATED`), true heading in degrees
normalized to [0,360) (`PLANE HEADING DEGREES TRUE`), and on-ground flag
(`SIM ON GROUND`). `CapturedAtUtc` is local receipt time. Their source and
units appear under `BasicFields` in the catalog; the finite/structure checks
validate transport, not the precision of a cockpit instrument.

The 58 aircraft-specific fields all start as `unvalidated`. The exact variable
and units for **each** field are returned under `SystemFields` in the catalog:

| Group | Fields | Meaning/limits |
| --- | --- | --- |
| Overhead (18) | Battery1Auto, Battery2Auto, Battery1Fault, Battery2Fault, Battery1Voltage, Battery2Voltage, ExternalPowerOn, ExternalPowerAvailable, ApuMasterOn, ApuMasterFault, ApuStartOn, ApuAvailable, ApuBleedOn, Pack1On, Pack2On, Adirs1Mode, Adirs2Mode, Adirs3Mode | Selected mode, reported fault, voltage or availability are distinct. Battery AUTO and EXT PWR ON do not certify effective bus supply or cable removal. |
| Engines (10) | Engine1State, Engine1N1, Engine1N2, Engine1Egt, Engine1FuelFlow, Engine2State, Engine2N1, Engine2N2, Engine2Egt, Engine2FuelFlow | State enum, N1/N2 percent, EGT Celsius, fuel flow kg/hour. These do not certify complete start or absence of ECAM warnings. |
| Fuel (12) | Left1Active, Left1Switch, Left2Active, Left2Switch, Right1Active, Right1Switch, Right2Active, Right2Switch, Center1Open, Center1Switch, Center2Open, Center2Switch | Four wing-pump command/active pairs; two center transfer-valve command/opening pairs. No pressure/fault/flow proof. Center elements are transfer valves. |
| Controls (18) | ParkingBrakeLever, Engine1MasterOn, Engine2MasterOn, Engine1IgnitionMode, Engine2IgnitionMode, FlapsHandleIndex, SpeedBrakeHandle, SpoilersArmed, TransponderMode, AltitudeReportingOn, TcasMode, NoseLightSelector, BeaconOn, RunwayTurnOffLeftOn, RunwayTurnOffRightOn, ApuFireTestPressed, Engine1FireTestPressed, Engine2FireTestPressed | Selected controls and pressed test buttons. No actual flap surface position, physical brake effectiveness, fire-test success or movement authorization. |

MCDU uses SimBridge, reads only the left screen and has independent freshness,
status and events. Receipt time does not establish the producer's original data
age. [Monitor semantics](monitoring.md) and [MCDU setup](simbridge.md) apply.

Checklists persist ordered progress and evidence. Automatic telemetry
confirmation now requires a fresh real sample and `known` field quality; every
current system mapping requires explicit `source=user` confirmation instead.
This prevents an unvalidated zero from completing an OFF checklist item.
See [checklists](checklists.md). Historical sessions retain their old evidence;
they are not retroactively validated or treated as current state.

Still unsupported: fuel pressure/faults, effective bus supply, ECAM warning and
AVAIL indications, doors/tug/ground equipment, thrust levers, ground speed,
automatic phase detection/cross-phase prerequisites and aircraft commands.

## Validation workflow

Hosted Windows CI runs Release build/unit tests and explicit Mock/Settings MCP
smoke tests. It never requires MSFS, SimBridge or a proprietary DLL. Packaging
checks the published executable in Mock and Settings and includes these docs
and the smoke script. Neither CI nor packaging claims live validation.

Real testing is an explicit local action on a prepared Windows machine:

```powershell
pwsh -File scripts/Test-Mcp.ps1 -Mode Real -AircraftOnly
pwsh -File scripts/Test-Mcp.ps1 -Mode Real
```

Build first and configure the official DLL; start MSFS and load the intended
aircraft. The second command additionally requires SimBridge. To test a ZIP,
add `-ExecutablePath C:/A320Copilot/A320Copilot.Mcp.exe`. Real fails when a
connection/read is unavailable; Mock is never silently substituted. A real
read on another aircraft is allowed for basic telemetry but cannot confirm
A32NX systems. Position validation still requires a recorded cockpit comparison
for the installed version; a passing smoke test does not upgrade field quality.
