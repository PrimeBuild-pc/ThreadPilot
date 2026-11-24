param(
    [ValidatePattern('^v?[0-9][0-9A-Za-z.+-]*$')]
    [string]$Version = "1.7.11",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

$outputParent = Join-Path $projectRoot "Installer\Output"
$outputRoot = Join-Path $outputParent ("v" + $Version.TrimStart('v'))
$publishDir = Join-Path $outputRoot "publish"
Write-Host "Preparing output directories..."
if (Test-Path -LiteralPath $outputRoot) {
    $resolved = (Resolve-Path -LiteralPath $outputRoot).ProviderPath
    if (!(Split-Path -Parent $resolved).Equals($outputParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected build output directory: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Publishing ThreadPilot $Version ($Configuration, $Runtime)..."
dotnet publish "ThreadPilot.csproj" `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    /p:PublishTrimmed=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "ThreadPilot publish failed (exit code $LASTEXITCODE)."
}

& (Join-Path $PSScriptRoot "package-release-zips.ps1") -Version $Version -PublishDirectory $publishDir -OutputDirectory $outputRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "portable-uninstall.bat") -Destination (Join-Path $outputRoot "uninstall.bat") -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination (Join-Path $outputRoot "LICENSE.md") -Force
