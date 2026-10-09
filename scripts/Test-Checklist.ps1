param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$dll = (Resolve-Path "$PSScriptRoot/../src/A320Copilot.Mcp/bin/Release/net10.0/A320Copilot.Mcp.dll").Path
$storage = Join-Path $PSScriptRoot ('../artifacts/checklist-validation-' + [guid]::NewGuid().ToString('N'))
$script:requestId = 0
function Start-Client {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Dotnet
    $info.ArgumentList.Add($dll)
    $info.ArgumentList.Add('--real')
    $info.Environment['A320COPILOT_Checklists__StoragePath'] = [IO.Path]::GetFullPath($storage)
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.CreateNoWindow = $true
    $script:client = [Diagnostics.Process]::Start($info)
    $null = Request 'initialize' @{protocolVersion='2024-11-05';capabilities=@{};clientInfo=@{name='checklist-validation';version='1.0'}}
    $client.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
    $client.StandardInput.Flush()
}
function Stop-Client {
    if ($script:client) {
        if (-not $client.HasExited) { $client.Kill($true); $client.WaitForExit() }
        $client.Dispose()
        $script:client = $null
    }
}
function Request($method, $params) {
    $script:requestId++
    $client.StandardInput.WriteLine((@{jsonrpc='2.0';id=$requestId;method=$method;params=$params} | ConvertTo-Json -Depth 30 -Compress))
    $client.StandardInput.Flush()
    $pending = $client.StandardOutput.ReadLineAsync()
    if (-not $pending.Wait(15000)) { throw 'MCP response timeout' }
    $response = $pending.Result | ConvertFrom-Json
    if ($response.error) { throw ($response.error | ConvertTo-Json -Compress) }
    return $response.result
}
function Call($name, $arguments = @{}) {
    $result = Request 'tools/call' @{name=$name;arguments=$arguments}
    if ($result.isError) { throw $result.content[0].text }
    return ($result.content[0].text | ConvertFrom-Json)
}
try {
    Start-Client
    $templates = Call 'list_checklist_templates'
    if (@($templates).Count -ne 6) { throw 'Missing checklist templates' }
    $aircraft = Call 'get_aircraft_state'
    if ($aircraft.Source -ne 'real' -or $aircraft.Freshness.Validity -ne 'fresh') { throw 'Expected fresh real telemetry' }
    $fuel = $aircraft.State.Systems.Fuel
    $controls = $aircraft.State.Systems.Controls
    if (@($fuel.PSObject.Properties).Count -ne 12 -or $null -eq $controls.ParkingBrakeLever) { throw 'Missing new telemetry fields' }
    $mcdu = Call 'get_mcdu_state'
    if ($mcdu.Source -ne 'simbridge' -or $mcdu.IsMock -or $mcdu.Freshness.Validity -ne 'fresh') { throw 'Expected real SimBridge screen' }
    $started = Call 'start_checklist' @{templateId='before_start';flightLabel='AUTOMATED TEST ONLY - not actual flight progress'}
    $id = $started.Session.Id
    $advanced = Call 'update_checklist_item' @{sessionId=$id;expectedRevision=0;itemId='thrust_idle';status='skipped';source='user';note='Explicit test-only skip authorized by test execution; not a cockpit confirmation'}
    if ($advanced.NextItem.Id -ne 'fuel_left1' -or $advanced.AllItemsConfirmed) { throw 'Invalid skip semantics' }
    $switch = $fuel.Left1Switch.Value
    $attempt = Request 'tools/call' @{name='update_checklist_item';arguments=@{sessionId=$id;expectedRevision=1;itemId='fuel_left1';status='confirmed';source='telemetry';note='Test-only check of current reported switch'}}
    if ($switch -eq 1) {
        if ($attempt.isError) { throw $attempt.content[0].text }
        $progress = $attempt.content[0].text | ConvertFrom-Json
        if ($progress.Session.Entries[1].Evidence.Source -ne 'telemetry') { throw 'Missing telemetry evidence' }
    } else {
        if (-not $attempt.isError) { throw 'Mismatch was incorrectly confirmed' }
    }
    $before = Call 'get_checklist' @{sessionId=$id}
    $conflict = Request 'tools/call' @{name='update_checklist_item';arguments=@{sessionId=$id;expectedRevision=0;itemId='thrust_idle';status='pending';source='user';note='test'}}
    if (-not $conflict.isError) { throw 'Revision conflict was accepted' }
    Stop-Client
    Start-Client
    $resumed = Call 'get_checklist' @{sessionId=$id}
    if ($resumed.Session.Revision -ne $before.Session.Revision -or $resumed.NextItem.Id -ne $before.NextItem.Id) { throw 'Progress did not survive server restart' }
    $summary = @{Timestamp=[DateTimeOffset]::Now;Passed=$true;ChecklistStorage=[IO.Path]::GetFullPath($storage);Aircraft=$aircraft;Mcdu=$mcdu;ResumedChecklist=$resumed}
    $report = Join-Path $PSScriptRoot '../artifacts/checklist-live-validation.json'
    $summary | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $report -Encoding utf8
    Write-Output 'PASS: live fuel/controls, real SimBridge, six templates, skip semantics, telemetry confirmation/mismatch rejection, revision conflict and persistence across MCP restart.'
    Write-Output ($fuel | ConvertTo-Json -Depth 5 -Compress)
    Write-Output ($controls | ConvertTo-Json -Depth 5 -Compress)
} finally { Stop-Client }
