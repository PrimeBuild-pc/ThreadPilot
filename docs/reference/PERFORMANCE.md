# Performance Guide

This document consolidates performance diagnostics and optimization guidance for ThreadPilot.

## Profiling Instructions

1. Collect process footprint baseline:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File "build/collect-process-footprint.ps1" -ProcessName "ThreadPilot" -DurationMinutes 30 -SampleIntervalSeconds 5 -WaitForProcess
```

2. Optional runtime counters:

```powershell
dotnet-counters monitor --process-id <PID> --refresh-interval 5 --counters System.Runtime
```

## Benchmarks and Targets

- Idle tray CPU: target < 1% single-core equivalent
- Idle memory footprint: target < 100MB working set
- No sustained Gen2 pause > 100ms without investigation

## Optimization Notes

- Prefer event-driven process detection via WMI watchers.
- Use fallback polling with adaptive intervals and overlap protection.
- Keep UI-thread work minimal in periodic timers.

## Related Documents

- docs/audits/PHASE1_1_MEMORY_CPU_BASELINE.md
- docs/reference/runtimeconfig.template.json
