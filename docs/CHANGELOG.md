# Changelog

All notable changes to this project are documented in this file.

## v1.6.0 - CPU assignment strategies

### Added

- Added four explicit CPU assignment modes: ThreadPilot automatic, Affinity Mask, Ideal Processor, and CPU Sets.
- Added a persistent global default and per-rule overrides. ThreadPilot automatic is the default and preserves the established CPU Sets with safe affinity fallback behavior.
- Added topology-aware, per-thread affinity for multi-group selections and processors above CPU 63.
- Added deterministic round-robin Ideal Processor assignment with per-thread verification.
- Added read-only visibility for reserved CPU Sets and availability checks scoped to the target process.
- Added localized mode descriptions and guidance in English, Italian, French, German, Spanish, Russian, and Simplified Chinese.

### Changed

- Process actions, notifications, and audit entries now report the requested and effective CPU assignment strategy.
- Explicit CPU Sets and Ideal Processor modes never use a hidden affinity fallback.
- Existing rules remain in ThreadPilot automatic mode and retain their historical behavior without rewriting stored data.

### Fixed

- Corrected the native `GetProcessDefaultCpuSets` call contract, preventing invalid memory access while verifying process CPU Sets.
- Thread enumeration now handles threads that exit during assignment without reporting a false success.

### Safety and compatibility

- ThreadPilot does not write the global Windows `ReservedCpuSets` registry configuration.
- Existing restrictive affinity is not broadened automatically when selecting a soft strategy; restarting the target process may be required.
- Ideal Processor and multi-group Affinity cover newly created threads through the existing automation monitor. With monitoring disabled, only threads present at apply time are affected.

### Validation

- 689 automated tests passed in both Debug and Release configurations.
- All four strategies passed hardware smoke tests on a Windows 11 Hyper-V guest with four logical processors.
- Multi-group and processor indexes above 63 are covered by synthetic topology tests pending access to suitable hardware.

## v1.5.3 - Reliability, lifecycle and threading hardening

### Fixed

- Saved process rules are no longer lost when the rules file cannot be read. A transient lock from antivirus, backup or a sync client used to pin an empty rule set for the rest of the session, so the next save overwrote every saved rule. A file that cannot be parsed is now preserved alongside the original for recovery.
- Pending edits in Settings are no longer discarded when something writes settings in the background, such as the startup update check recording its last-check time.
- Do Not Disturb now suppresses notifications during the configured quiet hours. The overnight comparison was inverted, so the default 22:00-08:00 window never suppressed anything while a daytime window suppressed everything. Turning Do Not Disturb on explicitly now takes effect regardless of the schedule, and the timed window raises its change notification when it expires.
- Default keyboard shortcuts are applied on a fresh installation. The fallback only ran when the stored shortcut list was null, which never happens, so no global hotkeys were registered.
- Closing the main window can no longer leave the window permanently unclosable when saving settings fails during shutdown.
- The last few seconds of diagnostics before exit are no longer lost. Application shutdown now releases the service container, which is what flushes buffered log entries and stops the WMI watchers and native performance counters.
- Stopping process monitoring during shutdown now actually stops it; disposal previously short-circuited itself and left the WMI watchers and the polling timer running.
- The tray Power Plans submenu now shows the active plan after a plan change instead of keeping the checkmark on whichever plan was active at startup.
- Per-core CPU readings work on systems with more than 64 logical processors by using the group-aware performance counter category, and a single unavailable core instance no longer clears the whole per-core view.
- Autostart no longer removes the existing startup entry before confirming the replacement was created, and updating autostart settings no longer disables it first.
- Notification delivery failures now retry as intended instead of being dropped.

### Changed

- Tray menu, tray tooltip and monitoring-status updates are marshalled to the UI thread. These are Windows Forms controls that were previously mutated from WMI and timer threads.
- Throttling, notification history, performance history and CPU counter state are guarded against concurrent access.
- View models owned by the main window are released on close, including the power plan refresh timer and subscriptions to long-lived services.
- Debug-logging state is cached instead of deep-cloning the settings model on every structured log call.
- A failed CPU Set topology probe is retried after a short delay instead of disabling CPU Sets for the rest of the session.

### Safety

- No change to the elevation model, the administrator-required manifest, the protected-process denylist, the Realtime priority block or the High priority warning.
- No change to how affinity, priority or memory priority are applied; the affinity apply pipeline and its fallback order are untouched.
- Power plan behaviour is unchanged. The tray now listens to the existing power-plan-changed notification rather than polling, so no additional `powercfg` calls are introduced.

### Notes

- Windows Management Instrumentation recovery still depends on fallback polling being enabled, which remains the default.

## v1.5.2 - Process monitoring configuration

### Fixed

- Consolidated process-monitoring configuration so application settings are the single runtime source for WMI, fallback polling, and fallback interval behavior.
- Removed ineffective duplicate WMI, fallback polling, and polling interval settings from Rules & Automation.
- Existing configuration JSON files remain compatible when they contain the removed legacy fields.

## v1.5.1 - Persistent automation monitoring preference

### Fixed

- Stopping Automation Monitoring now remains in effect after restarting ThreadPilot.
- Added a clear in-app confirmation explaining which automatic process rules pause while monitoring is disabled.
- Process Management now shows that monitoring is disabled instead of refreshing the process list.


## v1.5.0 - Languages, Windows 11 polish, and one-click updates

### Added

- Added complete Italian, French, German, Spanish, and Russian localization.
- Added Windows language detection with safe English fallback.
- Added one-click in-app updates: ThreadPilot checks on every enabled startup, asks for consent, verifies and installs the update, then relaunches automatically.

### Changed

- System tweak state is detected at every startup and verified again after changes.
- Core Parking and C-State detection now use native Windows power APIs instead of localized command output.
- Updated tweak controls, dialogs, and selected states with more neutral Windows 11 styling.
- Removed the unsupported HPET performance toggle and corrected the MMCSS Games scheduling tweak.

### Fixed

- Saving from the unsaved-settings dialog now persists language and other pending settings correctly.
- In-app upgrades preserve the user's `%AppData%` settings and no longer require a manual installer download or app restart.

## v1.4.4 - Runtime efficiency

### Performance

- Reduced background idle CPU by approximately 80-100% in measured tray scenarios.
- Reduced CPU overhead during short-lived process churn by approximately 8%.
- Reduced hidden working-set memory by approximately 4-5% and peak handle usage by approximately 12-13%.
- Reduced external command launches during visible startup by approximately 85% (27 to 4 in the measured scenario).
- Deferred inactive views, reused process snapshots and static metadata, and removed redundant `powercfg` calls.
- Suspended hidden Log Viewer updates, made log flushing event-driven, and added bounded WMI recovery backoff.

### Fixed

- Persistent rules are cached without weakening process-start matching or CPU-priority verification and retry behavior.
- Process handles and PID-scoped runtime state are consistently cleaned up.

## v1.4.3 - Expanded bundled power-plan catalog

### Added

- Added 30 new bundled Windows power plans.
- Added structural, discovery, duplicate, packaging, and invalid-file tests for bundled power plans.

### Changed

- Updated four existing bundled power plans and removed one redundant duplicate asset.
- Fixed parsing of power-plan display names containing parentheses.
- Confirmed automatic inclusion in build, publish, installer, and portable ZIP output.

## v1.4.2 - Persistent CPU-priority rule verification and retry

### Fixed

- Persistent CPU-priority rules now verify the applied priority.
- ThreadPilot detects when a process resets its CPU priority shortly after startup.
- A single bounded retry is performed when a verified priority is reverted.
- Retry state is cleared when the process exits.
- Improved activity logging distinguishes initial apply, reversion, retry, verification, and final failure.
- Added attribution for internal CPU-priority writes.
- Fixed misleading success reporting when the requested priority did not remain applied.
- Fixes #32.

## v1.4.0 - Safe in-app updater

### Added

- Added safe in-app updater support.
- Added manual update checks from Settings.
- Added optional background update checks with a default 7-day interval.
- Added latest/current version display in Settings.
- Added update download and install flow with explicit user confirmation.
- Added updater documentation.

### Security

- Update metadata is fetched only from the official PrimeBuild-pc/ThreadPilot GitHub repository.
- Prereleases are excluded by default.
- Installer assets are selected from GitHub HTTPS release assets.
- SHA256 checksums are verified when SHA256SUMS.txt is available.
- Checksum mismatches are rejected.
- Authenticode signature verification is performed on a best-effort basis, rejecting explicitly invalid signatures.
- Installer launch uses ProcessStartInfo without shell command construction.
- Concurrent update attempts are prevented.

### User data preservation

- Updates preserve AppData, settings, profiles, CPU masks, rules, custom/imported power plans, and logs.
- Only updater temporary files are cleaned by the update flow.
- Full uninstall behavior remains separate.

### Changed

- Project version updated to 1.4.0.
- Installer, packaging, Chocolatey, and release metadata updated to v1.4.0.

### Verification

- Build passed.
- Automated tests passed.

## v1.3.1 - Localization and installer metadata hotfix

### Fixed

- Completed Simplified Chinese localization coverage for primary WPF views, dialogs, context menus, tooltips, tray menu text, status text, and user-facing service messages.
- Changed Inno Setup display metadata so installed apps list ThreadPilot as `ThreadPilot` while keeping `1.3.1` in version metadata.
- Added guarded cleanup for obsolete `ThreadPilot 0.1.0-beta` uninstall registry metadata only when it clearly matches the old ThreadPilot display name and Program Files install path.

### Changed

- Project, package, installer, Chocolatey, Sonar, and app manifest metadata updated to 1.3.1.
- Full uninstall removes ThreadPilot-owned AppData/settings for the uninstalling user account and removes ThreadPilot startup entries; normal install/update preserves user data.

### Safety

- No automatic in-app updating was added.
- No elevation, affinity, process control, power plan, or system tweak behavior was changed.

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
