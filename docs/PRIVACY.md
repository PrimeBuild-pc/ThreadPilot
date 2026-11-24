# Privacy

ThreadPilot runs locally and does not include advertising, analytics, telemetry, user accounts, or automatic log uploads.

The application reads local process, CPU, power, device, and Windows configuration information to provide its features. Settings, rules, tweak backups, activity history, and diagnostic logs are stored locally under the current Windows user profile. Users can inspect or remove those files locally.

When update checks are enabled, ThreadPilot contacts the GitHub API for the official repository's release metadata. If the user chooses to download an update, the release artifact is downloaded from GitHub. GitHub receives ordinary connection information such as the user's IP address and user agent under GitHub's own privacy terms.

ThreadPilot does not transmit the local process list, executable paths, hardware inventory, settings, rules, or logs to the project maintainers. Information is shared only when a user chooses to include it in a GitHub issue or another support request.

In the Microsoft Store edition ThreadPilot makes no network requests and downloads or runs no executables; updates come only from Microsoft Store.

The Power Plans and Settings pages contain links to Discord and GitHub Issues. ThreadPilot opens these links only after an explicit user click; the destination then receives ordinary connection information under its own privacy terms. ThreadPilot does not automatically download community power plans.

## Support

Email: primebuild.official@gmail.com. Website: https://primebuild.website. Bug reports and feature requests can also be opened on [GitHub Issues](https://github.com/PrimeBuild-pc/ThreadPilot/issues).

## Uninstalling ThreadPilot

Uninstalling the app (including the Microsoft Store edition) removes its program files but does **not** undo changes ThreadPilot made to Windows. Switch the tweaks and per-process "registry priority" options back in ThreadPilot **before** uninstalling. What can remain:

- Registry priority entries: `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\<program>\PerfOptions\CpuPriorityClass`.
- Tweaked values: Prefetch, Power Throttling, MMCSS Games scheduling, Ethernet and GPU driver properties, power-scheme AC values, mouse, menu-delay and Game Mode values, and the start type of the SysMain service.
- Scheduled tasks `ThreadPilot_Launch`, `ThreadPilot_Startup`, `ThreadPilot_Store_Launch` and `ThreadPilot_Store_Startup`. Turn off "start with Windows" first, or delete them in Task Scheduler as administrator.
- Power plans imported into Windows (remove with the Power Plans page or `powercfg /delete`).
- Local data: `%APPDATA%\ThreadPilot` (settings, rules, profiles, tweak backups) and `%LOCALAPPDATA%\ThreadPilot\Logs`. Delete them after restoring.

This policy describes ThreadPilot 1.7.6. Material changes to data collection or network behavior will be documented before release.
