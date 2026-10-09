# Deterministic installer checks with inert ZIP fixtures; no network, simulator or runtime execution.
param()
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRoot = Join-Path $root ('artifacts/plugin-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$fixtureScripts = Join-Path $testRoot 'scripts'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin') -Destination $fixtureScripts -Recurse
$fixtureFiles = Join-Path $testRoot 'files'
New-Item -ItemType Directory -Path $fixtureFiles | Out-Null
foreach ($name in @('A320Copilot.Mcp.exe', 'A320Copilot.Mcp.dll', 'hostfxr.dll', 'appsettings.json')) {
    Set-Content -LiteralPath (Join-Path $fixtureFiles $name) -Value 'inert fixture, never executed'
}
$archive = Join-Path $testRoot 'runtime.zip'
Compress-Archive -Path (Join-Path $fixtureFiles '*') -DestinationPath $archive
$release = Get-Content -LiteralPath (Join-Path $fixtureScripts 'release.json') -Raw | ConvertFrom-Json
$release.sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$release | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixtureScripts 'release.json') -Encoding UTF8
. (Join-Path $fixtureScripts 'Runtime.ps1')
function Assert($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Failure([scriptblock]$operation, [string]$expected) {
    $failed = $false
    try { & $operation | Out-Null } catch { $failed = $true; Assert ($_.Exception.Message -like "*$expected*") $_.Exception.Message }
    Assert $failed "Expected failure: $expected"
}
$testHome = Join-Path $testRoot 'user profile with spaces'
$executable = Install-PluginRuntime -PluginHome $testHome -ArchivePath $archive
Assert (Test-Path -LiteralPath $executable) 'Runtime was not installed.'
Assert ((Install-PluginRuntime -PluginHome $testHome -ArchivePath (Join-Path $testRoot 'missing.zip')) -eq $executable) 'Repeat install should reuse the runtime without downloading.'
Assert ((Get-PluginSettings -PluginHome $testHome).Mode -eq 'Mock') 'First-run default must be Mock.'
$runtimeDll = Join-Path (Split-Path $executable -Parent) 'A320Copilot.Mcp.dll'
[IO.File]::Move($runtimeDll, "$runtimeDll.saved")
Assert-Failure { Install-PluginRuntime -PluginHome $testHome -ArchivePath $archive } 'Incomplete or different runtime'
[IO.File]::Move("$runtimeDll.saved", $runtimeDll)

$corrupt = Join-Path $testRoot 'corrupt.zip'
Set-Content -LiteralPath $corrupt -Value 'corrupt archive'
$rejectedHome = Join-Path $testRoot 'rejected'
Assert-Failure { Install-PluginRuntime -PluginHome $rejectedHome -ArchivePath $corrupt } 'SHA-256 mismatch'
Assert (-not (Test-Path -LiteralPath (Join-Path $rejectedHome 'versions'))) 'Rejected archive must not be installed.'
Assert (@(Get-ChildItem -LiteralPath $rejectedHome -Filter 'staging-*').Count -eq 0) 'Failed download staging was not cleaned.'
Assert ((Install-PluginRuntime -PluginHome $rejectedHome -ArchivePath $archive) -ne '') 'Retry after failure should release the lock and install.'

$incompleteHome = Join-Path $testRoot 'incomplete'
New-Item -ItemType Directory -Path (Join-Path $incompleteHome "versions/$($release.version)") -Force | Out-Null
Assert-Failure { Install-PluginRuntime -PluginHome $incompleteHome -ArchivePath $archive } 'Incomplete or different runtime'

# Setup settings persist separately from installed versions and are not replaced on launch/reinstall.
$oldPluginHome = $env:A320COPILOT_PLUGIN_HOME
try {
    $env:A320COPILOT_PLUGIN_HOME = $testHome
    & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -Mode Mock -ArchivePath $archive | Out-Null
    $dll = Join-Path $testRoot 'official-path-fixture.dll'
    Set-Content -LiteralPath $dll -Value 'test path only, never loaded'
    & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -Mode Real -SimConnectLibraryPath $dll -McduWebSocketUrl 'ws://localhost:9000/mcdu' | Out-Null
    Install-PluginRuntime -PluginHome $testHome -ArchivePath $archive | Out-Null
    $saved = Get-PluginSettings -PluginHome $testHome
    Assert ($saved.Mode -eq 'Real' -and $saved.SimConnectLibraryPath -eq $dll -and $saved.McduWebSocketUrl -eq 'ws://localhost:9000/mcdu') 'User settings were lost.'
    Assert-Failure { & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -SimConnectLibraryPath (Join-Path $testRoot 'missing.dll') } 'absolute path'
    Assert-Failure { & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -McduWebSocketUrl 'https://localhost' } 'ws://'
    Assert ((Get-PluginSettings -PluginHome $testHome).Mode -eq 'Real') 'Failed setup must preserve prior settings.'
    $env:A320COPILOT_PLUGIN_HOME = Join-Path $testRoot 'no-dll'
    Assert-Failure { & (Join-Path $fixtureScripts 'Setup-Plugin.ps1') -Mode Real } 'Real mode requires'
} finally { $env:A320COPILOT_PLUGIN_HOME = $oldPluginHome }
Write-Output 'PASS: first install, offline reuse, checksum rejection/retry, incomplete install detection, settings preservation and explicit Real prerequisites.'
