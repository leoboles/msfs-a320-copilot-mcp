param(
    [string]$Dotnet = "dotnet",
    [ValidateSet("Real", "Mock")] [string]$Mode = "Real",
    [ValidateRange(2, 120)] [int]$DurationSeconds = 20,
    [switch]$VerifyStaleHandling,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../artifacts/monitor-validation.json')
)
$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path (Join-Path $PSScriptRoot '../src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll')).Path
$info = [System.Diagnostics.ProcessStartInfo]::new()
$info.FileName = $Dotnet
$info.ArgumentList.Add($dll)
$info.ArgumentList.Add('--' + $Mode.ToLowerInvariant())
if ($VerifyStaleHandling) {
    if ($Mode -ne 'Real') { throw 'Stale verification requires Real mode' }
    $info.Environment['A320COPILOT_SimConnect__MaximumSampleAgeMilliseconds'] = '100'
}
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardInput = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($info)
$stderr = $process.StandardError.ReadToEndAsync()
$requestId = 0
$statuses = [System.Collections.Generic.List[object]]::new()
$pages = [System.Collections.Generic.List[object]]::new()
function Request($method, $parameters) {
    $script:requestId++
    $process.StandardInput.WriteLine((@{jsonrpc='2.0';id=$script:requestId;method=$method;params=$parameters} | ConvertTo-Json -Depth 20 -Compress))
    $process.StandardInput.Flush()
    $line = $process.StandardOutput.ReadLineAsync()
    if (-not $line.Wait(15000)) { throw 'MCP response timeout' }
    if (-not $line.Result) { throw 'MCP process closed stdout' }
    $response = $line.Result | ConvertFrom-Json
    if ($response.error) { throw ($response.error | ConvertTo-Json -Compress) }
    return $response.result
}
function Tool($name, $arguments = @{}) {
    $result = Request 'tools/call' @{name=$name;arguments=$arguments}
    if ($result.isError) { throw $result.content[0].text }
    return ($result.content[0].text | ConvertFrom-Json)
}
try {
    $hello = Request 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='persistent-monitor-test';version='1.0'}}
    if (-not $hello.serverInfo) { throw 'Missing MCP server info' }
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $process.StandardInput.Flush()
    $tools = Request 'tools/list' @{}
    foreach ($name in @('get_aircraft_state','get_monitor_status','get_recent_events','get_mcdu_state')) {
        if ($name -notin $tools.tools.name) { throw "Missing tool: $name" }
    }
    $firstAircraft = Tool 'get_aircraft_state'
    $first = Tool 'get_monitor_status'
    $statuses.Add($first)
    if ($Mode -eq 'Real' -and ($first.Validity -ne 'fresh' -or $firstAircraft.Freshness.Validity -ne 'fresh')) {
        throw 'Expected a fresh real aircraft sample'
    }
    $staleStatus = $null
    $staleReadRejected = $false
    if ($VerifyStaleHandling) {
        # This changes only the child MCP's age threshold, not simulator data or controls.
        Start-Sleep -Milliseconds 300
        $staleStatus = Tool 'get_monitor_status'
        if ($staleStatus.Validity -ne 'stale') { throw 'Expected stale status under the 100 ms test threshold' }
        $staleRead = Request 'tools/call' @{name='get_aircraft_state';arguments=@{}}
        if (-not $staleRead.isError -or $staleRead.content[0].text -notlike '*stale*') {
            throw 'Stale state was not rejected by MCP'
        }
        $staleReadRejected = $true
    }
    $cursor = 0L
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        $status = Tool 'get_monitor_status'
        $statuses.Add($status)
        if ($Mode -eq 'Real') {
            if ($status.MonitorId -ne $first.MonitorId) { throw 'Monitor instance changed inside one MCP process' }
            if ($status.SessionId -ne $first.SessionId) { throw 'Unexpected reconnect during stability test; inspect status/events' }
            if (-not $VerifyStaleHandling -and $status.Validity -ne 'fresh') { throw ("Invalid telemetry during test: " + ($status | ConvertTo-Json -Compress)) }
        } elseif ($status.Running) { throw 'Mock started the real monitor' }
        $page = Tool 'get_recent_events' @{afterSequence=$cursor;limit=200}
        $pages.Add($page)
        if ($Mode -eq 'Real') {
            if ($page.HistoryTruncated) { throw 'Unexpected event history gap' }
            if (@($page.Events | Where-Object Sequence -LE $cursor).Count -ne 0) { throw 'Duplicate event returned' }
            $cursor = $page.NextAfterSequence
        }
        Start-Sleep -Milliseconds $(if ($VerifyStaleHandling) { 50 } else { 1000 })
    } while ($timer.Elapsed.TotalSeconds -lt $DurationSeconds)
    if ($VerifyStaleHandling) {
        $wait = [System.Diagnostics.Stopwatch]::StartNew()
        while ((Tool 'get_monitor_status').Validity -ne 'fresh') {
            if ($wait.Elapsed.TotalSeconds -gt 3) { throw 'Fresh samples did not resume' }
            Start-Sleep -Milliseconds 10
        }
        $allEvents = Tool 'get_recent_events' @{afterSequence=0;limit=200}
        if ('data_stale' -notin $allEvents.Events.Type -or 'data_fresh' -notin $allEvents.Events.Type) {
            throw 'Missing freshness transition events'
        }
        $pages.Add($allEvents)
    }
    $lastAircraft = Tool 'get_aircraft_state'
    $last = Tool 'get_monitor_status'
    $statuses.Add($last)
    if ($Mode -eq 'Real' -and $last.SamplesReceived -le $first.SamplesReceived) { throw 'No background samples arrived between MCP calls' }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(5000)) { throw 'MCP did not shut down cleanly after closing stdin' }
    if ($process.ExitCode -ne 0) { throw "MCP exited with code $($process.ExitCode)" }
    $report = [ordered]@{
        Mode=$Mode;Passed=$true;ProcessId=$process.Id;DurationSeconds=$timer.Elapsed.TotalSeconds
        FirstStatus=$first;LastStatus=$last;Statuses=$statuses;EventPages=$pages
        FirstAircraftResponse=$firstAircraft;LastAircraftResponse=$lastAircraft
        StaleStatus=$staleStatus;StaleReadRejected=$staleReadRejected
        GracefulExitCode=$process.ExitCode
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $OutputPath) | Out-Null
    $report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath
    Write-Output ([pscustomobject]$report | Select-Object Mode,Passed,ProcessId,DurationSeconds,FirstStatus,LastStatus,GracefulExitCode | ConvertTo-Json -Depth 10)
    Write-Output "Report: $OutputPath"
} finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $errorLog = $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { Write-Warning "MCP exit code: $($process.ExitCode). $errorLog" }
    $process.Dispose()
}
