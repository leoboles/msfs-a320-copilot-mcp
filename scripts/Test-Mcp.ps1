param([string]$Dotnet = "dotnet", [ValidateSet("Mock", "Real", "Settings")] [string]$Mode = "Mock",
    [ValidateSet("Debug", "Release")] [string]$Configuration = "Release",
    [string]$ExecutablePath, [switch]$AircraftOnly)
$ErrorActionPreference = "Stop"
$dll = Join-Path $PSScriptRoot "../src/A320Copilot.Mcp/bin/$Configuration/net10.0/A320Copilot.Mcp.dll"
$info = [System.Diagnostics.ProcessStartInfo]::new()
if ($ExecutablePath) {
    $info.FileName = (Resolve-Path $ExecutablePath).Path
} else {
    $info.FileName = $Dotnet
    $info.ArgumentList.Add((Resolve-Path $dll).Path)
}
if ($Mode -ne "Settings") { $info.ArgumentList.Add("--" + $Mode.ToLowerInvariant()) }
$info.UseShellExecute = $false
$info.RedirectStandardInput = $true
$info.RedirectStandardOutput = $true
$info.CreateNoWindow = $true
$process = [System.Diagnostics.Process]::Start($info)
function Send-Request($request) {
    $process.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 20 -Compress))
    $process.StandardInput.Flush()
    $read = $process.StandardOutput.ReadLineAsync()
    if (-not $read.Wait(15000)) { throw "MCP response timed out" }
    if (-not $read.Result) { throw "MCP server closed stdout" }
    $response = $read.Result | ConvertFrom-Json
    if ($response.error) { throw ($response.error | ConvertTo-Json) }
    return $response
}
try {
    $hello = Send-Request @{ jsonrpc="2.0"; id=1; method="initialize"; params=@{
        protocolVersion="2024-11-05"; capabilities=@{}; clientInfo=@{name="smoke-test";version="1.0"} } }
    if (-not $hello.result.serverInfo) { throw "Missing server info" }
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $process.StandardInput.Flush()
    $list = Send-Request @{ jsonrpc="2.0"; id=2; method="tools/list"; params=@{} }
    if ("get_aircraft_state" -notin $list.result.tools.name) { throw "Missing tool" }
    if ("get_mcdu_state" -notin $list.result.tools.name) { throw "Missing MCDU tool" }
    if ("get_capabilities" -notin $list.result.tools.name) { throw "Missing capabilities tool" }
    $catalog = Send-Request @{ jsonrpc="2.0"; id=5; method="tools/call"; params=@{
        name="get_capabilities"; arguments=@{} } }
    if ($catalog.result.isError) { throw "Capabilities failed" }
    $capabilities = $catalog.result.content[0].text | ConvertFrom-Json
    if ($capabilities.ContractVersion -ne 1 -or $capabilities.SystemFields.Count -ne 58 -or
        $capabilities.AircraftControlSupported -or
        @($capabilities.SystemFields | Where-Object { $_.Validation -ne 'unvalidated' }).Count -ne 0) {
        throw "Unexpected capabilities catalog"
    }
    $call = Send-Request @{ jsonrpc="2.0"; id=3; method="tools/call"; params=@{
        name="get_aircraft_state"; arguments=@{} } }
    if ($Mode -eq "Real") {
        if ($call.result.isError) { throw ("Live aircraft read failed: " + $call.result.content[0].text) }
        $aircraft = $call.result.content[0].text | ConvertFrom-Json
        if ($aircraft.Source -ne "real" -or -not $aircraft.SimulatorConnected -or
            [string]::IsNullOrWhiteSpace($aircraft.State.AircraftTitle)) { throw "Unexpected real aircraft state" }
        Write-Output "MCP get_aircraft_state live passed."
        Write-Output ($aircraft | ConvertTo-Json -Depth 20)
        if ($aircraft.State.Systems.Validation -ne 'unsupported_aircraft') {
            if ($null -eq $aircraft.State.Systems.Overhead.Battery1Auto -or
                $null -eq $aircraft.State.Systems.Engines.Engine1N2 -or
                $aircraft.State.Systems.Overhead.Battery1Auto.Quality -ne 'unvalidated') { throw "Missing FlyByWire systems/quality in MCP response" }
            Write-Output "MCP overhead and engine parameters present."
        }
        if ($AircraftOnly) { return }
        $mcdu = Send-Request @{ jsonrpc="2.0"; id=4; method="tools/call"; params=@{
            name="get_mcdu_state"; arguments=@{} } }
        if ($mcdu.result.isError) { throw ("Live MCDU failed: " + $mcdu.result.content[0].text) }
        $screen = $mcdu.result.content[0].text | ConvertFrom-Json
        if ($screen.Source -ne "simbridge" -or $screen.IsMock -or
            $screen.Scope -ne "left_mcdu_screen_only" -or $null -eq $screen.Left.lines) {
            throw "Unexpected live MCDU state"
        }
        Write-Output "MCP get_mcdu_state live passed."
        Write-Output ($screen | ConvertTo-Json -Depth 20)
        return
    }
    if ($call.result.isError) { throw "Tool failed" }
    $state = $call.result.content[0].text | ConvertFrom-Json
    if ($state.Source -ne "mock" -or $state.SimulatorConnected -or
        $state.Battery1On -or $state.Battery2On -or $state.ApuRunning -or
        $state.Engine1Running -or $state.Engine2Running -or
        $state.Scenario -ne "cold_and_dark_in_hangar") { throw "Unexpected state" }
    Write-Output "MCP initialize, tools/list and tools/call passed."
    $mcdu = Send-Request @{ jsonrpc="2.0"; id=4; method="tools/call"; params=@{
        name="get_mcdu_state"; arguments=@{} } }
    if ($mcdu.result.isError) { throw "MCDU mock failed" }
    $screen = $mcdu.result.content[0].text | ConvertFrom-Json
    if ($screen.Source -ne "mock" -or -not $screen.IsMock -or
        $screen.Left.lines.Count -ne 12) { throw "Unexpected mock MCDU" }
    Write-Output "MCP get_mcdu_state mock passed."
} finally {
    if (-not $process.HasExited) { $process.Kill($true) }
    $process.Dispose()
}
