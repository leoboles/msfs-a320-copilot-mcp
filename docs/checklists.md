# Simulator checklist sessions

The MCP owns versioned checklist templates and persisted progress. The copilot skill owns conversational guidance, explanations and appropriate use of the tools. This prevents progress from depending solely on chat memory. These are partial simulator training sequences, not a complete airline SOP, dispatch release or readiness engine.

## Tools

- `list_checklist_templates`: preparation, before_start, pushback, engine_start, after_start and before_taxi. Each template has its own ordered items and scope.
- `start_checklist(templateId, flightLabel)`: creates an independent, entirely pending session. Never reuse an old flight's confirmations implicitly.
- `list_checklist_sessions`: locate a session for explicit resumption.
- `get_checklist(sessionId)`: returns the pinned template, revision, progress, evidence and next pending item.
- `update_checklist_item(sessionId, expectedRevision, itemId, status, source, note)`: record confirmed/skipped/pending. Source is user or telemetry. User confirmation requires an actual user statement; a skip requires an explicit user decision and explanation. Reopening a prerequisite resets all later items. Concurrent clients with old revisions get an actionable conflict; reload before retrying.

The ordered phases are separate checklists, allowing the pilot to select the phase applicable to an already running aircraft. Dependencies between different phase sessions are not automatically enforced. Prerequisite items require explicit confirmation; do not claim the whole flight is ready from one checklist. Some cockpit-preparation items are intentionally grouped and require checking the official procedure. The APU and both engine fire tests precede APU start in the preparation template as the user's simulator-session preference, not a universal operator SOP.

## Evidence and freshness

Mapped telemetry confirmation obtains a fresh real FlyByWire sample through the same reader/freshness policy as get_aircraft_state, requires Quality=known and a non-null matching field value, then saves sample time, monitor/session identity, field and reported value. Mock, unsupported items, unavailable/unvalidated/stale fields and mismatches cannot confirm. All current production system mappings are unvalidated, so use explicit source=user visual confirmation. A finite zero does not establish LVAR existence or an OFF position. See [capabilities and field quality](capabilities.md). Historical evidence is preserved and is not retroactively upgraded to validated/current state.

Fire-test button state cannot establish successful visual/aural results. Engine N2 alone cannot establish successful start or absence of warnings. Clearances, doors/ground equipment and full electrical supply likewise require appropriate human confirmation. None of the tools operates aircraft controls.

Confirmations are historical evidence, not continuous assertions. get_checklist does not silently refresh or validate every old switch setting; obtain current telemetry when relevant. AllItemsConfirmed is true only when every item is confirmed, never when an item was skipped. A null next item with skipped entries does not mean successful completion. Checklist sessions do not deliver unsolicited notifications or implement automatic flight-phase detection.

## Storage and tests

Default storage: `%LOCALAPPDATA%/A320Copilot/checklists`. Configure `Checklists:StoragePath` or `A320COPILOT_Checklists__StoragePath` for a different directory. Each session has a JSON file. A shared file lock, expected revision and atomic replacement protect cooperating local MCP processes. The template is pinned in the file so an update does not silently reorder an existing session. This is local single-machine storage, not cloud synchronization; choose the same path to share across local clients.

Run `scripts/Test-Checklist.ps1` with MSFS and SimBridge running; supply `-Dotnet` with your local executable path if needed. It writes only isolated test progress under ignored artifacts, reads live telemetry and MCDU, and checks quality/mismatch rejection, conflicts and persistence across process restart. It does not fill the pilot's real checklist.

## Sources

Templates follow the relevant portions of the [FBW preparation guide](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/starting-the-aircraft/) and [engine start/taxi guide](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/engine-start-taxi/), with the fire-test ordering preference documented above. Verify applicable operator procedures and simulator changes before expanding them.
