# MSFS A320 Copilot MCP

A .NET 10 foundation for a virtual copilot for the FlyByWire A320 in Microsoft Flight Simulator 2024.

## Current status

The solution builds without MSFS or the SimConnect SDK. It includes a stdio MCP server, the `get_aircraft_state` tool, a fictional powered-off A320 hangar scenario, and a placeholder for a future SimConnect adapter.

**General SimConnect telemetry, checklists, AI integration, and Winwing/WinControl integration are not implemented.**

An initial SimBridge integration reads the left MCDU screen through `get_mcdu_state`. It has not been validated against a live simulator. See [setup and transfer instructions](docs/simbridge.md).

The MCP server selects its data source through `Telemetry:Mode` in `appsettings.json`. The default is `Mock`.

## Requirements

- A stable .NET 10 SDK (the runtime alone is insufficient for building).
- For future SimConnect integration: Windows, MSFS 2024, and the official SimConnect SDK.
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
- `Real`: calls the SimConnect adapter for `get_aircraft_state`. **This integration is not implemented**, so the tool returns an explicit error instead of substituting mock data.
- For `get_mcdu_state`, `Real` reads SimBridge and `Mock` returns a fictional blank screen.

The settings file is copied to the build and publish directories. You can also edit `appsettings.json` directly beside the deployed DLL. Restart the MCP process after changing the mode; settings are not reloaded during a session.

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
pwsh -File scripts/Test-Mcp.ps1
```

## Automated releases

Push a version tag on a commit containing the release workflow:

```powershell
git tag v0.2
git push origin v0.2
```

The Release Windows binaries workflow builds and tests the tagged commit,
publishes a self-contained Windows x64 executable, checks its MCP tools in Mock
mode, and creates a GitHub release with a ZIP and SHA-256 checksums.
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
| A320Copilot.Bridge | Telemetry sources, SimBridge MCDU reader, and future SimConnect adapter |
| A320Copilot.Mcp | Stdio MCP server and JSON demo |
| A320Copilot.Tests | Contract and data source behavior tests |

See the [architecture](docs/architecture.md) and [SimConnect integration plan](docs/simconnect.md).

For flight simulation only; not intended for real aircraft operations.
