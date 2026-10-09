param()
. (Join-Path $PSScriptRoot 'Runtime.ps1')
try {
    $pluginHome = Get-PluginHome
    $executable = Install-PluginRuntime -PluginHome $pluginHome
    $settings = Get-PluginSettings -PluginHome $pluginHome
    if (-not $env:A320COPILOT_Telemetry__Mode) { $env:A320COPILOT_Telemetry__Mode = $settings.Mode }
    if (-not $env:A320COPILOT_SimConnect__LibraryPath) { $env:A320COPILOT_SimConnect__LibraryPath = $settings.SimConnectLibraryPath }
    if (-not $env:A320COPILOT_SimBridge__McduWebSocketUrl) { $env:A320COPILOT_SimBridge__McduWebSocketUrl = $settings.McduWebSocketUrl }
    Add-Type -Path (Join-Path $PSScriptRoot 'StdioLauncher.cs')
    exit ([A320PluginStdio]::Run($executable))
} catch {
    [Console]::Error.WriteLine("A320 Copilot startup failed: $($_.Exception.Message)")
    exit 1
}
