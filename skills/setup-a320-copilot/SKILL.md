---
name: setup-a320-copilot
description: Install or configure this plugin's Windows runtime and select Mock or Real telemetry for the FlyByWire A320. Use for first-time setup, changing the SimConnect DLL path, or diagnosing plugin startup.
---

Resolve the plugin root two levels above this SKILL.md. Use its `scripts/plugin/Setup-Plugin.ps1` with Windows PowerShell. Never assume a checkout or SDK path from another machine.

The launcher downloads the version pinned in `scripts/plugin/release.json` on first MCP start, verifies SHA-256, and keeps it in `%LOCALAPPDATA%/A320Copilot/plugin/versions`. Setup reuses that runtime and preserves settings in `%LOCALAPPDATA%/A320Copilot/plugin/settings.json`. `A320COPILOT_PLUGIN_HOME` overrides this storage for tests/custom installs. The .NET runtime is included. Installation does not download the proprietary SimConnect DLL or install MSFS/SimBridge.

For installation only or explicit practice, run setup with `-Mode Mock`. Clearly identify Mock as fictional. For a request to connect the simulator, obtain the user's installed official x64 SimConnect.dll path; reuse a valid supplied/configured path, or ask for it if missing. Run setup with `-Mode Real -SimConnectLibraryPath <absolute-path>`. Preserve the default MCDU URL unless the user has a different SimBridge endpoint; pass `-McduWebSocketUrl` only when needed. Use explicit script parameters, with quoted paths; do not alter machine-wide settings or register a duplicate MCP server with `codex mcp add`.

After setup, explain that the plugin's MCP process needs restarting (or a new chat/client restart), then call its `get_capabilities`. For Real, call `get_aircraft_state` and, when MCDU is needed, `get_mcdu_state`. Check Source, freshness and field quality. Report which source is available; an installation success is not proof of live simulator connectivity. Do not operate the aircraft to make a test pass.

If the installed runtime is incomplete or a download fails, use the error's path/reason. Do not silently fall back from Real to Mock, disable checksum checks or delete checklist history. Settings and checklists survive plugin updates/removal; no cleanup is needed for ordinary setup.
