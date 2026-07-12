## ThreadPilot v1.4.4

Patch release focused on reducing ThreadPilot's foreground and background runtime overhead while preserving process automation and persistent CPU-priority verification.

### Measured improvements

Compared with the official v1.4.3 build on the same system and using the same Release profile:

- **Approximately 80-100% lower background idle CPU** in measured tray scenarios, reaching 0% in the final idle sample.
- **Approximately 8% lower CPU overhead during process churn** with 80 short-lived processes.
- **Approximately 4-5% lower hidden working-set memory**.
- **Approximately 12-13% lower peak handle usage** while hidden.
- **Approximately 85% fewer external command launches during visible startup**, reduced from 27 to 4.

Results vary by hardware, Windows configuration, active page, and workload. The Process page remains intentionally more active while visible because it refreshes live process information.

### Runtime changes

- Cached persistent-rule snapshots in memory and pre-matched process starts before expensive enrichment.
- Disposed process handles consistently and cleared PID-scoped caches on process exit.
- Deferred initialization of inactive Power Plans, Rules, Settings, Tweaks, and Logs pages.
- Reused the process monitor's initial snapshot and PID/start-time-validated static process metadata.
- Parsed the active plan from a single `powercfg /list` call and limited periodic power-plan refresh to the visible Power Plans page.
- Suspended Log Viewer collection updates while hidden and changed log flushing to one-shot scheduling.
- Added WMI recovery backoff while preserving fallback polling reliability.

### Compatibility and safety

- Persistent rules, process start/stop tracking, and lazy diagnostics remain covered by regression tests.
- CPU-priority verification and the bounded retry introduced for issue #32 are unchanged.
- No default ThreadPilot affinity limit was enabled.

### Validation

- Release build completed with zero warnings and zero errors.
- All 581 automated tests passed before release preparation.
- CI and CodeQL passed for both performance pull requests.
- NuGet dependency audit found no known vulnerable packages.
