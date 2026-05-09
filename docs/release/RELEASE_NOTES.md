# ThreadPilot v1.1.3 Release Notes

## Highlights

- Lower background UI refresh work when ThreadPilot is minimized or hidden to tray.
- Safer process enumeration when Windows denies access or processes exit during refresh.
- More predictable affinity changes with explicit apply and OS verification.
- More stable process-driven power plan automation with duplicate and debounce protection.
- Redesigned Process Management layout with clearer selected-process actions.

## Added

- App refresh policy for foreground, minimized, tray-hidden, and background states.
- Lightweight process classification for foreground apps, visible-window apps, background processes, system processes, and protected/access-denied processes.
- Regression coverage for refresh policy, process deltas, affinity apply, process classification, passive error throttling, and power plan transition logic.

## Changed

- Process Management now uses a two-pane layout with the process table on the left and selected-process controls on the right.
- Affinity controls distinguish current OS affinity from pending core mask edits.
- Automation Monitoring and Live Metrics terminology is clearer across the app.
- Background refresh, virtualized preload, and UI monitoring are reduced when the UI is not active.

## Fixed

- Passive process read failures no longer empty the process table.
- Protected, elevated, and terminated processes are handled as best-effort entries during passive refresh.
- Process selection and core mask selection no longer apply affinity automatically.
- Repeated power plan changes for the same target are suppressed during rapid process churn.

## Security

- Access-denied and protected-process scenarios are handled without noisy passive refresh failures.

## Performance

- Reduced dispatcher and process-list work while the app is minimized, hidden to tray, or on non-process views.
- Improved process list delta updates to preserve existing rows and reduce collection churn.

## Breaking Changes

- None.

## Installation

### Installer

1. Download ThreadPilot_v1.1.3_Setup.exe.
2. Run installer.
3. Launch ThreadPilot.

### Portable

1. Download ThreadPilot_v1.1.3_singlefile_win-x64.zip.
2. Extract archive.
3. Run ThreadPilot.exe.

## Checksums

See SHA256SUMS.txt in release assets.

## Known Issues

- Windows 10 support remains best effort.
