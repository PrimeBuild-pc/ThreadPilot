@echo off
setlocal EnableExtensions

set "DRYRUN=0"
if /I "%~1"=="/dry-run" set "DRYRUN=1"
if /I "%~1"=="--dry-run" set "DRYRUN=1"

set "FAILURES=0"

echo ======================================================
echo ThreadPilot Uninstall Utility
echo ======================================================
echo.

if "%DRYRUN%"=="1" (
    echo Running in DRY-RUN mode. No changes will be made.
    echo.
) else (
    set /p CONFIRM=Proceed with uninstall? [Y/N]: 
    if /I not "%CONFIRM%"=="Y" (
        echo Uninstall cancelled.
        goto :finish
    )
)

echo [STEP] Remove ThreadPilot services
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would stop and delete services matching ThreadPilot*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'ThreadPilot*' -or $_.DisplayName -like 'ThreadPilot*' } | ForEach-Object { try { if ($_.Status -ne 'Stopped') { Stop-Service -Name $_.Name -Force -ErrorAction SilentlyContinue }; sc.exe delete $_.Name | Out-Null } catch { } }" >nul 2>&1
    if errorlevel 1 (
        set /a FAILURES+=1
        echo [WARN] Service cleanup reported errors
    ) else (
        echo [OK] Service cleanup complete
    )
)

echo [STEP] Remove scheduled tasks
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove scheduled tasks matching ThreadPilot*
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ScheduledTask -TaskName 'ThreadPilot*' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue" >nul 2>&1
    schtasks /Delete /TN "ThreadPilot_Startup" /F >nul 2>&1
    echo [OK] Scheduled task cleanup complete
)

echo [STEP] Remove autorun and application registry keys
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove HKCU and HKLM ThreadPilot registry keys
) else (
    reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "ThreadPilot" /f >nul 2>&1
    reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Run" /v "ThreadPilot" /f >nul 2>&1
    reg delete "HKCU\Software\ThreadPilot" /f >nul 2>&1
    reg delete "HKLM\Software\ThreadPilot" /f >nul 2>&1
    reg delete "HKLM\Software\WOW6432Node\ThreadPilot" /f >nul 2>&1
    echo [OK] Registry cleanup complete
)

echo [STEP] Remove Windows Firewall rules
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove firewall rules named ThreadPilot and ThreadPilot*
) else (
    netsh advfirewall firewall delete rule name="ThreadPilot" >nul 2>&1
    netsh advfirewall firewall delete rule name="ThreadPilot*" >nul 2>&1
    echo [OK] Firewall cleanup complete
)

echo [STEP] Remove MSIX installations
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove installed and provisioned ThreadPilot MSIX packages
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage -AllUsers *ThreadPilot* -ErrorAction SilentlyContinue | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue }" >nul 2>&1
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like '*ThreadPilot*' } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null }" >nul 2>&1
    echo [OK] MSIX cleanup complete
)

echo [STEP] Remove residual folders
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove AppData, LocalAppData, ProgramData, and Program Files folders
) else (
    rd /s /q "%APPDATA%\ThreadPilot" >nul 2>&1
    rd /s /q "%LOCALAPPDATA%\ThreadPilot" >nul 2>&1
    rd /s /q "%PROGRAMDATA%\ThreadPilot" >nul 2>&1
    rd /s /q "%ProgramFiles%\ThreadPilot" >nul 2>&1
    rd /s /q "%ProgramFiles(x86)%\ThreadPilot" >nul 2>&1
    for /d %%D in ("%LOCALAPPDATA%\Packages\PrimeBuild.ThreadPilot*") do rd /s /q "%%~fD" >nul 2>&1
    echo [OK] Residual folder cleanup complete
)

echo [STEP] Remove shortcuts
if "%DRYRUN%"=="1" (
    echo [DRY-RUN] Would remove Start Menu and Desktop shortcuts
) else (
    del /f /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\ThreadPilot.lnk" >nul 2>&1
    del /f /q "%PUBLIC%\Desktop\ThreadPilot.lnk" >nul 2>&1
    del /f /q "%USERPROFILE%\Desktop\ThreadPilot.lnk" >nul 2>&1
    echo [OK] Shortcut cleanup complete
)

echo.
if "%FAILURES%"=="0" (
    echo Uninstall completed successfully.
) else (
    echo Uninstall completed with %FAILURES% warning^(s^). Review output above.
)
echo ThreadPilot uninstall routine has finished.
echo.

:finish
if "%DRYRUN%"=="1" goto :exit
pause

:exit
exit /b 0
