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

With the current EXE installer or portable uninstaller, you can remove app data and request restoration of tracked Windows changes. Both options start selected. Use **Settings → Clean up before uninstalling** in the Microsoft Store edition, then remove the package through Windows Settings. This cleanup is available in builds that include the new uninstaller.

Cleanup preserves user exports, shared data used by another installation, and settings changed after ThreadPilot wrote them. Power plan files with no ownership record remain in place. Cleanup reports any retained data or failed operations. If recovery fails, retry cleanup before removing the program.

For ThreadPilot 1.7.11 and earlier, turn off tweaks, registry priority and Start with Windows in the app before removing it. Windows changes and imported power plans from older builds can require manual restoration because those versions did not save a verifiable baseline for every change. The Store edition can also leave the startup task and data outside the package if you uninstall it directly from Windows Settings.

App data is stored under `%APPDATA%\ThreadPilot` and logs under `%LOCALAPPDATA%\ThreadPilot\Logs`. Review recovery data before removing these folders manually.

Material changes to data collection or network behavior will be documented before release.
