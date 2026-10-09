# Install the Codex plugin on Windows

The GitHub repository is a plugin marketplace for **local Windows x64 Codex**.
Installing the plugin registers its MCP launcher and includes the copilot and
setup skills. The first MCP start downloads the pinned self-contained Windows
release, verifies SHA-256, and extracts it automatically. No .NET SDK or manual
ZIP extraction is needed. Git is required to add a GitHub marketplace.

## Install from GitHub

With a Codex version that supports plugin marketplaces, run:

```powershell
codex plugin marketplace add leoboles/msfs-a320-copilot-mcp --ref main
codex plugin add msfs-a320-copilot@leoboles-flight-sim
```

Alternatively, after adding the marketplace, restart the desktop app, open
Plugins, select **Leo Boles Flight Simulation**, and install **MSFS A320 Copilot**.
Run the plugin's setup action or ask: **"Configure o copiloto para meu FlyByWire
A320."** If an older client does not show setup, invoke the packaged
`setup-a320-copilot` skill in the chat. Restart the MCP/client after configuration.

Adding the marketplace only makes the plugin available; the install step enables
it. The binary is downloaded when the installed MCP first starts (or setup runs).
The first start can take up to three minutes. Later starts reuse the installed
version and do not contact GitHub. Merely pasting the repository link into a chat
does not install the plugin.

If you previously registered this MCP manually, remove that old registration
before enabling the plugin to avoid duplicate servers. The plugin manages its
own MCP registration; setup does not run `codex mcp add`.

The plugin starts in **Mock**, a fixed fictional cold-and-dark scenario. Ask for
`get_capabilities` and `get_aircraft_state` to verify the connection. To use Real,
setup needs your installed official x64 `SimConnect.dll` path, MSFS running with
the FlyByWire A320 loaded, and SimBridge for the left MCDU. These dependencies
are not downloaded by the plugin. See [SimConnect](simconnect.md) and
[SimBridge](simbridge.md). Failed Real reads never substitute Mock data.

## Configuration and storage

Setup can also be run from the plugin root (or a repository checkout):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/plugin/Setup-Plugin.ps1 -Mode Mock
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/plugin/Setup-Plugin.ps1 -Mode Real -SimConnectLibraryPath "C:/your-installed-sdk/SimConnect.dll"
```

Use `-McduWebSocketUrl` only to override the default
`ws://localhost:8380/interfaces/v1/mcdu`. Running setup without options preserves
the existing settings. Changing the mode requires restarting the MCP process.

| Data | Location |
| --- | --- |
| Verified runtime | `%LOCALAPPDATA%/A320Copilot/plugin/versions/<version>` |
| Persistent settings | `%LOCALAPPDATA%/A320Copilot/plugin/settings.json` |
| Previous settings | `%LOCALAPPDATA%/A320Copilot/plugin/settings.previous.json` |
| Checklist sessions | `%LOCALAPPDATA%/A320Copilot/checklists` (the MCP default) |

`A320COPILOT_PLUGIN_HOME` may override runtime/settings storage with an absolute
path. Existing `A320COPILOT_Telemetry__Mode`,
`A320COPILOT_SimConnect__LibraryPath` and
`A320COPILOT_SimBridge__McduWebSocketUrl` environment variables take precedence
over plugin settings. `A320COPILOT_Checklists__StoragePath` overrides checklist
storage independently. Settings and history survive plugin updates/removal.

For a failed download, retry after restoring connectivity. A checksum mismatch
stops installation before any downloaded executable runs. An incomplete cached
version produces an explicit error; move only that reported version directory
aside and run setup again. Keep `settings.json` and checklist history.

## Updates and release maintenance

Refresh the marketplace with `codex plugin marketplace upgrade
leoboles-flight-sim`, then update/reinstall the plugin in the client. The plugin
version and runtime version are independent: plugin **0.3.1** currently pins the
published runtime **0.3.0**. It never follows the mutable `latest` release.

To ship a new runtime, first publish and validate its GitHub release. Then update
`scripts/plugin/release.json` with its version, exact asset URL and SHA-256 from
`SHA256SUMS.txt`, and increment `.codex-plugin/plugin.json`'s version. Commit the
catalog/plugin update after the asset exists. Keep old runtime directories so
existing plugin versions continue to work. Do not replace published assets.

The build workflow tests the installer with inert offline ZIPs, including hash
rejection, retries and settings preservation. Release packaging additionally
tests its actual executable through the plugin launcher in Mock and Settings
modes, including clean shutdown when the client closes stdin. Maintainers can
run those checks directly:

```powershell
powershell -NoProfile -File scripts/Test-PluginInstaller.ps1
pwsh -File scripts/Test-PluginPackage.ps1 -ArchivePath "C:/path/to/release.zip"
```

This distribution uses the [Codex plugin/marketplace compatibility
format](https://developers.openai.com/plugins/build/plugins). It is a local stdio
plugin; publishing a GitHub catalog does not submit it to the public ChatGPT
directory or create a hosted MCP endpoint.
