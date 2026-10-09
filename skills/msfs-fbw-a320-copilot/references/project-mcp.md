# Projeto msfs-a320-copilot-mcp

Use the registered local MCP server. For script fallback, locate this repository
on the current machine; do not assume another computer's checkout or SDK path.
Call `get_capabilities` for the current field catalog and validation limits.

## Tool scope

- `get_aircraft_state`: real source is native SimConnect. It reports aircraft title, altitude, indicated airspeed, heading and on-ground state, plus the implemented FlyByWire overhead and engine LVAR fields. Inspect the response's source, connection flag, timestamp, and `Systems.Validation` caveat.
- `get_mcdu_state`: reads the left MCDU screen through the local SimBridge WebSocket. Real data should have `Source=simbridge`, `IsMock=false`, and a receipt timestamp. It is screen-only; it does not control the MCDU or provide general SimVars.
- These reads are separate and may succeed or fail independently. No automatic fallback from Real to Mock is intended. Never use a mock response as live state.

## Real-mode fallback

If the installed plugin's MCP tools are unavailable, use its setup skill and
restart the client to restore tool access. For an explicit diagnostic on a
machine with PowerShell 7 (`pwsh`), invoke from the plugin root:

```powershell
pwsh -File scripts/Test-Mcp.ps1 -PluginLauncher scripts/plugin/Start-Plugin.ps1 -Mode Real
```

This uses the plugin's downloaded runtime and saved configuration. It does not
require a .NET SDK. When working with a built source checkout instead, invoke:

```powershell
./scripts/Test-Mcp.ps1 -Mode Real
```

The script starts the stdio MCP and calls both tools. It is read-only for the aircraft and MCDU. Do not edit source or settings to make a demonstration work. Report connection or timeout failures plainly; they do not prove the simulator or SimBridge is stopped.

Project documentation:

- `README.md` describes current implementation scope.
- `docs/simbridge.md` documents the MCDU reader and its limits.
- `docs/simconnect.md` documents aircraft telemetry and configuration.
- `docs/local-validation.md` is a dated validation record, not a source of current aircraft state.

The system is simulation-only. The MCP now provides partial simulator training checklist templates and persisted session progress. This skill provides conversational guidance; neither layer supplies a complete airline SOP or an autonomous readiness/flight-control engine.

## Persistent reads, freshness and events

SimConnect and the SimBridge WebSocket persist for the lifetime of the MCP server process, with automatic reconnection. Prefer direct MCP calls on the existing server. The fallback Test-Mcp.ps1 starts and stops a new server on each invocation: it is a smoke test, not a persistent conversational client. Do not confuse its startup or model/tool latency with cached MCP read latency.

Both state tools expose Freshness. Check Source, ConnectionState and Validity; connected alone does not establish fresh data. The default maximum age is 3000 ms, but use the returned configured limit. Stale/disconnected snapshots must not be reported as current. SimConnect and SimBridge have independent monitors, sessions and event cursors.

- Aircraft: get_monitor_status and get_recent_events.
- MCDU: get_mcdu_status and get_recent_mcdu_events.
- Keep each event cursor with its MonitorId, advance with NextAfterSequence, and resynchronize with a full valid snapshot after a monitor restart or truncated history. Events do not reconstruct changes during a disconnect and do not constitute a checklist/alert engine.
- Obtain full state initially; use status/events for incremental checks when a persistent client is available. Read the changed state as needed. Silence or an unchanged value is not evidence of successful checklist completion.

Current scope verified against the project on 2026-10-08: battery switches/faults/voltages, external-power selected/available, APU master/start/available/bleed, packs, ADIRS selectors and engine state/N1/N2/EGT/fuel flow. This is a dated capability record: inspect current responses and project documentation before treating a later feature as unsupported.

At that verification, the MCP did not expose fuel-pump switches, fire-test completion, parking brake, thrust levers, engine master/mode selectors, gear/flaps, exterior lights, transponder, doors, tug or ground equipment, ECAM AVAIL/warnings, electrical bus supply or ground speed. Require user confirmation where relevant. IAS zero is not proof of zero ground speed; external power OFF is not proof the cable is removed; APU Master OFF is not immediate proof of complete shutdown; N2 near idle is not proof of ECAM AVAIL or no warnings. Engine enum values outside the documented mapping remain unknown.

Consult docs/monitoring.md and docs/simbridge.md for current monitor semantics, docs/copilot-session-notes.md for user preferences, and docs/implementation-backlog.md for pending features. A feature request is not implemented merely because it was recorded.

## Checklist and telemetry update — later on 2026-10-08

This section supersedes the earlier dated capability record above. The project now exposes State.Systems.Fuel (12 fields: six switch commands, four wing-pump active states, two center transfer-valve opening ratios) and Controls (18 fields: parking brake lever, engine masters/ignition modes, flaps/spoilers, transponder/altitude reporting/TCAS, selected exterior lights and three momentary fire-test button states). All are reported observations, not cockpit-cross-checked proof. The two center controls operate transfer valves in the neo jet-pump system, not conventional electric center pumps. Pressure/fault indications remain unavailable. Fire-test pressed state does not prove test duration, correct lights/sounds or successful completion. Engine-state metadata is now 0=OFF, 1=ON, 2=STARTING, 3=RESTARTING, 4=SHUTTING.

Use list_checklist_templates, then start_checklist(templateId, flightLabel) for the phase the user wants to perform. The available phase IDs are preparation, before_start, pushback, engine_start, after_start and before_taxi. Keep the returned session ID and revision. get_checklist returns progress and the next pending item; list_checklist_sessions locates a session for explicit resumption. Do not choose an old flight merely because it was most recent.

For update_checklist_item supply sessionId, expectedRevision, itemId, status (confirmed/skipped/pending), source (user/telemetry) and note. Only record user confirmation actually given; only skip on explicit user instruction. Source telemetry triggers a fresh real read and requires a mapped expected value with Quality=known. All current system fields are unvalidated, so require explicit visual confirmation using source=user. Unavailable/null, unvalidated, stale, mock or mismatched data cannot confirm. Consult get_capabilities and docs/capabilities.md for current source, units, meaning and validation evidence. Fire-test success, full engine-start completion and clearances remain human-confirmed. Reopening a prerequisite clears later progress; a revision conflict requires reloading before retrying.

Progress persists locally across MCP restarts, independently of the in-memory telemetry event journals. Templates are pinned per checklist session. Confirmations are historical; read current telemetry when making a current-state claim. Skipped is not confirmed, and a null next item with skipped entries is not a completed checklist. Completion of one partial phase does not establish aircraft readiness or enforce prerequisites across other phase sessions. Read docs/checklists.md for the storage override and detailed semantics. The tools never operate simulator controls.

When checklist tools are not available directly, do not use Test-Checklist.ps1 to operate the user's real progress: it is an isolated automated validation harness. Use an available persistent MCP client or maintain an explicitly conversational checklist until actual tool access is available; do not claim MCP persistence for chat-only notes.
