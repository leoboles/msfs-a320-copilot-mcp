# MCP implementation backlog

## Fuel panel telemetry — implemented, requested 2026-10-08

- [x] Read all six fuel-panel switch/command states through the official API mappings: four wing pumps and two center transfer valves (neo jet-pump system).
- [x] Expose Fuel fields through get_aircraft_state with units, variable provenance and the existing freshness metadata.
- [x] Distinguish switch selection from reported wing-pump active state and center-valve opening ratio. Pressure and fault telemetry is not implemented; absence must remain unknown.
- [x] Include fuel/control changes in get_recent_events with the same cursor and reconnection-baseline behavior as other aircraft parameters.
- [ ] Test mappings and error/availability handling, then compare real MCP responses against user-operated cockpit switches one pump at a time.

Acceptance: an AI client can identify the reported state of each of the six
pumps and the age/source of the data, without confusing a commanded ON state
with proof of physical operation or a successful before-start checklist.

Live MCP reads returned all six switch states, four active states and two valve opening ratios. One-switch-at-a-time cockpit cross-check remains pending because this test did not operate aircraft controls. Historical user confirmations must not be reused for another flight.

## Checklist progress and additional cockpit observations — implemented

- [x] Versioned training templates for preparation, before-start, pushback, engine-start, after-start and before-taxi.
- [x] Persisted sessions, next pending item, explicit skipped state, evidence provenance and revision conflict protection.
- [x] Fresh real telemetry confirmation only for mapped items; fire-test success, start completion and clearances require human confirmation.
- [x] Parking brake, engine master/ignition, flaps/spoilers, transponder/ALT RPTG/TCAS, selected lights and fire-test button observations, with change events.
- [x] Correct engine-state metadata: 3=RESTARTING, 4=SHUTTING, verified against current FBW FADEC source.
- [ ] Extend to full phase rules/continuous checklist reassessment, ECAM warnings/AVAIL, ground equipment/doors, ground speed/gear/thrust lever and electrical bus supply using validated mappings. These are remaining capabilities, not values inferred from unrelated telemetry.
