# Run the Windows x64 binaries

The .NET runtime is included. Extract the entire ZIP to a permanent directory,
such as C:/A320Copilot, and keep all files together.

Register the executable with your local MCP client:

```powershell
codex mcp add msfs-a320-copilot-mcp -- "C:/A320Copilot/A320Copilot.Mcp.exe"
```

Restart the client and request get_aircraft_state or get_mcdu_state.
The default appsettings.json selects Mock mode.

To print mock JSON without starting MCP:

```powershell
./A320Copilot.Mcp.exe --demo
```

For SimBridge, set Telemetry:Mode to Real in appsettings.json, configure
SimBridge:McduWebSocketUrl (default ws://localhost:8380/interfaces/v1/mcdu),
and restart the MCP process. Start SimBridge, load the FlyByWire A320,
and call get_mcdu_state.

This integration reads only the left MCDU screen and never presses keys.
Live SimBridge/MSFS compatibility still requires validation.
General SimConnect telemetry is not implemented; get_aircraft_state
returns an explicit error in Real mode without substituting mock data.

See docs/simbridge.md in the repository for full setup instructions.
For flight simulation only; not intended for real aircraft operations.
