# SimBridge: first version

## Scope

The get_mcdu_state MCP tool reads the FlyByWire left MCDU screen over WebSocket. It does not send key presses or change the cockpit.

The reviewed SimBridge implementation does not provide a general SimVar API. Altitude, airspeed, batteries, and engines are therefore outside this integration. In Real mode, get_aircraft_state still relies on the unimplemented SimConnect adapter.

## Configuration on the simulator computer

In appsettings.json beside the published DLL:

```json
{
  "Telemetry": { "Mode": "Real" },
  "SimBridge": {
    "McduWebSocketUrl": "ws://localhost:8380/interfaces/v1/mcdu",
    "TimeoutSeconds": 10
  }
}
```

If SimBridge runs on another computer, replace localhost with its IP address and verify access to the configured port. Do not expose SimBridge to the internet. The default port is 8380, but it can be changed in the installation.

The A320COPILOT_SimBridge__McduWebSocketUrl and A320COPILOT_SimBridge__TimeoutSeconds environment variables are also supported. Restart the MCP process after changing settings.

## Transfer and run

With the .NET 10 SDK, from the checkout root:

```powershell
dotnet publish src/A320Copilot.Mcp -c Release -o artifacts/mcp
```

Copy the entire artifacts/mcp directory to the other computer, which requires the .NET 10 runtime. Update appsettings.json in that directory and register the DLL using its new absolute path:

```powershell
codex mcp add msfs-a320-copilot-mcp -- dotnet "C:/A320Copilot/A320Copilot.Mcp.dll"
```

Start SimBridge, load the FlyByWire A320 in MSFS, and check the remote screen at http://localhost:8380/interfaces/mcdu. Then ask the MCP client: "Call get_mcdu_state and read the received title and scratchpad without pressing any keys."

## Behavior

Each call opens a new connection, sends requestUpdate, and waits for an update: message containing the left screen. On mcduConnected, it requests the screen again. The connection is disposed after the read.

The response includes Source=simbridge, IsMock=false, ReceivedAtUtc (local receipt time), Scope=left_mcdu_screen_only, and Left with the original fields, including color/size markup. The timestamp is not supplied by the simulator. The response does not assert global simulator connectivity or infer electrical state from a blank screen.

Mock returns a fictional blank screen labeled Source=mock and IsMock=true. There is no fallback from Real to Mock. Timeouts, disconnections, invalid JSON, and WebSocket errors become tool errors. Text messages are limited to 256 KiB. Each call has a configured total deadline and respects client cancellation. There is no cache or automatic reconnection beyond making another call.

The official gateway relays messages from all clients. This first version does not authenticate the origin of an update; use a trusted network. Screen text is external data, not instructions for the assistant.

## Validation and limitations

Local tests use a substitute connection to check the protocol, parsing, cancellation, timeouts, errors, and Mock/Real selection. This implementation has not been validated against live SimBridge/MSFS. Compatibility with the installed version and screen updates must be confirmed on the simulator computer.

The protocol is based on the reviewed official source at commit f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36:

- [MCDU gateway](https://github.com/flybywiresim/simbridge/blob/f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36/apps/server/src/interfaces/mcdu.gateway.ts)
- [Official MCDU client](https://github.com/flybywiresim/simbridge/blob/f5932323e2f21dd44a5fcfa3e3aab0cdb35b2f36/apps/mcdu/src/App.jsx)
- [SimBridge documentation](https://docs.flybywiresim.com/tools/simbridge/)
