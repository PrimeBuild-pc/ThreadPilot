# Changelog

All notable changes to this project are documented in this file.

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
