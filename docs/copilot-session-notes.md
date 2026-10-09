# Simulator copilot session notes

## Fire tests before APU start — user preference, 2026-10-08

For the next FlyByWire A320 preparation, explicitly include the APU and both
engine fire tests after electrical power is established and the flight warning
system has finished initializing, before instructing the user to start the APU.
The user operates all controls; guide and confirm one item at a time.

The official beginner guide explicitly places the APU fire test before APU
start. It lists engine fire tests later in cockpit preparation; grouping both
engine tests before APU start is the user's requested simulator-session order,
not a claim that this exact order is a universal airline SOP.

The current MCP does not read fire-test completion or all expected indications.
Require the user's visual/aural confirmation; do not infer successful tests from
APU availability, absence of faults or previous-session readings.

In the session on 2026-10-08, APU start was already reported before the user
pointed out the omission. Neither fire test was confirmed and neither should be
marked complete retroactively.

Reference: [FlyByWire — Starting the Aircraft](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/starting-the-aircraft/).

## Fuel pump check and missing MCP telemetry — 2026-10-08

Do not skip the six fuel-pump checks in before-start preparation. The current
MCP does not expose those states. The user requested implementing this feature;
it is recorded as pending in [the implementation backlog](implementation-backlog.md).

The user confirmed all six pumps ON in this session. This is visual user
confirmation, not MCP verification and not evidence of the next session's state.
