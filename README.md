# MSFS A320 Copilot MCP

A .NET 10 foundation for a virtual copilot for the FlyByWire A320 in Microsoft Flight Simulator 2024.

## Current status

The solution builds without MSFS or the SimConnect SDK. It includes a stdio MCP server, the `get_aircraft_state` tool, a fictional powered-off A320 hangar scenario, and a read-only native SimConnect adapter.

Real telemetry reads aircraft title, altitude, indicated airspeed, true heading and on-ground status, plus 18 FlyByWire overhead values, 10 engine values, 12 fuel values and 18 cockpit control values. Fuel distinguishes wing-pump commands/active states from center transfer-valve commands/opening ratios. Controls include parking brake, engine masters/ignition, flaps, spoilers, transponder, exterior lights and momentary fire-test buttons. Each system field includes `Quality` and `QualityReason`; all current mappings are `unvalidated`. Unsupported aircraft/non-finite optional fields have null values and `unavailable` quality. Zero cannot certify an OFF position or variable existence.

`get_capabilities` returns the current field catalog, units, source variables, meaning and validation limits without a simulator connection. See [capabilities and field quality](docs/capabilities.md) and the [Windows package guide](docs/binary-quickstart.md). No exact FlyByWire version has full cockpit validation recorded. Automatic checklist telemetry confirmation requires `known` quality; current mappings require explicit user confirmation.

Versioned simulator training checklists now persist per-session progress, explicit user or fresh telemetry evidence, skipped items and the next pending item. Five tools list templates, create/resume sessions and update progress with revision checks. See [checklist usage and limits](docs/checklists.md). Full airline SOPs, automatic flight-phase/readiness rules, embedded AI and Winwing/WinControl integration are not implemented. No tool controls the aircraft.

Real mode now maintains a persistent SimConnect subscription. `get_aircraft_state` returns the latest valid sample with age/session metadata and rejects stale data. `get_monitor_status` reports connection health; `get_recent_events` exposes a bounded change journal with cursors. See [monitoring behavior and tests](docs/monitoring.md).

The SimBridge integration also maintains a persistent connection. `get_mcdu_state` returns the latest valid left MCDU screen with freshness metadata; `get_mcdu_status` reports its connection and `get_recent_mcdu_events` returns only changed fields and lifecycle events. Unchanged screens generate no screen-change events. Live reads and repeated background refreshes were confirmed locally. See [setup, settings and event polling](docs/simbridge.md) and [local validation](docs/local-validation.md).

The MCP server selects its data source through `Telemetry:Mode` in `appsettings.json`. The default is `Mock`.

## Requirements

- A stable .NET 10 SDK (the runtime alone is insufficient for building).
- For real telemetry: Windows x64, MSFS, and an official x64 `SimConnect.dll`. Configure its absolute path through `SimConnect:LibraryPath`; see [setup](docs/simconnect.md). No proprietary DLL is committed or redistributed.
- NuGet access for the initial test dependency restore.

## Build and test

From the repository root:

```powershell
dotnet restore A320Copilot.slnx
dotnet build A320Copilot.slnx --configuration Release --no-restore
dotnet test A320Copilot.slnx --configuration Release --no-build
dotnet run --project src/A320Copilot.Mcp --configuration Release -- --demo
```

The demo prints synthetic JSON labeled `mock`. To start the stdio MCP server, run the compiled DLL without arguments to use the settings, or use `--mock` / `--real` to override the mode.

The mock scenario is fixed: discussing switching on a battery does not change the data.

## Select the data source

Edit `src/A320Copilot.Mcp/appsettings.json` and rebuild:

```json
{
  "Telemetry": {
    "Mode": "Mock"
  }
}
```

- `Mock`: returns the fictional powered-off aircraft in a hangar.
- `Real`: calls the SimConnect adapter for `get_aircraft_state`. Missing DLL, simulator connection errors and timeouts produce explicit errors without substituting mock data.
- For `get_mcdu_state`, `Real` reads SimBridge and `Mock` returns a fictional blank screen.

The settings file is copied to the build and publish directories. You can also edit `appsettings.json` directly beside the deployed DLL. Restart the MCP process after changing the mode; settings are not reloaded during a session.

An optional `appsettings.Local.json` overrides base settings before environment variables and CLI mode selection. It is ignored by Git and copied to build output, but excluded from publishing. Use it for this computer's SimConnect DLL path.

Precedence, from highest to lowest: `--mock` or `--real`, the `A320COPILOT_Telemetry__Mode` environment variable, .NET host configuration (including `appsettings.json`), and the `Mock` default. An invalid mode exits with code 2. `--demo` always prints the mock scenario and exits.

The file is loaded from the application directory regardless of the client's working directory. Do not register the server with `--mock` if you want the settings file to control the mode.

## Connect to local Codex

After building, replace the example path with the absolute path to your checkout:

```powershell
codex mcp add msfs-a320-copilot-mcp -- dotnet "C:/path/to/msfs-a320-copilot-mcp/src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll"
```

Open a new conversation after registering the server. If it does not appear, restart the application. Example request: "Use the msfs-a320-copilot-mcp server and call get_aircraft_state. Let's practice with the fictional powered-off A320 in the hangar."

The server reserves stdout for MCP messages and sends logs to stderr.

Run the end-to-end smoke test after building:

```powershell
pwsh -File scripts/Test-Mcp.ps1 -Mode Mock
pwsh -File scripts/Test-Mcp.ps1 -Mode Settings
```

## Automated releases

Hosted CI runs build, unit tests and deterministic Mock/Settings smoke tests.
Real integration is an explicit local test with MSFS, an official x64
SimConnect DLL and (for MCDU) SimBridge. It fails when a live connection is absent:

```powershell
pwsh -File scripts/Test-Mcp.ps1 -Mode Real -AircraftOnly
pwsh -File scripts/Test-Mcp.ps1 -Mode Real
```

Push a version tag on a commit containing the release workflow:

```powershell
git tag v0.2
git push origin v0.2
```

The Release Windows binaries workflow builds and tests the tagged commit,
publishes a self-contained Windows x64 executable, checks its MCP tools in Mock
and Settings modes, and creates a GitHub release with a ZIP and SHA-256 checksums.
The ZIP includes `START-HERE.md`, setup/capability documentation and the MCP
smoke script. It excludes the SDK, proprietary SimConnect DLL and local settings.
Tags such as v0.2, v0.2.0, and v0.2.0-beta.1 are supported; versions with a
suffix are published as prereleases. Existing releases are never overwritten.
The existing v0.1 release is unchanged.

The workflow uses the built-in GITHUB_TOKEN; no personal token is required.
You can run it manually from Actions to validate packaging without publishing a release.
For a local package, run `pwsh -File scripts/Package-Release.ps1 -Version v0.2`.
Output is written to artifacts/releases/v0.2. Use a fresh output directory for each run.

## Project structure

| Project | Responsibility |
| --- | --- |
| A320Copilot.Domain | Models and the telemetry contract, without external dependencies |
| A320Copilot.Bridge | Telemetry sources, SimBridge MCDU reader, and native SimConnect adapter |
| A320Copilot.Mcp | Stdio MCP server and JSON demo |
| A320Copilot.Tests | Contract and data source behavior tests |

See the [architecture](docs/architecture.md) and [SimConnect integration plan](docs/simconnect.md).

A [future remote bridge proposal](docs/remote-bridge-proposal.md) describes
possible mobile ChatGPT Chat access through a hosted MCP endpoint. It is a
design proposal; the current implementation remains local.

For flight simulation only; not intended for real aircraft operations.
