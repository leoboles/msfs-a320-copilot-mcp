# Persistent telemetry, freshness and change events

In Real mode the MCP host starts one background worker. It opens a SimConnect
session, defines data once and subscribes to samples every simulated second.
All native calls, including close and reconnect, occur on that same thread.
The native module stays loaded until process exit rather than being unloaded
after each read. The MCDU SimBridge reader has its own persistent WebSocket,
validity and change journal; see [MCDU monitoring](simbridge.md). Its MonitorId
and event cursor are independent of this SimConnect monitor.

Persistence lasts for the lifetime of one MCP server process. A client should
keep that process running: launching Test-Mcp.ps1 for every question starts a
new monitor each time. Mock mode never starts the real connection. Normal host
shutdown cancels the worker and closes the native session.

## MCP tools

### get_aircraft_state

The real response retains Source, SimulatorConnected and State, and adds
Freshness with the status below. It returns the latest valid background sample,
which may be the same sample across closely spaced calls. It does not request a
new connection or force a sample for every call.

Samples older than the configured limit, or samples from a disconnected session,
are not returned as current aircraft state. These cases produce MCP tool errors.
At startup, the call can wait up to TimeoutSeconds for the first valid sample.
Cancellation of one request does not stop the shared monitor.

### get_monitor_status

Returns Source, MonitorId, Running, ConnectionState, SessionId,
ConnectionAttempts, SamplesReceived, Validity, AgeMilliseconds,
MaximumSampleAgeMilliseconds, LastReceivedAtUtc, LastError and
LatestEventSequence.

- Validity is fresh, stale, unavailable (no sample yet), or disconnected.
- A connected session may still have stale data. Check both fields.
- Age uses monotonic receipt time, so wall-clock adjustments do not extend validity.
- LastReceivedAtUtc is local receipt time, not a simulator clock timestamp.
- LastError clears after a successful sample; counts span reconnects in this process.

### get_recent_events

Arguments: afterSequence (default 0) and limit (1–200, default 100).
The response includes MonitorId, OldestAvailableSequence, LatestSequence,
NextAfterSequence, HistoryTruncated, HasMore and Events.

Each event has Sequence, OccurredAtUtc, Type, SessionId, Field, PreviousValue,
Value and Description. Types include connecting, connected, disconnected,
stopped, baseline_created, aircraft_changed, data_stale, data_fresh,
parameter_changed, parameter_available and parameter_unavailable.

Use NextAfterSequence for the next call. Store the cursor together with MonitorId;
reset it when MonitorId changes. A cursor beyond the current sequence is rejected.
If HistoryTruncated is true, older events have been overwritten: obtain a fresh
state instead of inferring missing transitions. HasMore means another page exists.
Queries do not consume events or interfere with other clients' cursors.

The journal is bounded, in memory only, and resets on process restart. A reconnect
or aircraft-title change establishes a new baseline: changes during the data gap
are not invented. A same-title aircraft reload cannot be identified conclusively
by these fields alone. Events describe reported data, not procedural or safety
alerts. They do not prove LVAR availability or cockpit correctness.

## Settings and behavior

Settings live under SimConnect, with the normal environment-variable overrides:

| Setting | Default | Purpose |
| --- | --- | --- |
| MaximumSampleAgeMilliseconds | 3000 | Maximum valid sample age |
| TimeoutSeconds | 10 | Startup/read deadline and silence timeout before reconnect |
| ReconnectDelayMilliseconds | 2000 | Delay between connection attempts |
| EventCapacity | 500 | Maximum retained events |

While data is stale, reads fail even before the receive timeout. Exceptions and
silence cause close/reconnect; stale samples cannot become valid merely because
a new session opens. A new sample is required. Simulator pause or loading can
interrupt samples and will therefore be reported as stale/unavailable.

To reduce noise, event thresholds are 10 ft altitude, 1 knot IAS, 2 degrees true
heading (circular difference), 0.2 V battery voltage, 1 percentage point N1/N2,
5 C EGT and 20 kg/h fuel flow. Switches and enum values report exact changes.
Thresholds compare against the last emitted value, allowing slow accumulated
changes to generate events. The latest state still contains unrounded values.

## Validation

Run scripts/Test-Monitor.ps1 -Dotnet ../.dotnet-sdk/dotnet.exe -Mode Real to make
repeated MCP calls within one process. It checks advancing sample counts, one
session, valid sample ages, cursor behavior and graceful shutdown. Results go to
artifacts/monitor-validation.json. Mock mode verifies no real connection starts.

Add -VerifyStaleHandling -DurationSeconds 2 for a process-only test that uses a
100 ms age threshold. It verifies stale MCP errors and data_stale/data_fresh
events against real incoming samples. No simulator settings or controls change.

Unit tests cover concurrency, session ownership, stale rejection, cancellation,
silence recovery, reconnect baselines, changed values, heading wrap, thresholds,
journal bounds and cursor paging. Live tests on 2026-10-08 observed 45 samples
over approximately 45 seconds in a single session without reconnects; graceful
exit code was 0. The stale/recovery test passed against live data. No relevant
parameter change occurred during the stable live test, so switch-change and
simulator-disconnect behavior was tested using substitute sessions, not by
operating the user's aircraft or closing the simulator. This bounded run does
not establish long-flight stability or diagnose all earlier native failures.

The MCP exposes observations for a client to poll. It does not itself speak,
send notifications to an AI conversation, or implement flight-phase/checklist
rules. The earlier conversation heartbeat remains independent and paused.
