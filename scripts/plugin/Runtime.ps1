# Shared by the launcher and setup; compatible with Windows PowerShell 5.1.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-PluginHome {
    if ($env:A320COPILOT_PLUGIN_HOME) { $path = $env:A320COPILOT_PLUGIN_HOME }
    else { $path = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'A320Copilot/plugin' }
    if (-not [IO.Path]::IsPathRooted($path)) { throw 'A320COPILOT_PLUGIN_HOME must be an absolute path.' }
    return [IO.Path]::GetFullPath($path)
}

function Assert-PluginPlatform {
    if ([Environment]::OSVersion.Platform -ne 'Win32NT' -or -not [Environment]::Is64BitProcess -or
        $env:PROCESSOR_ARCHITECTURE -ne 'AMD64') { throw 'This plugin requires Windows x64 and 64-bit Windows PowerShell.' }
}

function Get-PluginSettings {
    param([string]$PluginHome)
    $path = Join-Path $PluginHome 'settings.json'
    if (Test-Path -LiteralPath $path) {
        $settings = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        if ($settings.Mode -notin @('Mock', 'Real') -or $null -eq $settings.SimConnectLibraryPath -or
            $null -eq $settings.McduWebSocketUrl) { throw "Invalid settings: $path. Run plugin setup to repair the file." }
        return $settings
    }
    return [pscustomobject]@{ Mode='Mock'; SimConnectLibraryPath=''; McduWebSocketUrl='ws://localhost:8380/interfaces/v1/mcdu' }
}

function Install-PluginRuntime {
    param([string]$PluginHome, [string]$ArchivePath)
    Assert-PluginPlatform
    $release = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'release.json') -Raw | ConvertFrom-Json
    if ($release.version -notmatch '^\d+\.\d+\.\d+$' -or $release.sha256 -notmatch '^[a-f0-9]{64}$' -or
        $release.url -ne "https://github.com/leoboles/msfs-a320-copilot-mcp/releases/download/v$($release.version)/msfs-a320-copilot-mcp-$($release.version)-win-x64.zip") {
        throw 'Invalid pinned release metadata.'
    }
    [IO.Directory]::CreateDirectory($PluginHome) | Out-Null
    # Serialize simultaneous MCP starts/setup. The OS releases the lock after a crash.
    $lock = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(150)
    while ($null -eq $lock) {
        try { $lock = [IO.File]::Open((Join-Path $PluginHome 'install.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) { throw 'Another plugin installation is still running. Retry shortly.' }
            Start-Sleep -Milliseconds 200
        }
    }
    $stage = $null
    try {
        $runtime = Join-Path $PluginHome ("versions/" + $release.version)
        $executable = Join-Path $runtime 'A320Copilot.Mcp.exe'
        $receiptPath = Join-Path $runtime 'install-receipt.json'
        $requiredFiles = @('A320Copilot.Mcp.exe', 'A320Copilot.Mcp.dll', 'hostfxr.dll', 'appsettings.json')
        if (Test-Path -LiteralPath $runtime) {
            $missingFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $runtime $_) -PathType Leaf) })
            if ($missingFiles.Count -eq 0 -and (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
                $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
                if ($receipt.Version -eq $release.version -and $receipt.ArchiveSha256 -eq $release.sha256) { return $executable }
            }
            throw "Incomplete or different runtime at $runtime. Move that version directory aside, then run setup again."
        }
        $stage = Join-Path $PluginHome ('staging-' + [guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($stage) | Out-Null
        $archive = Join-Path $stage 'runtime.zip'
        if ($ArchivePath) { Copy-Item -LiteralPath $ArchivePath -Destination $archive }
        else {
            [Console]::Error.WriteLine("Downloading A320 Copilot $($release.version) from GitHub...")
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $priorProgress = $ProgressPreference
            try {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $release.url -OutFile $archive -UseBasicParsing -TimeoutSec 120
            } finally { $ProgressPreference = $priorProgress }
        }
        $stream = [IO.File]::OpenRead($archive)
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $archiveHash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose(); $hasher.Dispose() }
        if ($archiveHash -ne $release.sha256) {
            throw 'Release SHA-256 mismatch. The downloaded runtime was not installed or executed.'
        }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $unpacked = Join-Path $stage 'unpacked'
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $unpacked)
        foreach ($required in $requiredFiles) {
            if (-not (Test-Path -LiteralPath (Join-Path $unpacked $required) -PathType Leaf)) { throw "Release is missing $required." }
        }
        $receipt = @{ Version=$release.version; ArchiveSha256=$release.sha256 }
        $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $unpacked 'install-receipt.json') -Encoding UTF8
        [IO.Directory]::CreateDirectory((Join-Path $PluginHome 'versions')) | Out-Null
        [IO.Directory]::Move($unpacked, $runtime)
        [Console]::Error.WriteLine("A320 Copilot installed at $runtime. Run plugin setup to enable Real mode.")
        return $executable
    } finally {
        # Only remove our generated staging child, after verifying its resolved boundary.
        try {
            if ($stage -and (Test-Path -LiteralPath $stage)) {
                $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
                $resolvedHome = (Resolve-Path -LiteralPath $PluginHome).Path.TrimEnd('\') + '\'
                if (-not $resolvedStage.StartsWith($resolvedHome, [StringComparison]::OrdinalIgnoreCase) -or
                    (Split-Path $resolvedStage -Leaf) -notmatch '^staging-[a-f0-9]{32}$') { throw 'Unsafe staging cleanup path.' }
                Remove-Item -LiteralPath $resolvedStage -Recurse -Force
            }
        } finally { if ($lock) { $lock.Dispose() } }
    }
}
