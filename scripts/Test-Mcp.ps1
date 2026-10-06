param([string]$Dotnet = "dotnet")
$ErrorActionPreference = "Stop"
$dll = Join-Path $PSScriptRoot "../src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll"
$info = [System.Diagnostics.ProcessStartInfo]::new()
$info.FileName = $Dotnet
$info.ArgumentList.Add((Resolve-Path $dll).Path)
$info.ArgumentList.Add("--mock")
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
    $call = Send-Request @{ jsonrpc="2.0"; id=3; method="tools/call"; params=@{
        name="get_aircraft_state"; arguments=@{} } }
    if ($call.result.isError) { throw "Tool failed" }
    $state = $call.result.content[0].text | ConvertFrom-Json
    if ($state.Source -ne "mock" -or $state.SimulatorConnected -or
        $state.Battery1On -or $state.Battery2On -or $state.ApuRunning -or
        $state.Engine1Running -or $state.Engine2Running -or
        $state.Scenario -ne "cold_and_dark_in_hangar") { throw "Unexpected state" }
    Write-Output "MCP initialize, tools/list and tools/call passed."
} finally {
    if (-not $process.HasExited) { $process.Kill($true) }
    $process.Dispose()
}
