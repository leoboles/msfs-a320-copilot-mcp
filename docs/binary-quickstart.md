# Run the Windows x64 package

For automatic installation in local Codex, use the [GitHub plugin
guide](https://github.com/leoboles/msfs-a320-copilot-mcp/blob/main/docs/plugin-installation.md).
The steps below describe manual ZIP installation.

Extract the entire ZIP to a permanent directory, such as `C:/A320Copilot`, and
keep all files together. The .NET runtime is included; no SDK is needed to run
the executable. Building from source requires a .NET 10 SDK.

## Start with Mock

Register the executable with your local stdio MCP client (replace the path):

```powershell
codex mcp add msfs-a320-copilot-mcp -- "C:/A320Copilot/A320Copilot.Mcp.exe"
```

Restart the client and call `get_capabilities`, `get_aircraft_state` or
`get_mcdu_state`. The supplied `appsettings.json` selects `Mock`. It returns a
fixed fictional cold-and-dark aircraft and blank MCDU; conversation does not
change that scenario. `./A320Copilot.Mcp.exe --demo` prints mock JSON and exits.

## Enable Real

Real aircraft telemetry is implemented through native SimConnect. It requires
Windows x64, a running MSFS with the FlyByWire A320neo/A32NX loaded, and an
official x64 `SimConnect.dll` obtained from your installed simulator/SDK.
The SDK and proprietary DLL are **not included** in this ZIP.

Create `appsettings.Local.json` beside the executable, replacing the example
with the absolute DLL path on your computer:

```json
{
  "Telemetry": { "Mode": "Real" },
  "SimConnect": { "LibraryPath": "C:/your-installed-sdk/SimConnect.dll" },
  "SimBridge": { "McduWebSocketUrl": "ws://localhost:8380/interfaces/v1/mcdu" }
}
```

Restart the MCP process after changing settings. Local settings override the
supplied `appsettings.json`; `A320COPILOT_` environment variables override files;
`--mock` or `--real` overrides the mode. Paths resolve independently of the MCP
client's working directory. Local development settings are excluded from packages.

For the left MCDU, install and start FlyByWire SimBridge separately, enable the
aircraft's SimBridge connection, and check the remote screen at
`http://localhost:8380/interfaces/mcdu`. SimBridge is not needed for aircraft
SimConnect telemetry. The two connections can succeed or fail independently.

## Read capabilities and quality

`get_capabilities` works without the simulator and lists the 58 implemented
system fields, variables, units, meaning and validation limits. The bundled
`docs/capabilities.md` explains the contract. Read `Source`, `Freshness` and each
parameter's `Quality` before interpreting values. All current system mappings
are `unvalidated`: a numeric zero does not establish that a variable exists or
that a control is OFF. `unavailable` fields have `Value=null`. Other aircraft,
including the FlyByWire A380X, do not receive usable A32NX system values.

Stale/disconnected state produces an error. Real failures never return mock
data. Battery AUTO is selected mode, not bus supply; pump selection is not
pressure; center fuel controls report transfer valves; a pressed fire-test
button is not a successful test. No exact FlyByWire version has full cockpit
validation recorded. Dated successful reads are described in
`docs/local-validation.md`, not a compatibility guarantee.

The MCP reads aircraft state and the left MCDU, maintains connections while its
process runs, exposes status/events, and stores checklist progress. It sends no
aircraft commands or MCDU keypresses. Checklist telemetry confirmation requires
`known` field quality; current unvalidated mappings require explicit user
confirmation. Historical progress does not certify the current aircraft state.

## Diagnose a Real failure

- Call `get_monitor_status` for SimConnect or `get_mcdu_status` for SimBridge;
  inspect `ConnectionState`, `Validity`, age and `LastError`.
- Missing DLL/wrong architecture: check the absolute path and official x64 DLL.
- No aircraft sample: check MSFS is running with the intended aircraft loaded.
- No MCDU screen: check SimBridge, its URL and the aircraft connection; compare
  the remote MCDU page with the cockpit.
- Stale data: wait for fresh reception or inspect reconnection status. An error
  does not prove a button is OFF or that the simulator is stopped.

For an explicit local integration test, use the scripts included in this ZIP:

```powershell
pwsh -File ./scripts/Test-Mcp.ps1 -ExecutablePath ./A320Copilot.Mcp.exe -Mode Mock
pwsh -File ./scripts/Test-Mcp.ps1 -ExecutablePath ./A320Copilot.Mcp.exe -Mode Real -AircraftOnly
pwsh -File ./scripts/Test-Mcp.ps1 -ExecutablePath ./A320Copilot.Mcp.exe -Mode Real
```

Real requires the live services and fails if unavailable. Each smoke test starts
and stops its own MCP process; use a persistent client for conversation.
Hosted CI runs deterministic Mock/Settings tests without a simulator.

See bundled `docs/simconnect.md`, `docs/simbridge.md`, `docs/monitoring.md` and
`docs/checklists.md` for settings and limits. For flight simulation only.
