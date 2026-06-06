# Changelog

All notable changes to this project are documented in this file.

## v1.3.0 - Localization support

### Added

- Added localization infrastructure.
- Added English and Simplified Chinese resource dictionaries.
- Added language selector in Settings.
- Added localized notification support for selected user-facing messages.
- Added tests for localization fallback, language persistence, Settings language selection, and localized notifications.

### Changed

- English remains the default application language.
- Simplified Chinese is available as an optional display language.
- Project version updated to 1.3.0.

### Safety

- Unsupported or invalid language settings now fall back to English.
- Missing translation keys fall back safely to English or the key.
- No changes to elevation, system tweaks, affinity, or privileged operation behavior.

## v1.2.0 - CPU topology, persistent rules, and process control update

### Added

- CPU topology v2 support with `CpuSelection` for topology-aware affinity.
- Group-aware CPU Sets support and safer handling for processor groups and systems with more than 64 logical processors.
- Memory priority controls and persistent process rules.
- Apply at process start for saved rules while ThreadPilot is running.
- Process tab context menu actions, explicit Apply now, and Save as rule flows.
- Selected process summary panel for current affinity, priority, memory priority, and last operation status.
- Optional Diagnostics experience hidden by default.

### Changed

- README and release documentation now describe ThreadPilot as a process control center rather than a performance overlay.
- Default presets are gaming-oriented and topology-aware.
- Intel hybrid handling uses topology and `EfficiencyClass` instead of hardcoded SKU lists.
- AMD preset generation is CCD/L3-aware and avoids hardcoded SKU lists.
- Project version updated to 1.2.0.

### Fixed

- Startup binding crash caused by a display-only selected-process summary message binding to a read-only property with a TwoWay-capable target.
- CPU64 no longer aliases CPU0 in the new safe affinity paths.
- Persistent rule auto-apply cancellation is handled as shutdown/cancellation instead of logged as a warning.

### Safety

- CPU priority guardrails warn for High priority and block Realtime priority.
- Anti-cheat/protected-process failures use safe user messaging and ThreadPilot does not bypass protected processes.
- Persistent rules reuse the existing affinity, priority, memory-priority, and Realtime guardrail backend instead of duplicating apply logic.

### Notes / limitations

- Apply at process start is runtime-based and works only while ThreadPilot is running.
- No Windows Service, registry autorun, IFEO persistence, installer privilege workaround, tag, GitHub release, or generated release artifact is included in this update.
- Administrator rights can help normal access-denied cases but do not bypass protected-process or anti-cheat restrictions.

## [1.1.6] - 2026-05-16

### Added

- Windows 11 native visual refresh: neutral Fluent surfaces, refined card styles, and reduced visual weight across Rules, Logs, Performance, Settings, Tweaks, Process, Power Plans, and CPU Masks views.
- Sidebar navigation separator polish: horizontal separator lines softened for a cleaner Windows 11 Settings-like sidebar appearance.
- Start minimized default clarification: `StartMinimized` now explicitly defaults to `false` for predictable manual-launch visibility.

### Changed

- Project version updated to 1.1.6.

### Fixed

- Older settings JSON without `startMinimized` field no longer risks unexpected minimized startup.

## [1.1.3] - 2026-05-09

### Added

- Expanded core service test coverage to 104 tests.

### Changed

- Hardened background refresh behavior while ThreadPilot is minimized or hidden to tray.
- Improved process enumeration resilience for access-denied, protected, and rapidly terminating processes.
- Added a verified affinity apply flow so UI state reflects the OS-confirmed affinity after explicit apply.
- Added duplicate and debounce protection for process-driven power plan switching.
- Redesigned Process Management into a clearer two-pane layout with selected-process actions on the right.
- Clarified terminology between Automation Monitoring and Live Metrics.

### Fixed

- Prevented passive process read failures from emptying the process table.
- Preserved explicit-only affinity changes so process selection and mask selection do not apply affinity automatically.

## [1.1.1] - 2026-04-15

### Added

- Global unobserved task exception handler with structured diagnostics.
- Domain exception hierarchy and ErrorCode registry (`ThreadPilotException` and derived types).
- Retry policy unit tests covering transient and non-retriable behaviors.
- Release readiness documentation set:
  - Exception handling policy
  - Quality gates
  - Release runbook
  - Release notes template
  - Test plan v1.1.1
- GitHub release automation script (`build/create-github-release.ps1`).

### Changed

- System tray periodic status updates now use adaptive backoff after failures.
- Tray update path reduces UI-thread load by collecting metrics off-dispatcher and applying UI updates on dispatcher only.
- `.gitignore` expanded for AI/workspace temporary artifact cleanup.

### Security

- Improved persistence of unhandled exception metadata for post-mortem diagnostics.
