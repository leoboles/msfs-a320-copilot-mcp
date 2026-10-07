param(
    [Parameter(Mandatory)] [string]$Version,
    [string]$Dotnet = "dotnet"
)
$ErrorActionPreference = "Stop"
if ($Version -notmatch '^v?(\d+)\.(\d+)(?:\.(\d+))?(-[0-9A-Za-z.-]+)?$') {
    throw "Expected a version such as v0.2 or v0.2.0."
}
$packageVersion = $Version.TrimStart('v')
$buildVersion = "$($Matches[1]).$($Matches[2])."
if ($Matches[3]) { $buildVersion += $Matches[3] } else { $buildVersion += "0" }
$buildVersion += $Matches[4]
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$output = Join-Path $root "artifacts/releases/$Version"
$publish = Join-Path $output "win-x64"
if (Test-Path $output) { throw "Output directory already exists: $output. Use a fresh workspace." }
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $Dotnet publish (Join-Path $root "src/A320Copilot.Mcp") -c Release -r win-x64 --self-contained true --artifacts-path (Join-Path $output "build") -o $publish "-p:Version=$buildVersion" -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
Copy-Item -LiteralPath (Join-Path $root "docs/binary-quickstart.md") -Destination (Join-Path $publish "START-HERE.md")
& (Join-Path $PSScriptRoot "Test-Mcp.ps1") -ExecutablePath (Join-Path $publish "A320Copilot.Mcp.exe")
& (Join-Path $PSScriptRoot "Test-Mcp.ps1") -ExecutablePath (Join-Path $publish "A320Copilot.Mcp.exe") -Mode Settings
$name = "msfs-a320-copilot-mcp-$packageVersion-win-x64.zip"
$zip = Join-Path $output $name
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $output "SHA256SUMS.txt") -Value "$hash  $name" -Encoding utf8NoBOM
Write-Output "Release package: $zip"
