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
schtasks /Delete /TN "ThreadPilot_Launch" /F >nul 2>&1
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
