# Changelog

All notable changes to this project are documented in this file.

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
