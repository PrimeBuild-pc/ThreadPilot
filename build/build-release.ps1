param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

function Remove-IfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location -LiteralPath $projectRoot

$outputRoot = Join-Path $projectRoot ("Installer\Output\v" + $Version)
$publishDir = Join-Path $outputRoot "publish"
$portableStage = Join-Path $outputRoot "portable_stage"
$setupStage = Join-Path $outputRoot "setup_stage"

$portableZip = Join-Path $outputRoot ("ThreadPilot_v" + $Version + "_Portable.zip")
$setupZip = Join-Path $outputRoot ("ThreadPilot_v" + $Version + "_Setup.zip")
$uninstallerPath = Join-Path $outputRoot "uninstaller.bat"

Write-Host "Preparing output directories..."
Remove-IfExists -Path $outputRoot
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

Write-Host "Creating uninstaller script..."
$uninstallerContent = @'
@echo off
setlocal EnableExtensions
title ThreadPilot Uninstaller

set "APP_DIR=%~dp0"
if "%APP_DIR:~-1%"=="\" set "APP_DIR=%APP_DIR:~0,-1%"

echo ======================================================
echo ThreadPilot v1.0.0 Uninstaller
echo ======================================================
echo.
echo App directory: "%APP_DIR%"
echo.

echo [1/4] Closing running ThreadPilot processes...
taskkill /IM "ThreadPilot.exe" /F >nul 2>&1

echo [2/4] Removing startup task and startup registry entry...
schtasks /Delete /TN "ThreadPilot_Startup" /F >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "ThreadPilot" /f >nul 2>&1

echo [3/4] Optional user data cleanup...
set "REMOVE_DATA=N"
set /p REMOVE_DATA=Do you want to remove user settings at "%APPDATA%\ThreadPilot"? [y/N]:
if /I "%REMOVE_DATA%"=="Y" (
    if exist "%APPDATA%\ThreadPilot" (
        rd /s /q "%APPDATA%\ThreadPilot"
        echo User settings removed.
    ) else (
        echo No user settings folder found.
    )
) else (
    echo User settings were kept.
)

echo [4/4] Scheduling app folder removal...
start "" /min cmd /c "timeout /t 5 /nobreak >nul & rd /s /q \"%APP_DIR%\""

echo.
echo Uninstall completed. Remaining files will be removed in a few seconds.
endlocal
exit /b 0
'@

Set-Content -LiteralPath $uninstallerPath -Value $uninstallerContent -Encoding Ascii

Write-Host "Packaging portable archive..."
New-Item -ItemType Directory -Path $portableStage -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $portableStage -Recurse -Force
Remove-IfExists -Path $portableZip
Compress-Archive -Path (Join-Path $portableStage "*") -DestinationPath $portableZip -CompressionLevel Optimal

Write-Host "Packaging setup archive..."
New-Item -ItemType Directory -Path $setupStage -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $setupStage -Recurse -Force
Copy-Item -LiteralPath $uninstallerPath -Destination (Join-Path $setupStage "uninstaller.bat") -Force
Remove-IfExists -Path $setupZip
Compress-Archive -Path (Join-Path $setupStage "*") -DestinationPath $setupZip -CompressionLevel Optimal

Write-Host "Cleaning staging folders..."
Remove-IfExists -Path $portableStage
Remove-IfExists -Path $setupStage

Write-Host "Release packaging complete." -ForegroundColor Green
Write-Host "Portable: $portableZip"
Write-Host "Setup:    $setupZip"
Write-Host "Uninstaller script source: $uninstallerPath"
