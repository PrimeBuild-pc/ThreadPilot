# ThreadPilot v1.1.2 Release Notes

## Highlights

- Improved global exception safety, including unobserved task handling.
- Better tray refresh resilience via adaptive timer backoff.
- Expanded release-readiness documentation and automation helpers.

## Added

- Domain exception hierarchy with ErrorCode registry.
- RetryPolicyService unit tests for transient/non-retriable flows.
- Quality gates, runbook, and release notes template documentation.

## Changed

- Reduced UI-thread pressure in periodic tray status updates.
- Expanded ignore rules for local AI/temp artifacts.

## Security

- Strengthened unhandled exception persistence context.

## Performance

- Adaptive tray update backoff to reduce repeated wake-ups during error conditions.

## Breaking Changes

- None.

## Installation

### Installer

1. Download ThreadPilot_v1.1.2_Setup.exe.
2. Run installer.
3. Launch ThreadPilot.

### Portable

1. Download ThreadPilot_v1.1.2_singlefile_win-x64.zip.
2. Extract archive.
3. Run ThreadPilot.exe.

## Checksums

See SHA256SUMS.txt in release assets.

## Known Issues

- Windows 10 support remains best effort.
