@echo off
setlocal EnableExtensions
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1" %*
set "cleanupExit=%errorlevel%"
if exist "%~dp0uninstall-files.json" exit /b %cleanupExit%
rem Leave the batch context before deleting the running script.
(goto) 2>nul & del "%~f0" & cmd.exe /d /c exit %cleanupExit%
