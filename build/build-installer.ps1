param(
    [string]$Version = "1.1.2",
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

$publishDir = Join-Path $projectRoot "artifacts\release\singlefile"
$installerOutputDir = Join-Path $projectRoot "artifacts\release\installer"
$installerExe = Join-Path $installerOutputDir ("ThreadPilot_v{0}_Setup.exe" -f $Version)
$publishedExe = Join-Path $publishDir "ThreadPilot.exe"

if (-not $SkipPublish)
{
    Write-Host "Publishing ThreadPilot ($Configuration) to fresh single-file output..."
    dotnet publish "ThreadPilot.csproj" --configuration $Configuration -p:PublishProfile=WinX64-SingleFile
}

if (-not (Test-Path -LiteralPath $publishedExe))
{
    throw "Published executable not found: $publishedExe"
}

if (Test-Path -LiteralPath $installerExe)
{
    Remove-Item -LiteralPath $installerExe -Force
}

$resolvedSourceDir = (Resolve-Path -LiteralPath $publishDir).Path
Write-Host "Building installer from source directory: $resolvedSourceDir"

& iscc.exe "/DMyAppVersion=$Version" "/DMyAppSourceDir=$resolvedSourceDir" "Installer/setup.iss"
if ($LASTEXITCODE -ne 0)
{
    throw "Inno Setup compile failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $installerExe))
{
    throw "Installer not generated at expected path: $installerExe"
}

$publishedInfo = Get-Item -LiteralPath $publishedExe
$installerInfo = Get-Item -LiteralPath $installerExe

if ($installerInfo.LastWriteTimeUtc -lt $publishedInfo.LastWriteTimeUtc)
{
    throw "Installer timestamp is older than the published executable. Build may be stale."
}

Write-Host "Installer build complete." -ForegroundColor Green
Write-Host "Published exe: $publishedExe"
Write-Host "Installer exe: $installerExe"
Write-Host "Published exe timestamp (UTC): $($publishedInfo.LastWriteTimeUtc.ToString('u'))"
Write-Host "Installer timestamp (UTC): $($installerInfo.LastWriteTimeUtc.ToString('u'))"
