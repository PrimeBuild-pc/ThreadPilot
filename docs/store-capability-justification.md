# ThreadPilot: Microsoft Store capability justification (policy 10.6.3)

Product ID `9PH5QDX08R6Z` · package `LorenzoMassafra.ThreadPilot` · analysed package: `1.7.11.0` (x64, manifest `packaging/msix/AppxManifest.xml`, Store build = `-p:StorePackage=true`, compile symbol `MSIX_STORE`).
Support: `primebuild.official@gmail.com` · `https://primebuild.website` · source: `https://github.com/PrimeBuild-pc/ThreadPilot` (AGPL-3.0).

Everything below was read from the code and manifest of this repository. Where something was **not** tested or is not true, it says so (see "Gaps found" and "Not verified"). Two differences from the sister app TweakHub matter for the review: ThreadPilot **always starts elevated** (`requireAdministrator`), and it **does not download or run any tool** in the Store build.

---

## 1. Paste-ready text for the Partner Center "business justification" field

> ThreadPilot is a Windows 11 process and power-management utility: CPU affinity and CPU Sets, process priority, power plans and a few system tweaks. Its only job is to change real Windows state: attributes of other running programs, HKLM values read by the kernel and drivers, the active power scheme, one service and, only if the user asks for it, one logon task. A copy of those changes inside a private package registry or file view would be invisible to Windows, and every feature would silently do nothing.
>
> **allowElevation.** ThreadPilot always runs elevated: its embedded manifest is requireAdministrator, so it cannot run as asInvoker. Elevation is needed to set affinity and priority on processes owned by services, other users or elevated games; to write HKLM (Image File Execution Options PerfOptions, Memory Management, PowerThrottling, MMCSS Games, network-adapter and GPU driver properties); and to change the SysMain service start type. UAC is shown at every launch. The app starts no other elevated program except schtasks, powercfg and sc.exe from System32. It installs no service or driver. Start with Windows is off by default. Only if the user turns it on (a one-time notice at first launch, or Settings) does ThreadPilot register one non-elevated logon task that starts the app through normal package activation, so Windows shows the UAC prompt at sign-in. No scheduled task runs elevated and nothing bypasses UAC. Turning the option off deletes the task. In the background it only sits in the tray and applies the user's own rules.
>
> **unvirtualizedResources.** Virtualization is disabled for the whole package. (1) Targets are open-ended: Image File Execution Options is keyed by the executable name of whatever program the user picks, and driver keys by the adapters present, so a fixed ExcludedKeys list cannot describe them. (2) Original values saved before a tweak (%APPDATA%\ThreadPilot) must outlive the package, because the system changes do.
>
> Every system change starts from a user toggle or rule. The Store build makes no network request and downloads or runs no executable; updates come only from Microsoft Store. No telemetry. What remains after uninstall, and how to remove it, is in docs/PRIVACY.md. Details: https://github.com/PrimeBuild-pc/ThreadPilot/blob/main/docs/store-capability-justification.md

(≈2,330 characters including spaces. If the field limit is lower, cut the last two sentences of the `allowElevation` paragraph first, then the first paragraph. The link points at `main` and works only after the PR for 1.7.11 is merged; before that use `.../blob/feat/store-autostart-opt-in/docs/store-capability-justification.md`.)

---

## 2. Point-by-point answers

### 2.1 Exact paths and keys excluded from virtualization
None are listed individually. The manifest has no `ExcludedKeys` or `ExcludedDirectories`; virtualization is switched off for the **entire package**. Effective scope is whatever the user switches on in ThreadPilot. Targets observed in code:

| Area | Target | Written by / when |
|---|---|---|
| HKLM | `SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\<exe file name>\PerfOptions` value `CpuPriorityClass` (set or delete); also deletes a legacy `PriorityClass` value under `...\Image File Execution Options\<exe file name>` | `ProcessService.SetRegistryPriorityAsync`, only from the per-process "registry priority" toggle (`ProcessViewModel.Behaviors.partial.cs`). Real-time class is blocked. The key name is the file name only, so it applies to any program with that name |
| HKLM | `SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters` value `EnablePrefetcher` | Tweaks page toggle (`SystemTweaksService`) |
| HKLM | `SYSTEM\CurrentControlSet\Control\Power\PowerThrottling` value `PowerThrottlingOff` | Tweaks page toggle |
| HKLM | `SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games` value `Scheduling Category` | Tweaks page toggle |
| HKLM | `SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\<nnnn>` (physical Ethernet adapters only) values `*EEE`, `AdvancedEEE`, `EnableGreenEthernet`, `PowerSavingMode`, `*InterruptModeration`; only values that already exist are changed | Tweaks page toggles; originals saved first |
| HKLM | `SYSTEM\CurrentControlSet\Enum\<PNPDeviceID of the display adapter>\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties` value `MSISupported`; only if the driver already exposes it | Tweaks page toggle; original saved first |
| Power scheme | AC values for core parking, processor idle disable (C-states) and USB selective suspend, written with `PowerWriteACValueIndex` and `PowerSetActiveScheme` (PowrProf API, not direct registry writes) | Tweaks page toggles |
| HKCU | `Control Panel\Mouse` (`MouseThreshold1`, `MouseThreshold2`, `MouseSpeed`), `Control Panel\Desktop` (`MenuShowDelay`), `Software\Microsoft\GameBar` (`AutoGameModeEnabled`) | Tweaks page toggles |
| HKCU | `Software\Microsoft\Windows\CurrentVersion\Run` value `ThreadPilot`: **only deleted**, legacy cleanup when the logon task is registered | `AutostartService` |
| File system (real profile) | `%APPDATA%\ThreadPilot\` (`settings.json`, `Profiles\`, `Configuration\`, `core_masks.json`, `persistent_rules.json`, `device_tweak_backups.json`, `game_boost_journal.json`, `ui-state.json`, `Powerplans\`), `%LOCALAPPDATA%\ThreadPilot\Logs\` | The app (`StoragePaths`, `EnhancedLoggingService`) |
| File system | `%SystemRoot%\Temp\ThreadPilot-task-<guid>\launch-task.xml`: admin-only directory (`AdminOnlyDirectory`), deleted right after `schtasks /Create` | `ElevatedTaskService.EnsureLaunchTaskAsync` |
| File system | Exports the user asks for (log export to Desktop) | `LogFileManager`, `LogViewerViewModel` |

Read-only access, for context: process list and executable paths (WMI/Win32), CPU topology, `SYSTEM\CurrentControlSet\Services\amd3dvcache`, HVCI/DeviceGuard state, HKCU Personalize theme key.

### 2.2 Whole package or specific keys/directories?
Whole package: `<desktop6:RegistryWriteVirtualization>disabled</…>` and `<desktop6:FileSystemWriteVirtualization>disabled</…>` in `<Properties>`, together with the `unvirtualizedResources` capability. No `ExcludedKeys`/`ExcludedDirectories`.

### 2.3 Writes by the packaged app vs. an elevated child process
There is no separate elevated child. The packaged executable has `requireAdministrator` in its embedded manifest (`app.manifest:8`, checked at build time by `packaging/msix/Build-Msix.ps1:54`), so **the main process is itself elevated** and does all the writes in 2.1 directly through the Win32 registry API and PowrProf. `uap10:TrustLevel="mediumIL"` in the manifest only describes the app type; it does not override the executable's UAC manifest.

Helper programs started by the elevated app, all with the same token, no shell, fixed System32 path:

| Helper | Use |
|---|---|
| `schtasks.exe` | create/query/delete the logon task, and delete a launch task left by an earlier build (2.4) |
| `powercfg.exe` | list, activate, import (user-chosen `.pow` file) and delete power plans |
| `sc.exe` | `config SysMain start= auto|disabled`, only that exact executable is allowed (`SystemTweaksService.IsAllowedExecutable`) |
| `explorer.exe` | open a folder / select a file (log viewer, process list) |
| `ms-settings:` URI | opens a Windows Settings page for two tweaks (HAGS, windowed optimisations); no write |

A grep of the source finds no use of `powershell.exe`, `cmd.exe`, `reg.exe` or any download-and-run call. `ElevationService.RestartWithElevation` (relaunch with `runas`) exists for the case where the app finds itself non-elevated; with `requireAdministrator` that only happens in a debug/test run or if the launch task starts it without elevation. Not exercised on a real install (see "Not verified").

Runtime changes to **other processes** (affinity, CPU Sets, priority class, memory priority, I/O priority, efficiency mode) are in-memory process attributes and are not written to disk. They are reset when ThreadPilot exits if "Reset process changes on exit" is on (default per `CHANGELOG`).

### 2.4 Writes to HKLM, Windows, Program Files, services, scheduled tasks, browser/app data, third-party resources

| Target | Purpose | Code |
|---|---|---|
| HKLM keys in 2.1 | Tweaks the user switches on or off; process-priority-by-registry for one executable the user selected | `SystemTweaksService`, `ProcessService` |
| Image File Execution Options subkey named after **a third-party executable** (for example a game's `.exe`) | The user selects the process; Windows then starts that program with the chosen CPU priority class (Idle/Below Normal/Normal/Above Normal/High). Removed when the user turns the toggle off | `ProcessService.SetRegistryPriorityAsync` |
| Service `SysMain` | Start type via `sc.exe config`, then start/stop through `ServiceController` | `SystemTweaksService.SetSysMainAsync` (admin check first) |
| Scheduled task `ThreadPilot_Store_Launch` | **Not created by the Store build since 1.7.11.** It used to let later launches start elevated without a second UAC prompt; it is now deleted if found (`ElevatedTaskService.EnsureLaunchTaskAsync`, `RemoveLaunchTaskAsync`). The EXE edition keeps its own `ThreadPilot_Launch` | `ElevatedTaskService` |
| Scheduled task `ThreadPilot_Store_Startup` | `ONLOGON`, **`/RL LIMITED` (not elevated)**, current user. Action: `%windir%\explorer.exe shell:AppsFolder\<package family name>!App`, i.e. normal package activation, so the app starts with package identity and Windows shows the UAC prompt. Registered only when the user turns "start with Windows" on (off by default since 1.7.11); deleted when it is turned off. An older task that ran the exe by path is replaced at the next elevated start | `AutostartService`, `ElevatedTaskService.EnsureAutostartTaskAsync` |
| Both tasks, safety rule | A task is registered only if the executable is under Program Files (`IsProtectedInstallPath`), so a non-admin user cannot replace the binary it launches. Tasks of the same two names that point elsewhere are deleted. The Store tasks use `_Store_` names so they never overwrite the EXE edition's tasks (`StorePackageTests`) | `ElevatedTaskService` |
| Program Files | No file is written there. The package directory is read-only to the app | n/a |
| Windows directory | Only the temporary admin-only task XML in `%SystemRoot%\Temp` (2.1) | `AdminOnlyDirectory` |
| Power plans | `powercfg /import` of a `.pow` the user picks, `/setactive`, `/delete` of a plan the user picks. ThreadPilot bundles no third-party `.pow` files; in the Store build plans are kept in `%APPDATA%\ThreadPilot\Powerplans` | `PowerPlanService` |
| Browser data, third-party app data | None read or written. | n/a |

### 2.5 Reproducible failure with standard MSIX virtualization
**Not reproduced by any recorded test in this repository.** The reasoning below follows Microsoft's documented behaviour plus the code, and should be demonstrated on the VM before it is quoted as an observation.

1. **Image File Execution Options.** The kernel reads `...\Image File Execution Options\<exe>\PerfOptions` from the real HKLM hive when it creates the process. A write that stays in a private per-package registry view is never read, so the "registry priority" feature would report success and have no effect.
2. **Driver and system keys** (`Class\{4d36e972…}`, `Enum\…\MessageSignaledInterruptProperties`, `PowerThrottling`, `Memory Management`, MMCSS `Games`): consumed by drivers, Memory Manager and the multimedia scheduler. Same failure mode: success in the UI, no change in Windows. (The power-scheme tweaks go through the PowrProf API and may behave correctly even with virtualization on; **not tested**.)
3. **Backups and journals.** With default file virtualization, files written under `%APPDATA%` go to the package's private store and are deleted on uninstall, while the registry changes above persist on the machine. After uninstall (or reinstall) the original values in `device_tweak_backups.json` and the Game Boost journal would be gone, and a reinstall could not restore them.
4. **Logon task.** The task points at the package executable under `WindowsApps`; this does not depend on virtualization.

Suggested demonstration on the VM `NeuroTune-W11` (**not run for this document**): install the same MSIX built with virtualization left at its default; toggle one IFEO priority for a test executable and one HKLM tweak; read the values from an unrelated process with `reg query`, start the test executable and read its priority in Task Manager; then uninstall and check whether `%APPDATA%\ThreadPilot\device_tweak_backups.json` still exists. Compare with the current package.

### 2.6 MSIX Flexible Virtualization
Evaluated on paper only (`ExcludedKeys`/`ExcludedDirectories`, which per Microsoft's documentation also require `unvirtualizedResources`). No prototype was built and none was tested.

- **File system: narrowing looks feasible.** The set of paths is small and known: `%APPDATA%\ThreadPilot`, `%LOCALAPPDATA%\ThreadPilot`, `%SystemRoot%\Temp`. **Proposed separately, not applied.**
- **Registry: partly feasible.** The HKLM tweak keys are fixed, and the subtrees `...\Image File Execution Options`, `SYSTEM\CurrentControlSet\Control\Class\{4d36e972-…}` and `SYSTEM\CurrentControlSet\Enum` could be listed as excluded subtrees. That would still give the app a write path into broad system subtrees, so the narrowing is cosmetic; the reviewer would see the same reach with more manifest. It also depends on the minimum OS build that flexible virtualization needs, which was **not checked** against the package's `MinVersion` 10.0.22000.0 (raising the minimum OS version would be a product decision).
- Conclusion: a list of exact keys is not possible for IFEO (user-chosen executable names) and for per-adapter/per-device keys. A narrower filesystem scope plus excluded registry subtrees is a possible follow-up, not a current claim.

### 2.7 `unvirtualizedResources` only for documented, user-chosen operations
For writes: yes by the code paths found. The setters for HKLM tweaks have a single production caller, `SystemTweaksViewModel` (a toggle the user clicks); the IFEO setter has a single caller, the per-process toggle; power-plan import/delete are triggered from the Power Plans page. Nothing writes those keys at start-up or in the background.

Automatic behaviour that does exist, and is not a user click:
- Re-registering the non-elevated `ThreadPilot_Store_Startup` task at an elevated start if it is missing or in an older format, **only while the user has "start with Windows" on**. The setting is off for new installs (`ApplicationSettingsModel.autostartWithWindows = false`); a profile saved by an earlier version keeps its value. A leftover `ThreadPilot_Store_Launch` is deleted in every case.
- Applying the user's own persistent rules and conditional profiles to matching processes while ThreadPilot runs in the tray. Game Boost is off by default (`gameBoostEnabled = false`).
- Update check: skipped in the Store build before any network access (`UpdateService.cs:53`).

This is a code review, not proof that no other path exists.

### 2.8 Downloaded executables and tools
**The Store build downloads no executable and performs no network request.**

| Item | Finding |
|---|---|
| In-app updater | `UpdateService.CheckForUpdatesAsync` and `DownloadAndInstallAsync` both return immediately when `StoreDistribution.IsStoreBuild`, with "Updates for this installation are managed by Microsoft Store." `UpdateInstallerService.LaunchInstallerElevatedAsync` throws as well (`StorePackageTests` covers this one). The only `HttpClient` is created in `ServiceConfiguration.cs` for the GitHub release client, which is unreachable in the Store build |
| Third-party tools | None downloaded or bundled. Power plan `.pow` files are not bundled; the Power Plans page and Settings open Discord and GitHub Issues links in the user's browser **only when clicked** |
| EXE edition only (not in the Store package) | Release asset from `api.github.com`/`github.com`, SHA-256 from the release's `SHA256SUMS.txt` (missing file = refused), Authenticode check, installer started only after the user confirms (`UpdateDownloadService.cs:40-65`) |

### 2.9 What remains after uninstall, where it is stated, how to restore and remove
Uninstalling an MSIX removes the package files. It does **not** revert anything ThreadPilot changed in Windows, and there is no uninstall hook in the package. Remaining items:

| Remains | Location | How to restore / remove |
|---|---|---|
| Per-executable priority entries | `HKLM\...\Image File Execution Options\<exe>\PerfOptions\CpuPriorityClass` (Windows keeps applying them to that exe name) | Turn the toggle off in ThreadPilot first; otherwise delete the `PerfOptions\CpuPriorityClass` value by hand |
| Tweaked values | Prefetch, PowerThrottling, MMCSS Games, Ethernet/GPU driver properties (backups in `device_tweak_backups.json`), power-scheme AC values, mouse/menu-delay/Game Mode values | Switch each tweak back in ThreadPilot **before** uninstalling; otherwise reinstall to restore, or change by hand |
| Service start type | `SysMain` | Re-enable the SysMain tweak, or `sc config SysMain start= auto` |
| Scheduled task (only if "start with Windows" was on) | `ThreadPilot_Store_Startup` (non-elevated; after uninstall its activation target no longer exists, so it does nothing) | Turn off "start with Windows" first (deletes it); or Task Scheduler / `schtasks /Delete /TN ThreadPilot_Store_Startup /F` |
| Imported power plans | Windows | `powercfg /delete <guid>` or the Power Plans page |
| Settings, rules, profiles, tweak backups, journal | `%APPDATA%\ThreadPilot\` | Delete after restoring |
| Logs | `%LOCALAPPDATA%\ThreadPilot\Logs\` | Delete |

Where this was declared before this document: one line in `packaging/msix/README.md`; `docs/PRIVACY.md` said only that data is stored locally. **Not shown inside the app or in the Store listing.** See G2.

### 2.10 Support contact and site (item 9)
`primebuild.official@gmail.com` and `https://primebuild.website`. Checked:
- `AppxManifest.xml`: has no support field; the Store listing contact is in Partner Center and was **not checked or touched**.
- `README.md`, `docs/PRIVACY.md`: named only GitHub Issues/Discord as support. **Fixed in this branch** (email and website added; GitHub kept as the bug-report channel).
- `packaging/msix/README.md`: "Public support: GitHub Issues". **Fixed and committed in 1.7.11** (email and website; GitHub kept for bug reports), see G3.
- App (Settings page): only Discord and GitHub Issues links (`SettingsViewModel.cs:24-25`, `Locales/*.xaml`). **Not changed** (G3).
- `docs/PRESS.md` already lists the email.

---

## 3. Gaps found

| # | Gap | Status |
|---|---|---|
| G1 | Start-with-Windows was on by default (`autostartWithWindows = true`). On the first elevated launch the app registered an `ONLOGON` highest-privilege task without a separate prompt, and `ThreadPilot_Store_Launch` at every elevated start so later launches skip UAC. A 10.6.3 reviewer could read this as silent persistent elevation | **Fixed in 1.7.11, all builds (Store and GitHub).** Default off for new installs; saved profiles keep their value (a profile without the key, written before the setting existed, keeps the old default). First launch shows one notice with "Turn on now" and "Open Settings" (seven languages). The logon task is created only when the user turns autostart on and deleted when it is turned off. In the Store build there is no UAC-free launch task at all (G8); in the EXE build it exists only while autostart is on. Tests: `AutostartServiceTests`, `ElevatedTaskServiceTests`, `ApplicationSettingsServiceTests`, `AutostartOffNoticePolicyTests` |
| G2 | Uninstall leftovers (IFEO priority entries, tweaked registry values, scheduled tasks, imported plans, `%APPDATA%` data) were not described to users anywhere clear | **Fixed in docs:** new "Uninstalling ThreadPilot" section in `docs/PRIVACY.md`. Still to do: two sentences in the Store listing description. An in-app notice or a "restore everything" action does not exist (no such feature found) and would be new code, not done |
| G3 | Support contact was GitHub only in `README.md`, `docs/PRIVACY.md`, `packaging/msix/README.md` and in the app's Settings (Discord, GitHub Issues). Microsoft does not accept GitHub as a support contact | **Fixed in docs** (`README.md`, `docs/PRIVACY.md`, `packaging/msix/README.md`, all committed). In-app: not changed (Settings still links Discord and GitHub Issues) |
| G4 | After a Store update the package path changes (the version is in it), and `EnsureAutostartAsync` returned early when the old task still existed, so the logon task kept pointing at the removed version | **Fixed in 1.7.11.** At each elevated start with autostart on, `EnsureAutostartAsync` reads the task's `<Command>` and re-registers it when it differs from the current executable path (case-insensitive full-path compare); the launch task is refreshed too. Tests: `EnsureAutostartAsync_WhenTaskPointsAtAnOlderPackageFolder_ReRegistersIt` and `..._WhenTaskAlreadyPointsHere_OnlyRefreshesTheLaunchTask`. An actual Store-to-Store update was **not** exercised (needs two published versions) |
| G5 | Failure under standard virtualization (2.5) is argued, not demonstrated | Open: run the VM demonstration before quoting it in the appeal |
| G6 | Registry priority is keyed by executable file name only, so it applies to every program with that name regardless of path | Documented; user-initiated; no change |
| G7 | `UpdateDownloadService` has no Store guard of its own; the guard is in `UpdateService` (the only caller) and in the installer | Verified unreachable in the Store build; no change |
| G8 | The Store tasks started the packaged exe **by path**, so the process had no package identity. On a PC where MSIX shares a payload file between packages, that file is executable only for package identities: the logon task crashed at once with `FileNotFoundException: WinRT.Runtime` (reproduced on a real host by running the task; not reproducible in a clean Sandbox) | **Fixed in 1.7.11:** the logon task is non-elevated and starts the app through package activation (`explorer.exe shell:AppsFolder\<PFN>!App`), which gives identity; the UAC-free launch task is no longer created in the Store build. Verified on the host: a limited task with that action starts ThreadPilot elevated with package identity. Tests in `StorePackageTests` |
| G9 | On one host (AMD Radeon RX 6950 XT, driver 32.0.21045.29003) the packaged app takes ~31 s to show its window: WPF's render thread waits in the AMD user-mode driver (`AMDXN64!OpenAdapter` from `d3d9`). The same payload outside the package starts in ~1 s, and the package starts in 2-3 s in Windows Sandbox | **Cause found (2026-11-28): AMD driver, not ThreadPilot.** Reproduced with the 1.7.11.0 test package: 31.2-31.8 s vs 1.0-1.2 s for the loose payload. The same loose exe started *inside* the package identity (`Invoke-CommandInDesktopPackage`) takes 32 s, so the trigger is package identity, not the WindowsApps path. An empty WPF window (Windows PowerShell, .NET Framework, no ThreadPilot code) takes 30.3 s with the identity and 0.2 s without it. Stacks: render thread in `Direct3DCreate9Ex` → `d3d9!CEnum` → `AMDXN64!OpenAdapter`, waiting on a second `AMDXN64` thread; the UI thread waits in `MilComposition_SyncFlush`; CPU stays idle (looks like a ~30 s timeout). Running AMD Software changes nothing. `RenderOptions.ProcessRenderMode = SoftwareOnly` only cuts it to ~10 s and drops hardware rendering, so there is no app-side fix. Not checked: the Store-signed package (still in certification), other drivers or GPUs |

Package for this version: `artifacts/msix/1.7.11.0/ThreadPilot_1.7.11.0_x64-Store.msix` (SHA-256 in `SHA256SUMS.txt` next to it). The manifest and its capabilities are unchanged from 1.7.9.0; the code changes are G1 and G4.

## 4. Not verified
- Microsoft's current wording of 10.6.3, and the exact length limit of the justification field.
- Behaviour at a real sign-in with default UAC settings: the host used for testing has `ConsentPromptBehaviorAdmin=0` (no prompt) and the shared VM was unreachable, so the UAC prompt shown by the logon task at sign-in was not observed. Tested on the host (1.7.11.0 test package): install, first-launch notice in 7 languages, turning autostart on and off, the logon task started manually (elevated, with package identity), a package update 1.7.11.0 → 1.7.12.0, uninstall. Not tested: tweak round trips, reinstall.
- Whether the power-scheme tweaks work with virtualization on (2.5).
- Flexible Virtualization: required OS build and manifest schema for ThreadPilot's `MinVersion`.
- The Store listing (description, support contact, privacy URL) as currently entered in Partner Center.
- The "Reset process changes on exit" default and the persistent-rule/conditional-profile paths were taken from `CHANGELOG`/code reading, not run.
- The registry list comes from a static scan of string literals in `Services/`; device-instance paths (adapter index, PNP device ID) are runtime values.
