param(
    [string]$Version = "1.4.1"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
Set-Location -LiteralPath $projectRoot

$packagesDir = "artifacts/release/packages"
$stageRoot = "artifacts/release/package-stage"
$portableStage = Join-Path $stageRoot "portable"
$singleFileDir = "artifacts/release/singlefile"

Remove-Item -Recurse -Force $packagesDir, $stageRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packagesDir, $portableStage | Out-Null

$uninstallPath = Join-Path $stageRoot "uninstall.bat"
$licensePath = Join-Path $stageRoot "LICENSE.md"

$uninstallContent = @'
@echo off
setlocal EnableExtensions
title ThreadPilot Uninstaller

set "APP_DIR=%~dp0"
if "%APP_DIR:~-1%"=="\" set "APP_DIR=%APP_DIR:~0,-1%"

echo ======================================================
echo ThreadPilot Uninstaller
echo ======================================================
echo.

echo [1/4] Closing running ThreadPilot processes...
taskkill /IM "ThreadPilot.exe" /F >nul 2>&1

echo [2/4] Removing startup task and startup registry entry...
schtasks /Delete /TN "ThreadPilot_Startup" /F >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "ThreadPilot" /f >nul 2>&1

echo [3/4] Removing ThreadPilot user data for this Windows account...
rem Full uninstall removes only ThreadPilot-owned per-user AppData. Normal install/update paths never run this script.
if exist "%APPDATA%\ThreadPilot" (
    rd /s /q "%APPDATA%\ThreadPilot"
    echo ThreadPilot user data removed.
) else (
    echo No ThreadPilot user data folder found.
)

echo [4/4] Scheduling app folder removal...
start "" /min cmd /c "timeout /t 5 /nobreak >nul & rd /s /q \"%APP_DIR%\""

echo.
echo Uninstall completed. Remaining files will be removed in a few seconds.
endlocal
exit /b 0
'@

Set-Content -LiteralPath $uninstallPath -Value $uninstallContent -Encoding Ascii
Copy-Item "LICENSE" -Destination $licensePath -Force

Copy-Item "$singleFileDir/*" -Destination $portableStage -Recurse -Force
Copy-Item $uninstallPath -Destination (Join-Path $portableStage "uninstall.bat") -Force
Copy-Item $licensePath -Destination (Join-Path $portableStage "LICENSE.md") -Force

$portableZip = Join-Path $packagesDir ("ThreadPilot_v{0}_Portable.zip" -f $Version)

Compress-Archive -Path "$portableStage/*" -DestinationPath $portableZip -Force

$portableHash = (Get-FileHash $portableZip -Algorithm SHA256).Hash

Write-Host "PORTABLE_ZIP=$portableZip"
Write-Host "PORTABLE_SHA256=$portableHash"
