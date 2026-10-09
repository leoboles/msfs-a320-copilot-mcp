# Exercise the real packaged executable through the Windows PowerShell MCP launcher.
# Only the isolated fixture's hash changes; production always pins its published release.
param([Parameter(Mandatory)] [string]$ArchivePath)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = Join-Path $root ('artifacts/plugin-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$fixtureScripts = Join-Path $testRoot 'scripts'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin') -Destination $fixtureScripts -Recurse
$releasePath = Join-Path $fixtureScripts 'release.json'
$release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
$release.sha256 = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$release | ConvertTo-Json | Set-Content -LiteralPath $releasePath -Encoding UTF8
$savedEnvironment = @{}
foreach ($key in @('A320COPILOT_PLUGIN_HOME', 'A320COPILOT_Telemetry__Mode', 'A320COPILOT_SimConnect__LibraryPath', 'A320COPILOT_SimBridge__McduWebSocketUrl')) {
    $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key)
    [Environment]::SetEnvironmentVariable($key, $null)
}
try {
    $env:A320COPILOT_PLUGIN_HOME = Join-Path $testRoot 'user profile with spaces'
    & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -Mode Mock -ArchivePath $ArchivePath | Out-Null
    & (Join-Path $PSScriptRoot 'Test-Mcp.ps1') -PluginLauncher (Join-Path $fixtureScripts 'Start-Plugin.ps1') -Mode Mock
    & (Join-Path $PSScriptRoot 'Test-Mcp.ps1') -PluginLauncher (Join-Path $fixtureScripts 'Start-Plugin.ps1') -Mode Settings
} finally {
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnvironment[$key]) }
}
Write-Output 'PASS: packaged runtime through Windows PowerShell plugin launcher, Mock/Settings and graceful stdin shutdown.'
