param([string]$Dotnet = "dotnet", [ValidateSet("Mock", "Real", "Settings")] [string]$Mode = "Mock",
    [ValidateSet("Debug", "Release")] [string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$dll = Join-Path $PSScriptRoot "../src/A320Copilot.Mcp/bin/$Configuration/net10.0/A320Copilot.Mcp.dll"
$info = [System.Diagnostics.ProcessStartInfo]::new()
$info.FileName = $Dotnet
$info.ArgumentList.Add((Resolve-Path $dll).Path)
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
    $call = Send-Request @{ jsonrpc="2.0"; id=3; method="tools/call"; params=@{
        name="get_aircraft_state"; arguments=@{} } }
    if ($Mode -eq "Real") {
        if (-not $call.result.isError -or
            $call.result.content[0].text -notlike "*SimConnect integration is not implemented*") {
            throw "Expected explicit real-mode unavailable error"
        }
        Write-Output "Real mode reports unavailable telemetry without mock fallback."
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
