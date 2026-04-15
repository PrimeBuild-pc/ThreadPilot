# Phase 1.1 Memory and CPU Baseline Audit

Date: 2026-04-15
Scope: `RELEASE_PLAN.md` -> Phase 1.1 (memory/CPU footprint, long-running monitoring readiness)

## Current Status

- Baseline collection automation added: `build/collect-process-footprint.ps1`.
- Output format aligned with requested deliverable (CSV + summary JSON).
- Initial static audit completed for timer, polling, and process-monitoring paths.

## Baseline Collection Procedure

1. Start ThreadPilot in minimized mode and leave it idle in tray.
2. Collect a 30-minute baseline:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File "build/collect-process-footprint.ps1" `
  -ProcessName "ThreadPilot" `
  -DurationMinutes 30 `
  -SampleIntervalSeconds 5 `
  -WaitForProcess
```

3. Optional GC telemetry in parallel (same PID):

```powershell
dotnet-counters monitor --process-id <PID> --refresh-interval 5 --counters System.Runtime
```

4. Artifacts produced under `artifacts/perf/`:
   - `<process>-footprint-<timestamp>.csv`
   - `<process>-footprint-<timestamp>.summary.json`

CSV fields:
- `WorkingSetMB`
- `PrivateMemoryMB`
- `HandleCount`
- `CpuPercentSingleCore`
- `CpuPercentOverall`
- `ThreadCount`

## Static Audit Findings (Initial)

| Priority | Finding | Evidence | Impact |
|---|---|---|---|
| P1 | Tray status refresh marshals full update path to UI dispatcher every cycle | `MainWindow.xaml.cs:1189`, `MainWindow.xaml.cs:1235` | Potential UI-thread stalls during expensive metrics reads |
| P1 | Fallback polling allocates dictionary/list snapshots every iteration | `Services/ProcessMonitorService.cs:452`, `Services/ProcessMonitorService.cs:473` | Increased allocation pressure in prolonged fallback mode |
| P1 | No explicit GC telemetry/alerts for Gen2 duration and pressure | repository-wide search (no `GC.CollectionCount` diagnostics path) | Harder to detect long-run memory regressions before release |
| P2 | Performance monitor uses WMI for physical memory lookup path | `Services/PerformanceMonitoringService.cs:374` | WMI latency risk, partially mitigated by 5-minute cache |
| P2 | Startup and tray paths use multiple timed async operations with timeouts | `MainWindow.xaml.cs:429`, `MainWindow.xaml.cs:1156`, `MainWindow.xaml.cs:1193` | Operationally safe, but requires baseline validation under load |

## Positive Baseline Readiness Signals

- Process monitor is already event-first with WMI start/stop watchers and adaptive fallback polling.
  - `Services/ProcessMonitorService.cs:241`
  - `Services/ProcessMonitorService.cs:491`
- Overlap protection exists for fallback polling and tray refresh loops.
  - `Services/ProcessMonitorService.cs:431`
  - `MainWindow.xaml.cs:1202`

## Next Implementation Slice (Phase 1.1 continuation)

1. Capture real ThreadPilot 30-minute idle baseline in tray mode and archive CSV/summary.
2. Add lightweight runtime GC counters to diagnostics/logging path (`Gen0/1/2 collections`, allocated bytes).
3. Compare baseline after any polling refactor to validate CPU wake-up and memory trend improvements.
