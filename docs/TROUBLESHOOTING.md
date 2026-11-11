# Troubleshooting

## SmartScreen Warning
ThreadPilot releases are currently unsigned. Windows SmartScreen may warn before first launch.

What to do:
- Verify the installer hash using SHA256SUMS.txt from the release page.
- Open file Properties and check the downloaded path/source.
- Use "More info" then "Run anyway" only after verification.

## UAC Prompt and Elevation
ThreadPilot requires administrator privileges at startup.

What to do:
- Launch ThreadPilot and approve the UAC prompt.
- If elevation is denied, restart and approve elevation.
- For startup automation, ensure the launch task runs with highest privileges.

## WMI Errors or Delayed Process Detection
If WMI is unavailable or slow, process event monitoring can degrade.

What to do:
- Ensure the Windows Management Instrumentation service is running.
- Reboot if repository consistency is suspected to be corrupted.
- Keep fallback polling enabled (default behavior).

## Saved Rules and Unavailable Executable Paths
A path-based saved rule can wait for executable metadata when a process first appears. ThreadPilot retries metadata lookup up to three times, with delays totaling three seconds, independently of WMI or fallback polling. It stops when the path is available, the process exits, monitoring stops, or the relevant rule is removed or disabled. The refreshed path must still match the current rule.

Look for `PersistentRulePathRetry` events in `%LOCALAPPDATA%\ThreadPilot\Logs` for scheduling, recovery, cancellation, exhausted retries, and matching/application outcomes. If the path stays inaccessible, the rule remains unapplied; retries do not run indefinitely.

## Performance Counter Access Denied
Some performance counters may fail on restricted systems.

What to do:
- Run with elevated privileges.
- Ensure Performance Logs and Alerts services are healthy.
- Validate that perflib counters are enabled on the machine.

## Smoke Test Command
For CI or local validation:

```powershell
ThreadPilot.exe --smoke-test
```

Exit code 0 means startup wiring and DI resolution succeeded.
