# ThreadPilot In-App Updates

ThreadPilot checks the official `PrimeBuild-pc/ThreadPilot` GitHub Releases feed for stable releases.

## Check Behavior

- Manual checks run from Settings.
- Startup checks run in the background only when automatic checks are enabled and the last check was more than the configured interval ago.
- The default interval is 7 days.
- Prereleases are excluded by default.
- Startup checks do not block app startup and never install updates.

## Install Flow

1. ThreadPilot reads release metadata from `PrimeBuild-pc/ThreadPilot`.
2. The updater selects a ThreadPilot installer asset from that release.
3. The installer downloads into a ThreadPilot-owned temp update directory.
4. If `SHA256SUMS.txt` is present in the release, ThreadPilot verifies the installer hash before launch.
5. ThreadPilot performs best-effort Authenticode verification. Invalid signatures are rejected; unsigned or unverifiable files are reported as unknown.
6. The user must confirm the update from Settings before download/install starts.
7. Windows shows the normal UAC prompt when the installer is launched elevated.
8. ThreadPilot requests shutdown after the installer starts so the installer can replace app files.

## Data Preservation

Updates do not delete ThreadPilot user data. The updater only cleans its own temporary download directory.

The following user-owned ThreadPilot data is preserved during update:

- settings;
- profiles;
- CPU masks;
- rules;
- imported or custom power plans;
- logs, subject to the app's normal log retention policy.

Full uninstall remains the path that removes ThreadPilot-owned AppData/settings. Update code must not call uninstall cleanup or remove `%AppData%\ThreadPilot`.

## Security Notes

- Update metadata is fetched only for the official `PrimeBuild-pc/ThreadPilot` repository.
- Asset URLs must be HTTPS GitHub release asset URLs.
- Installer file names must be safe file names and must match ThreadPilot installer naming.
- The updater does not invoke a shell command line to download files.
- The installer is launched with `ProcessStartInfo.ArgumentList` and `UseShellExecute=true` for UAC elevation.
- Concurrent update installation attempts are rejected.

## Known Limitation

If Windows keeps the elevated installer file locked after launch, immediate temp cleanup can fail. ThreadPilot logs the cleanup failure and leaves only the ThreadPilot update temp directory behind.
