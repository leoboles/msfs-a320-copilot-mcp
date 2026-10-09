# Local validation — 2026-10-08

## Persistent SimBridge MCDU — 22:31 São Paulo time

Implemented one persistent WebSocket receive loop, background screen refresh,
automatic reconnect, monotonic cache age and a bounded field-change journal.
get_mcdu_state preserves the full-screen contract and adds Freshness;
get_mcdu_status and get_recent_mcdu_events expose health and incremental events.
Queries do not open sockets. Mock mode starts neither real monitor.

Release compilation succeeded and all 48 unit tests passed. Tests cover one
connection across concurrent reads, client cancellation isolation, periodic
confirmation of unchanged screens, stale errors, recovery, echo-only silence
timeouts/reconnect, reset baselines, line/page/scratchpad/brightness changes,
monotonic age and cursor retention/paging.

The live MCP test ran 20.28 seconds with one connection attempt and SessionId=1,
receiving 20 screens and issuing 20 requestUpdate messages. The stable page was
FUEL PRED. All screens stayed valid under the default 3000 ms limit; final age
was about 639 ms. Among 41 full-screen calls the median was 0.40 ms, p95 0.92 ms,
and maximum 63.60 ms (initial wait). Event queries had a 0.34 ms median and 38
empty incremental pages, with no false page/screen changes. The earlier
per-call MCDU benchmark had a 43.64 ms median. These measure local MCP round
trips including client JSON parsing; they exclude chat/AI processing time.

A separate real test with a child-process-only 100 ms age threshold confirmed
stale status, explicit MCP rejection of the old screen, data_stale/data_fresh
events and fresh recovery in the same session. The Mock test and graceful
shutdown (exit 0) passed. A final real smoke test read both aircraft systems
through SimConnect and the MCDU through SimBridge in the updated host.

Reports (Git-ignored): artifacts/mcdu-monitor-validation.json,
artifacts/mcdu-stale-validation.json, artifacts/mcdu-mock-validation.json and
artifacts/mcp-both-persistent-validation.txt. The before-change timing report
is artifacts/mcp-latency-validation.json.

Page changes and reconnection were tested with substitute connections; the
user's cockpit was not operated and SimBridge/MSFS was not stopped. The live
test confirms short-term reuse, cache freshness and protocol responses, not
long-flight stability or accuracy of every screen field.

## Persistent monitor — 22:14 São Paulo time

Implemented a single-thread persistent SimConnect subscription, automatic
reconnect, monotonic sample age and bounded change events. New MCP tools are
get_monitor_status and get_recent_events; get_aircraft_state includes Freshness
and refuses stale/disconnected state.

Release build passed without warnings/errors. All 39 unit tests passed.
The real persistent test retained one MonitorId and SessionId=1 with one
connection attempt over 45.74 seconds, advancing from 1 to 45 samples. Final
sample age was about 530 ms under the default 3000 ms limit. Closing stdin
shut the MCP down with exit code 0. A separate 20-second run also passed.
Mock MCP and monitor tests passed, and a subsequent real MCDU read succeeded.

A separate real test used a temporary, process-only 100 ms age limit: it
observed stale status, an explicit MCP error rejecting old state, data_stale
and data_fresh events, and recovery to fresh samples without reconnection.
Reports are artifacts/monitor-validation-final.json and
artifacts/monitor-stale-validation.json (Git-ignored).

The stable live run produced connection/baseline events, with no relevant
parameter changes. Parameter transitions, disconnection and silence/reconnect
were validated using substitute sessions in automated tests. No aircraft
controls were changed and MSFS was not closed for testing. These bounded tests
do not establish long-flight stability or prove the cause of earlier crashes.

## Implemented SimConnect and systems — 21:20 São Paulo time

The native read-only adapter now returns real aircraft telemetry plus 18
overhead and 10 engine LVAR values through get_aircraft_state. Real MCP calls
passed against FlightSimulator2024.exe using the official installed legacy
client DLL configured in Git-ignored appsettings.Local.json. The initial
response used runtime ObjectID 524288 rather than the USER=0 request alias;
the reader now associates data by request and definition IDs.

Sample: Airbus A320neo FlyByWire, 21.63 ft geometric MSL, 0 KIAS, 110.54 degrees
true, on ground. Both batteries reported AUTO and 28 V; external power ON and
available; APU master ON, AVAIL and bleed ON; packs ON; all three ADIRS in NAV.
Engine 1 reported STARTING, N1 15.10%, N2 60.19%, EGT 453.80 C, fuel flow
313.34 kg/h. Engine 2 reported ON, N1 19.62%, N2 68.22%, EGT 374.78 C,
fuel flow 298.71 kg/h. These are the received values, not cockpit-verified
indications. Subsequent engine samples may differ as startup progresses.
MCDU MENU still read successfully through SimBridge.

Earlier unimplemented-adapter results below are historical. See simconnect.md
for current configuration, scope and validation limitations.

## Live retry — 21:12 São Paulo time

The repeated real-mode MCP test passed. `get_mcdu_state` returned in about
94 ms with `Source=simbridge`, `IsMock=false` and a left MCDU screen containing
12 empty lines, empty title and scratchpad, and `displayBrightness=0`.
This validates receiving a screen update through the full MCP/SimBridge path.
It does not establish aircraft electrical state or validate readable page text.
The next live check is to display a visible MCDU page and compare its returned
title and text with the cockpit, then change pages manually and read again.
General SimConnect telemetry still returns the expected unimplemented error.

Checkout: `main`, commit `1bb48eb`. The existing implementation was retained.
The SimBridge reader was introduced by `d01eb89`; later commits add English
documentation, Windows release automation and a remote bridge design proposal.

## Environment and results

- Windows x64; installed system runtime .NET 10.0.11, no system SDK.
- Installed official .NET SDK 10.0.401 in `../.dotnet-sdk` for local builds.
- NuGet restore succeeded.
- Release build succeeded with zero warnings and zero errors.
- All 23 unit tests passed.
- MCP stdio initialization, tool discovery, mock aircraft and mock MCDU passed.
- Real `get_aircraft_state` returned the expected explicit unimplemented
  SimConnect error without substituting mock data.
- SimBridge process `fbw-simbridge.exe` was running. Its installed manifest
  reports `0.7.0-94300badd`.
- `http://localhost:8380/interfaces/mcdu` returned HTTP 200.
- Direct WebSocket connection to `ws://localhost:8380/interfaces/v1/mcdu`
  reached `Open`; sending `requestUpdate` returned the relayed `requestUpdate`.
  This confirms the gateway connection, not aircraft data availability.
- MCP real `get_mcdu_state` timed out after 10 seconds without a screen update.
- No FlightSimulator process was found during the checks. A loaded FlyByWire
  aircraft and real screen contents have therefore **not** been validated.

## Change

`scripts/Test-Mcp.ps1 -Mode Real` previously stopped after checking the
unimplemented SimConnect error. It now also calls `get_mcdu_state`, requires
non-mock SimBridge screen data, and prints the returned screen for verification.
The extended test correctly fails when no live MCDU update arrives.
No production connection changes were justified by these results.

## Repeat after loading the aircraft

Run from the repository root with MSFS 2024 and the FlyByWire A320 loaded:

```powershell
./scripts/Test-Mcp.ps1 -Dotnet ../.dotnet-sdk/dotnet.exe -Mode Real
```

Compare the returned title, lines and scratchpad with the cockpit MCDU.
Change the displayed page manually and repeat to confirm a fresh update.
If no update arrives, first check whether the official remote MCDU page receives
the aircraft screen and whether the aircraft's SimBridge connection is enabled.
No simulator settings, firewall rules or aircraft controls were changed.

The next implementation increment after live MCDU validation is the read-only
SimConnect adapter for aircraft title, altitude, airspeed, heading and on-ground
status described in `simconnect.md`.
# Checklist and cockpit telemetry increment — 2026-10-08

Release build and 56 unit tests passed. Added tests cover separated fuel/control mappings, change events, persisted progress, ordering, skipped items, reopening prerequisites, optimistic revisions, path validation, mock rejection, and matched/mismatched real-monitor evidence.

Live `Test-Checklist.ps1` passed against the running MSFS 2024 FlyByWire A320 and SimBridge. Both sources returned fresh data. The MCP returned 12 Fuel fields and 18 Controls fields in addition to existing overhead/engine readings. The isolated test successfully confirmed a selected wing-pump switch with real evidence, rejected an old revision, and resumed progress after restarting the MCP. Four wing pumps reported selected/active, both center transfer valves selected/open, and the parking-brake lever reported set at the observation time. These are dated reported readings, not current state or completed flight checklist.

`Test-Monitor.ps1` with the expanded schema passed over 3.2 seconds: one session/connection attempt, four samples, fresh final data and graceful exit code 0. This is a short regression check, not long-flight endurance testing. New parameter changes are unit-tested; no switches, simulator configuration, engines or other controls were operated during the live tests. One-switch-at-a-time cockpit validation and physical pressure/fault validation remain pending. Test checklist files are isolated under ignored artifacts and do not represent the pilot's actual flight progress.

Reports: `artifacts/checklist-live-validation.json` and `artifacts/monitor-controls-validation.json`. See [checklist tools](checklists.md) for storage, scope and evidence semantics.

