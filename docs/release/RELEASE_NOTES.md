# ThreadPilot v1.7.10

- **Saved rules and settings.** A temporarily locked rules file can no longer cause existing rules to be overwritten. Failed configuration imports preserve settings saved concurrently, and failed edits leave the committed profile intact.
- **Process monitoring.** Startup scans and live events now agree on which power plan to use. Recovery distinguishes reused PIDs, preserves manual changes to settings already applied by an association, and respects a monitoring disable request made during startup. The configured default power plan returns after the last matching process exits, even if it exits during a switch.
- **Game Boost.** New sessions wait while ThreadPilot retries an unavailable recovery journal. Recovery runs even if Game Boost is disabled, and a session makes changes only when it can save the information needed to undo them.
- **Diagnostics and tweaks.** Temporary log write failures retain queued entries for retry, and device tweak failures roll back partial changes.

Installer and portable builds are available below with SHA-256 checksums.
