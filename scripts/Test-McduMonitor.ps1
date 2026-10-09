param(
    [string]$Dotnet = 'dotnet',
    [ValidateSet('Real', 'Mock')] [string]$Mode = 'Real',
    [ValidateRange(2, 120)] [int]$DurationSeconds = 20,
    [switch]$VerifyStaleHandling,
    [string]$OutputPath = (Join-Path $PSScriptRoot '../artifacts/mcdu-monitor-validation.json')
)
$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path (Join-Path $PSScriptRoot '../src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll')).Path
$info = [System.Diagnostics.ProcessStartInfo]::new()
$info.FileName = $Dotnet
$info.ArgumentList.Add($dll)
$info.ArgumentList.Add('--' + $Mode.ToLowerInvariant())
if ($VerifyStaleHandling) {
    if ($Mode -ne 'Real') { throw 'Stale verification requires Real mode' }
    $info.Environment['A320COPILOT_SimBridge__MaximumScreenAgeMilliseconds'] = '100'
}
$info.UseShellExecute = $false
$info.CreateNoWindow = $true
$info.RedirectStandardInput = $true
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($info)
$stderr = $process.StandardError.ReadToEndAsync()
$requestId = 0
$calls = [System.Collections.Generic.List[object]]::new()
$statuses = [System.Collections.Generic.List[object]]::new()
$pages = [System.Collections.Generic.List[object]]::new()
function Request($method, $parameters) {
    $script:requestId++
    $request = @{jsonrpc='2.0';id=$script:requestId;method=$method;params=$parameters} | ConvertTo-Json -Depth 20 -Compress
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $process.StandardInput.WriteLine($request)
    $process.StandardInput.Flush()
    do {
        $line = $process.StandardOutput.ReadLineAsync()
        if (-not $line.Wait(15000)) { throw 'MCP response timeout' }
        if (-not $line.Result) { throw 'MCP closed stdout' }
        $response = $line.Result | ConvertFrom-Json
    } while ($null -eq $response.id)
    $watch.Stop()
    if ($response.id -ne $script:requestId) { throw 'Unexpected MCP response ID' }
    if ($response.error) { throw ($response.error | ConvertTo-Json -Compress) }
    return [pscustomobject]@{Result=$response.result;Milliseconds=$watch.Elapsed.TotalMilliseconds}
}
function Tool($name, $arguments = @{}) {
    $reply = Request 'tools/call' @{name=$name;arguments=$arguments}
    if ($reply.Result.isError) { throw $reply.Result.content[0].text }
    $json = $reply.Result.content[0].text
    $calls.Add([pscustomobject]@{Tool=$name;Milliseconds=$reply.Milliseconds;ResponseBytes=[Text.Encoding]::UTF8.GetByteCount($json)})
    return ($json | ConvertFrom-Json)
}
function Wait-FreshScreen {
    $wait = [System.Diagnostics.Stopwatch]::StartNew()
    while ((Tool 'get_mcdu_status').Validity -ne 'fresh') {
        if ($wait.Elapsed.TotalSeconds -gt 12) { throw 'No fresh MCDU screen received' }
        Start-Sleep -Milliseconds 10
    }
}
try {
    $hello = Request 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='mcdu-monitor-test';version='1.0'}}
    if (-not $hello.Result.serverInfo) { throw 'Missing MCP server info' }
    $process.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $process.StandardInput.Flush()
    $tools = Request 'tools/list' @{}
    foreach ($name in @('get_mcdu_state','get_mcdu_status','get_recent_mcdu_events','get_aircraft_state','get_monitor_status','get_recent_events')) {
        if ($name -notin $tools.Result.tools.name) { throw "Missing tool: $name" }
    }
    if ($VerifyStaleHandling) { Wait-FreshScreen }
    $firstScreen = Tool 'get_mcdu_state'
    $first = Tool 'get_mcdu_status'
    $statuses.Add($first)
    if ($Mode -eq 'Real' -and ($firstScreen.Source -ne 'simbridge' -or $firstScreen.IsMock -or $firstScreen.Freshness.Validity -ne 'fresh')) {
        throw 'Expected a fresh real MCDU screen'
    }
    $staleStatus = $null
    $staleReadRejected = $false
    if ($VerifyStaleHandling) {
        $wait = [System.Diagnostics.Stopwatch]::StartNew()
        do {
            Start-Sleep -Milliseconds 10
            $staleStatus = Tool 'get_mcdu_status'
            if ($wait.Elapsed.TotalSeconds -gt 4) { throw 'Expected a stale MCDU screen under the 100 ms threshold' }
        } while ($staleStatus.Validity -ne 'stale')
        $rejected = Request 'tools/call' @{name='get_mcdu_state';arguments=@{}}
        if (-not $rejected.Result.isError -or $rejected.Result.content[0].text -notlike '*stale*') { throw 'MCP did not reject a stale screen' }
        $staleReadRejected = $true
    }
    $cursor = 0L
    $emptyPages = 0
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        $status = Tool 'get_mcdu_status'
        $statuses.Add($status)
        if ($Mode -eq 'Real') {
            if ($status.MonitorId -ne $first.MonitorId -or $status.SessionId -ne $first.SessionId -or $status.ConnectionAttempts -ne 1) {
                throw 'Unexpected MCDU monitor replacement or reconnect during live stability test'
            }
            if (-not $VerifyStaleHandling -and $status.Validity -ne 'fresh') { throw ("Invalid MCDU screen: " + ($status | ConvertTo-Json -Compress)) }
        } elseif ($status.Running) { throw 'Mock started the real SimBridge connection' }
        $page = Tool 'get_recent_mcdu_events' @{afterSequence=$cursor;limit=200}
        $pages.Add($page)
        if ($Mode -eq 'Real') {
            if ($page.HistoryTruncated) { throw 'Unexpected MCDU event history gap' }
            if (@($page.Events | Where-Object Sequence -LE $cursor).Count -gt 0) { throw 'Repeated event sequence' }
            $cursor = $page.NextAfterSequence
        }
        if (@($page.Events).Count -eq 0) { $emptyPages++ }
        if (-not $VerifyStaleHandling) { $null = Tool 'get_mcdu_state' }
        Start-Sleep -Milliseconds $(if ($VerifyStaleHandling) { 50 } else { 500 })
    } while ($timer.Elapsed.TotalSeconds -lt $DurationSeconds)
    if ($VerifyStaleHandling) { Wait-FreshScreen }
    $lastScreen = Tool 'get_mcdu_state'
    $last = Tool 'get_mcdu_status'
    $statuses.Add($last)
    if ($Mode -eq 'Real' -and $last.ScreensReceived -le $first.ScreensReceived) { throw 'No background MCDU screen refreshes' }
    $finalEvents = Tool 'get_recent_mcdu_events' @{afterSequence=0;limit=200}
    if ($VerifyStaleHandling -and ('data_stale' -notin $finalEvents.Events.Type -or 'data_fresh' -notin $finalEvents.Events.Type)) {
        throw 'Missing MCDU freshness transition events'
    }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(5000)) { throw 'MCP did not shut down cleanly' }
    if ($process.ExitCode -ne 0) { throw "MCP exit code: $($process.ExitCode)" }
    $latencies = @($calls | Group-Object Tool | ForEach-Object {
        $ordered = @($_.Group.Milliseconds | Sort-Object)
        [pscustomobject]@{Tool=$_.Name;Count=$ordered.Count;MedianMs=[Math]::Round(($ordered[[int][Math]::Floor(($ordered.Count-1)/2)]+$ordered[[int][Math]::Ceiling(($ordered.Count-1)/2)])/2,2);P95Ms=[Math]::Round($ordered[[int][Math]::Ceiling(0.95*$ordered.Count)-1],2);MaxMs=[Math]::Round($ordered[-1],2)}
    })
    $report = [ordered]@{
        Passed=$true;Mode=$Mode;MeasuredAtUtc=[DateTimeOffset]::UtcNow.ToString('o');DurationSeconds=$timer.Elapsed.TotalSeconds
        FirstStatus=$first;LastStatus=$last;FirstScreen=$firstScreen;LastScreen=$lastScreen
        Latencies=$latencies;Calls=$calls;Statuses=$statuses;EventPages=$pages;FinalEvents=$finalEvents
        EmptyEventPages=$emptyPages;StaleStatus=$staleStatus;StaleReadRejected=$staleReadRejected;GracefulExitCode=$process.ExitCode
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $OutputPath) | Out-Null
    $report | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $OutputPath
    [pscustomobject]@{Passed=$true;Mode=$Mode;DurationSeconds=$timer.Elapsed.TotalSeconds;FirstStatus=$first;LastStatus=$last;Latencies=$latencies;EmptyEventPages=$emptyPages;StaleReadRejected=$staleReadRejected;GracefulExitCode=$process.ExitCode;Report=$OutputPath} | ConvertTo-Json -Depth 10
} finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $errorLog = $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) { Write-Warning "MCP exit code $($process.ExitCode): $errorLog" }
    $process.Dispose()
}
