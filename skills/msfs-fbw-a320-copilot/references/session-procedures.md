# Preparation, start and ground-operation guidance

These notes address demonstrated gaps in the simulator session of 2026-10-08. Use the current official FlyByWire procedure and the user's actual phase; they are not a replacement airline SOP or a complete checklist. Do not reuse that session's switch confirmations in a new flight.

## Checklist continuity

Maintain a small phase/checklist ledger: item, confirmed state, evidence (live telemetry or user), and unresolved checks. Pause the sequence for questions, answer them, then resume the correct next item. If a prerequisite was skipped, acknowledge it and assess the current state; never mark it complete retroactively or blindly repeat a start already performed. Being connected to telemetry is not a readiness check.

## Preparation and engine start

- Include parking brake, thrust levers IDLE, engine masters OFF/mode NORM and the relevant cockpit preparation checks before directing a start. All six fuel-panel commands must be checked in before-start preparation; do not assume ON. The MCP now reads four wing pumps and two center transfer-valve controls. Distinguish the selected command, reported pump active state and valve opening ratio; pressure/FAULT still require other evidence.
- After electrical power and flight-warning-system initialization, include the APU fire test before APU start. The user specifically requested both engine fire tests also before APU start in future simulator sessions. Preserve that preference while identifying it as the user's sequence: the official beginner guide lists engine fire tests later in preparation. For each engine fire test, the reviewed FBW guide specifies holding TEST at least five seconds. Require the expected visual/aural confirmation; no faults or later APU availability cannot prove a test occurred.
- Establish APU availability/bleed or another applicable starting supply, and the guide's before-start/pushback prerequisites. Do not prescribe packs OFF as a universal step; follow the applicable FBW procedure/configuration.
- In normal automatic start, ENG MODE IGN/START and the relevant ENG MASTER ON initiate FADEC control. Do not instruct manual fuel injection at an N2 threshold. A user saying “start N1” may mean engine 1: distinguish engine number from N1/N2 spool readings.
- Verify the engine-start order in the current guide/SOP. The guide reviewed in this session notes engine 1 first under the updated Airbus sequence; operator procedures may differ. If engine 2 is already running, assess the state rather than requiring a restart to change order.
- Monitor available N1/N2/EGT/fuel-flow values without treating a single N2 value or an example idle EGT as definitive completion. Ask for ECAM warnings/availability if unreadable. The engine display is the upper central ECAM (E/WD), engine 1 left and engine 2 right. ENG, BLEED, PRESS and ELEC buttons select lower-display system pages; they are not needed to locate upper-display engine indications. Absence of a visible AVAIL label alone does not establish a fault; consult the guide if its behavior is in question.
- Follow the after-start sequence, including ENG MODE NORM and the required bleed, APU, packs, flight-controls, trim and configuration checks. APU Master OFF may initiate shutdown/cooldown; do not immediately declare the APU stopped.

## Pushback and taxi

- Distinguish IFR clearance, pushback/service coordination and taxi clearance. Ask which ATC service is being used when unclear. Do not say “only clearance remains” until the applicable checklist and ground-service checks are actually complete.
- Confirm doors, removed/disconnected ground equipment, tug connection and ground crew readiness as applicable. Panel EXT PWR OFF does not establish cable removal. If engines were already started before pushback, account for that state and the simulated ground service; do not infer blanket readiness or automatically command shutdown.
- Keep parking brake set until the connected ground service requests release. Explicitly include release for pushback, then reapplication when instructed at completion before tug disconnection. Do not omit this handoff or claim the MCP read the brake when it cannot.
- Before taxi, verify the remaining after-start/taxi items, clear area and applicable clearance. Do not instruct gear retraction on the ground. Do not default the transponder to STBY for taxi; verify the appropriate ATC/altitude-reporting/TCAS settings for the phase and service, distinguishing those controls.
- Locate exterior lights on overhead EXT LT. The guide uses RWY TURN OFF lights ON and NOSE in TAXI before moving; NOSE T.O. is a different setting. Give one explicit control at a time. Lighting selection itself does not authorize movement.

## MSFS 2024 ATC and route filing

The MSFS EFB/flight planner, the FlyByWire flyPad and the MCDU are distinct systems. A route in the MCDU does not prove the built-in ATC knows it. For built-in ATC, check the intended route in the MSFS EFB and use its ATC filing action (labels/layout depend on version), then request the appropriate clearance in the ATC communication window. Do not tell the user to “send the plan” from that window. Do not overwrite aircraft avionics unnecessarily just to file with ATC.

When the visible options are unknown, use available screen access or ask which options appear; do not invent a menu item. Keep departure/destination, runway, procedure and altitude consistent with the actual clearance and flight plan. A cleared cruise level is not automatically the initial cleared altitude; clarify the distinction before directing an FCU altitude change. Do not interpret unclear spoken SID/STAR names as validated entries.

## Official references

- [FlyByWire cockpit preparation](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/starting-the-aircraft/)
- [FlyByWire engine start and taxi](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-beginner-guide/engine-start-taxi/)
- [FlyByWire engine controls](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-briefing/flight-deck/pedestal/engine/)
- [FlyByWire exterior lights](https://docs.flybywiresim.com/pilots-corner/a32nx/a32nx-briefing/flight-deck/ovhd/ext-lt/)
- [Microsoft EFB introduction](https://flightsimulator.zendesk.com/hc/en-us/articles/16959472213916-Electronic-Flight-Bag-EFB-introduction)

Consult the relevant source when an exact procedural or UI detail is uncertain; avoid repeating research for an already established step in the same session.

