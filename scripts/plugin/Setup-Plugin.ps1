param(
    [ValidateSet('Mock','Real')] [string]$Mode,
    [string]$SimConnectLibraryPath,
    [string]$McduWebSocketUrl,
    [string]$ArchivePath
)
. (Join-Path $PSScriptRoot 'Runtime.ps1')
$pluginHome = Get-PluginHome
$settings = Get-PluginSettings -PluginHome $pluginHome
if ($Mode) { $settings.Mode = $Mode }
if ($PSBoundParameters.ContainsKey('SimConnectLibraryPath')) {
    if (-not [IO.Path]::IsPathRooted($SimConnectLibraryPath) -or
        -not (Test-Path -LiteralPath $SimConnectLibraryPath -PathType Leaf)) { throw 'Supply the absolute path to your installed official x64 SimConnect.dll.' }
    $settings.SimConnectLibraryPath = (Resolve-Path -LiteralPath $SimConnectLibraryPath).Path
}
if ($McduWebSocketUrl) {
    $mcduUri = [uri]$McduWebSocketUrl
    if (-not $mcduUri.IsAbsoluteUri -or $mcduUri.Scheme -notin @('ws','wss')) { throw 'MCDU URL must use ws:// or wss://.' }
    $settings.McduWebSocketUrl = $McduWebSocketUrl
}
if ($settings.Mode -eq 'Real' -and ([string]::IsNullOrWhiteSpace($settings.SimConnectLibraryPath) -or
    -not (Test-Path -LiteralPath $settings.SimConnectLibraryPath -PathType Leaf))) {
    throw 'Real mode requires -SimConnectLibraryPath pointing to your installed official x64 DLL.'
}
$executable = Install-PluginRuntime -PluginHome $pluginHome -ArchivePath $ArchivePath
$settingsPath = Join-Path $pluginHome 'settings.json'
$temporarySettings = Join-Path $pluginHome ('settings-' + [guid]::NewGuid().ToString('N') + '.tmp')
$settings | ConvertTo-Json | Set-Content -LiteralPath $temporarySettings -Encoding UTF8
if (Test-Path -LiteralPath $settingsPath) { [IO.File]::Replace($temporarySettings, $settingsPath, (Join-Path $pluginHome 'settings.previous.json')) }
else { [IO.File]::Move($temporarySettings, $settingsPath) }
[pscustomobject]@{ Executable=$executable; Settings=$settingsPath; Mode=$settings.Mode; RestartRequired=$true } | ConvertTo-Json
