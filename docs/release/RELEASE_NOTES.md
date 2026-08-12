## ThreadPilot v1.7.0

ThreadPilot 1.7.0 expands Windows gaming and device controls, adds persistent per-process I/O and power behavior, and prepares the release pipeline for trusted Authenticode signing.

### Highlights

- Control Game Mode, CPU Core Parking, supported CPU C-State behavior, USB selective suspend, pointer precision, supported Ethernet power-saving properties, interrupt moderation, and GPU MSI Mode.
- Open the correct Windows pages for Memory Integrity, HAGS, and windowed-game optimizations without silently weakening Windows security.
- Set memory and I/O priority per process and preserve them in persistent rules.
- Prevent system sleep while a selected process is running through a Windows power request that is released automatically.
- Restore backed-up device-driver values when supported tweaks are turned off.
- Open community power-plan and support links on demand, or report a problem directly through GitHub Issues.
- Use every new control and support action in all seven supported languages: English, Italian, German, Spanish, French, Russian, and Simplified Chinese.

### Power-plan distribution change

ThreadPilot no longer bundles the 89 third-party `.pow` files previously shipped with the application. Those files were publicly available, but no reliable per-file license or redistribution permission could be verified. Removing them keeps the signed release payload compatible with the SignPath Foundation requirement that distributed components have clear open-source redistribution rights.

This does not remove functionality or user data:

- Power plans already imported into Windows remain installed and active.
- An upgrade does not delete `.pow` files left by an earlier ThreadPilot installation.
- Manual `.pow` import remains available.
- The new **More plans?** action opens the ThreadPilot community Discord only when the user clicks it; no plan is downloaded automatically or included in the signed package.

### Compatibility and safety

- Existing rules remain compatible; all new persistent options default to off.
- Ethernet and GPU controls only modify properties already exposed by the installed driver and require administrator privileges.
- Hardware and driver behavior varies. GPU MSI Mode and some Windows graphics or security changes may require a restart.
- Memory Integrity remains a user-controlled Windows Security setting.

### Validation

- 701 automated Release tests pass, including XAML compilation and localization-key coverage for all seven languages.
- The final portable and installer artifacts contain zero `.pow` files.
- A Windows 11 Hyper-V gate passed portable launch, clean install, normal launch, system-tweak apply/restore, upgrade, and uninstall checks without ThreadPilot application errors.
- A real 1.6.0 to 1.7.0 installer upgrade preserved all 89 legacy `.pow` files byte-for-byte and did not change the active Windows power plan.

### Signing status

Release artifacts remain unsigned while the SignPath Foundation application is pending. The automated build, test, packaging, checksum, SBOM, and publishing flow remains in place, including its existing optional Authenticode path. The SignPath-specific integration will be added and tested separately after approval.
