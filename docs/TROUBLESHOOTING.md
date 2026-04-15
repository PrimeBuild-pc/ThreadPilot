# Troubleshooting

## SmartScreen Warning
ThreadPilot releases are currently unsigned. Windows SmartScreen may warn before first launch.

What to do:
- Verify the installer hash using SHA256SUMS.txt from the release page.
- Open file Properties and check the downloaded path/source.
- Use "More info" then "Run anyway" only after verification.

## UAC Prompt and Elevation
Some operations (priority, affinity, power plan changes) require admin rights.

What to do:
- Launch normally for read-only monitoring.
- Use in-app elevation request when needed.
- If elevation is denied, ThreadPilot continues in limited mode.

## WMI Errors or Delayed Process Detection
If WMI is unavailable or slow, process event monitoring can degrade.

What to do:
- Ensure the Windows Management Instrumentation service is running.
- Reboot if repository consistency is suspected to be corrupted.
- Keep fallback polling enabled (default behavior).

## Performance Counter Access Denied
Some performance counters may fail on restricted systems.

What to do:
- Run with elevated privileges.
- Ensure Performance Logs and Alerts services are healthy.
- Validate that perflib counters are enabled on the machine.

## App Does Not Start on Windows 10
ThreadPilot officially targets Windows 11; Windows 10 support is best effort.

What to do:
- Install latest Windows 10 updates (22H2+ recommended).
- Install .NET 8 runtime/SDK if running from source.
- Check %TEMP%\ThreadPilot_Debug.log for startup diagnostics.

## Smoke Test Command
For CI or local validation:

```powershell
ThreadPilot.exe --smoke-test
```

Exit code 0 means startup wiring and DI resolution succeeded.
