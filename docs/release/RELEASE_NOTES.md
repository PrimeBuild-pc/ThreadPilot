# ThreadPilot v1.1.6 Release Notes

## Highlights

- Windows 11 native visual refresh across all major views with neutral Fluent surfaces.
- Refined sidebar navigation: separator lines softened for a cleaner, Settings-like appearance.
- Reduced visual weight and consistent card-based layouts in Rules, Logs, Performance, Settings, Tweaks, Process, Power Plans, and CPU Masks.
- Start minimized default clarified: `StartMinimized` now explicitly defaults to `false` for predictable manual-launch visibility.

## Added

- Windows 11 visual refresh pass completed for neutral Fluent surfaces and card polish.
- Sidebar navigation separator polish: horizontal separator lines removed/softened for a native Windows 11 Settings-like feel.

## Changed

- `StartMinimized` defaults to `false` in `ApplicationSettingsModel`: manual exe launch opens the main window visibly by default.
- Older settings JSON without `startMinimized` field now reliably defaults to `false`.
- Explicit saved `startMinimized: true` or `startMinimized: false` values remain fully respected.
- Project version updated to 1.1.6.

## Fixed

- Legacy settings without `startMinimized` no longer risk unexpected minimized startup.

## Breaking Changes

- None.

## Installation

### Installer

1. Download ThreadPilot_v1.1.6_Setup.exe.
2. Run installer.
3. Launch ThreadPilot.

### Portable

1. Download ThreadPilot_v1.1.6_singlefile_win-x64.zip.
2. Extract archive.
3. Run ThreadPilot.exe.

## Checksums

See SHA256SUMS.txt in release assets.

## Known Issues

- Windows 10 support remains best effort.
