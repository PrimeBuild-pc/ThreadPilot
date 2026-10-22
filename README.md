<div align="center">
  <img src="assets/icons/ico.ico" alt="ThreadPilot app icon" width="128">
  <h1>ThreadPilot ✈️</h1>
  <p><strong>A free, open-source Windows process control center for deterministic CPU, priority, memory and power-plan workflows.</strong></p>
  <p>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/commits/main"><img alt="Last commit" src="https://img.shields.io/github/last-commit/PrimeBuild-pc/ThreadPilot?style=plastic&amp;logo=git&amp;logoColor=white"></a>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/stargazers"><img alt="GitHub stars" src="https://img.shields.io/github/stars/PrimeBuild-pc/ThreadPilot?style=plastic&amp;logo=github"></a>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/issues"><img alt="Open issues" src="https://img.shields.io/github/issues/PrimeBuild-pc/ThreadPilot?style=plastic&amp;logo=github"></a>
  </p>
  <p>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/PrimeBuild-pc/ThreadPilot/ci-devsecops.yml?branch=main&amp;style=plastic&amp;logo=githubactions&amp;label=build"></a>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/PrimeBuild-pc/ThreadPilot?style=plastic&amp;logo=github"></a>
    <a href="https://github.com/PrimeBuild-pc/ThreadPilot/releases"><img alt="Total downloads" src="https://img.shields.io/github/downloads/PrimeBuild-pc/ThreadPilot/total?style=plastic&amp;logo=github&amp;label=downloads&amp;color=2ea44f"></a>
    <a href="https://github.com/microsoft/winget-pkgs/tree/master/manifests/p/PrimeBuild/ThreadPilot"><img alt="WinGet version" src="https://img.shields.io/winget/v/PrimeBuild.ThreadPilot?style=plastic&amp;label=winget"></a>
    <a href="https://community.chocolatey.org/packages/threadpilot"><img alt="Chocolatey version" src="https://img.shields.io/chocolatey/v/threadpilot?style=plastic&amp;label=chocolatey"></a>
  </p>
  <p>
    <img alt="Windows 11" src="https://img.shields.io/badge/Windows-11-0078D4?style=plastic&amp;logo=windows&amp;logoColor=white">
    <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4?style=plastic&amp;logo=dotnet&amp;logoColor=white">
    <a href="Locales/"><img alt="Seven interface languages" src="https://img.shields.io/badge/UI%20languages-7-5B5FC7?style=plastic&amp;logo=googletranslate&amp;logoColor=white"></a>
    <a href="LICENSE"><img alt="AGPL v3 license" src="https://img.shields.io/badge/license-AGPLv3-2ea44f?style=plastic"></a>
  </p>
  <p><a href="#-install">Install</a> · <a href="#-features">Features</a> · <a href="#-screenshots">Screenshots</a> · <a href="#-build-from-source">Build</a> · <a href="#-support-the-project">Support</a></p>

</div>

<a href="assets/screenshots/overview-themes.png"><img width="1600" height="960" alt="ThreadPilot 1.7.8-beta.1: the current Processes interface in dark and light themes" src="assets/screenshots/overview-themes.png"></a>

<p align="center"><sub>Real dark/light screenshots of the interface. <a href="assets/screenshots/processes-dark.png">View dark</a> · <a href="assets/screenshots/processes-light.png">View light</a></sub></p>

## What is ThreadPilot?

ThreadPilot is a modern Windows process control center for users who want predictable control over process behavior, CPU affinity, CPU Sets, priority, memory priority, power plans, and saved process rules.

It is designed as an open-source alternative for power users who need Process Lasso-style capabilities, automation support, system tray controls, and a Windows 11-first experience. ThreadPilot is not a performance overlay: its primary job is applying explicit process controls safely and making the result visible.

## ✨ Features

- Live process management with refresh, filtering, context-menu actions, and a selected-process summary.
- Topology-aware CPU affinity through `CpuSelection`, including CPU Sets support, processor groups, and safe handling for systems with more than 64 logical processors.
- Safer CPU indexing in new affinity paths: CPU64 no longer aliases CPU0.
- Intel hybrid CPU handling through Windows topology and `EfficiencyClass`, not hardcoded SKU lists.
- Reusable CPU mask presets and custom core selections for process and rule workflows.
- Three-state efficiency mode / EcoQoS for running processes and persistent rules, independently of CPU priority.
- Opt-in Game Boost (off by default) with topology-aware game/background CPU Sets, optional priority and restoration. Real dual-CCD Ryzen/X3D and supported Intel hybrid validation is still pending.
- CPU priority controls with guardrails: High priority warning and Realtime priority blocked.
- Process memory priority support.
- Persistent process rules with explicit Apply now and Save as rule flows.
- Apply saved rules automatically when matching processes start while ThreadPilot is running.
- Rule-based automation for power plan switching when selected processes start or stop.
- Optional Diagnostics view, hidden by default, plus tray controls and dashboard views.
- Administrator-aware Windows desktop workflow.
- CI-backed build validation and package-manager distribution.
- Windows 11 native visual refresh with neutral Fluent surfaces and refined card-based layouts.

## 📦 Install

### Install with WinGet

ThreadPilot is available on WinGet as `PrimeBuild.ThreadPilot`.

From **Command Prompt** or **PowerShell**:

```cmd
winget install --id PrimeBuild.ThreadPilot -e
```

To refresh your local WinGet source first:

```cmd
winget source update
winget search ThreadPilot
```

### Install with Chocolatey

Install ThreadPilot from Chocolatey Community:

```powershell
choco install threadpilot
```

Upgrade an existing installation:

```powershell
choco upgrade threadpilot
```

### Download from GitHub Releases

[![Download Latest Stable](https://img.shields.io/badge/Download-Latest%20Stable-2ea44f?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)
**Stable v1.7.8 includes Game Boost** (off by default). It is not yet validated on real dual-CCD Ryzen and Intel hybrid CPUs; if you have one, see the [test steps and issue-reporting checklist](docs/releases/v1.7.8.md#help-us-test-dual-ccd-ryzen-and-intel-hybrid).

Both channels use the same application/settings location. Export a configuration backup before switching; they are not separate side-by-side installations.

| Package | Recommended use |
|---|---|
| `ThreadPilot_v<version>_Setup.exe` | Standard Windows installer for most users |
| `ThreadPilot_v<version>_Portable.zip` | Portable/no-install deployment |

Optional checksum verification:

```powershell
Get-FileHash .\ThreadPilot_v<version>_Setup.exe -Algorithm SHA256
Get-FileHash .\ThreadPilot_v<version>_Portable.zip -Algorithm SHA256
```

Compare the result with `SHA256SUMS.txt` from the same release.

## 🖼️ Screenshots

Captured from the official **v1.7.8-beta.1** installer in a Windows 11 test VM, using real UI and a sample Notepad rule. These are interface examples, not performance benchmarks. Click any image for full resolution.

<table>
  <tr>
    <td width="50%"><a href="assets/screenshots/processes-dark.png"><img src="assets/screenshots/processes-dark.png" alt="Processes in dark theme: selected process, CPU/memory/I/O priority, efficiency mode and core selection" width="800"></a><br><strong>Processes · Dark</strong><br>Live processes, priorities, EcoQoS and CPU cores.</td>
    <td width="50%"><a href="assets/screenshots/cpu-masks-light.png"><img src="assets/screenshots/cpu-masks-light.png" alt="CPU masks in light theme: reusable masks and the per-core editor" width="800"></a><br><strong>CPU masks · Light</strong><br>Reusable core selections for processes and rules.</td>
  </tr>
  <tr>
    <td><a href="assets/screenshots/power-plans-light.png"><img src="assets/screenshots/power-plans-light.png" alt="Power plans in light theme: installed Windows plans and custom power plan import" width="800"></a><br><strong>Power plans · Light</strong><br>Windows plans and custom .pow imports.</td>
    <td><a href="assets/screenshots/rules-dark.png"><img src="assets/screenshots/rules-dark.png" alt="Process rules in dark theme: a saved Notepad rule selected in the editor" width="800"></a><br><strong>Process rules · Dark</strong><br>Saved settings and the rule editor.</td>
  </tr>
  <tr>
    <td><a href="assets/screenshots/game-boost-dark.png"><img src="assets/screenshots/game-boost-dark.png" alt="Tweaks in dark theme with the Game Boost card, shown off by default" width="800"></a><br><strong>Game Boost overview · Dark</strong><br>Opt-in controls alongside Windows tweaks.</td>
    <td><a href="assets/screenshots/game-boost-light.png"><img src="assets/screenshots/game-boost-light.png" alt="Game Boost in light theme, enabled in the test VM with game/background app lists expanded and unsupported CPU fallback shown" width="800"></a><br><strong>Game Boost configuration · Light</strong><br>Game lists, exclusions and selected background apps.</td>
  </tr>
  <tr>
    <td><a href="assets/screenshots/activity-light.png"><img src="assets/screenshots/activity-light.png" alt="Activity in light theme: real settings and saved-rule events with filters and export" width="800"></a><br><strong>Activity · Light</strong><br>Changes, results, filters and export.</td>
    <td><a href="assets/screenshots/settings-dark.png"><img src="assets/screenshots/settings-dark.png" alt="Settings in dark theme: startup, language, theme, automation and CPU assignment preferences" width="800"></a><br><strong>Settings · Dark</strong><br>Startup, language, theme and automation preferences.</td>
  </tr>
</table>

## ⚙️ Requirements

- Windows 11, build 22000 or newer.
- Administrator privileges to launch and manage system-level process settings.
- .NET 8 SDK only if you want to build from source.

## 🚀 Usage Notes

ThreadPilot uses an administrator-required manifest and requests elevation at startup. If UAC elevation is declined, the application exits instead of continuing in a limited mode.

Persistent process rules are runtime-based. Apply at process start works only while ThreadPilot is running; it does not install a Windows Service, write registry or IFEO persistence, or use installer privilege tricks.

ThreadPilot does not bypass anti-cheat or protected-process restrictions. Running as administrator may help with normal access-denied cases, but it does not override protected-process or anti-cheat enforcement.

Useful startup arguments:

```text
--start-minimized
--autostart
--test
--smoke-test
```

In `Power Plans > Custom Power Plans`, use `Add .pow File` to import custom power plans directly from the app.

ThreadPilot does not bundle third-party `.pow` files. The Power Plans page can open the community Discord catalog on request; Settings also links to Discord support and GitHub Issues. Existing plans already imported into Windows are not removed by updates.

## 🧱 Build from Source

```powershell
git clone https://github.com/PrimeBuild-pc/ThreadPilot.git
cd ThreadPilot
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release
dotnet run --project ThreadPilot.csproj --configuration Release
```

Run integrated runtime tests:

```powershell
dotnet run --project ThreadPilot.csproj --configuration Release -- --test
```

Publish a self-contained Windows build:

```powershell
dotnet publish ThreadPilot.csproj --configuration Release --runtime win-x64 --self-contained true
```

Build release artifacts with the project script:

```powershell
./build/build-release.ps1
```

## 🔐 Quality and Security

- CI validates build, formatting, analyzers, vulnerability checks, and secret scanning.
- Security disclosures are handled through private GitHub advisories. See [`docs/SECURITY.md`](docs/SECURITY.md).
- ThreadPilot has no analytics or telemetry. See [`docs/PRIVACY.md`](docs/PRIVACY.md).
- Release signing scope and controls are documented in [`docs/CODE_SIGNING_POLICY.md`](docs/CODE_SIGNING_POLICY.md).
- Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org), once the pending application is approved; current artifacts are unsigned.
- Change history is tracked in [`docs/CHANGELOG.md`](docs/CHANGELOG.md).
- Coverage focuses on business/application code and excludes generated build artifacts.

## 🧭 Project Documentation

- [`docs/README.md`](docs/README.md)
- [`docs/CONTRIBUTING.md`](docs/CONTRIBUTING.md)
- [`docs/CODE_OF_CONDUCT.md`](docs/CODE_OF_CONDUCT.md)
- [`docs/RELEASE_SIGNING.md`](docs/RELEASE_SIGNING.md)
- [`docs/CODE_SIGNING_POLICY.md`](docs/CODE_SIGNING_POLICY.md)
- [`docs/PRIVACY.md`](docs/PRIVACY.md)
- [`docs/release/PACKAGING.md`](docs/release/PACKAGING.md)
- [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md)
- [`docs/QUALITY_GATES.md`](docs/QUALITY_GATES.md)
- [`docs/adr/`](docs/adr/)
- [`docs/releases/`](docs/releases/)

## 🗺️ Roadmap

- Expand unit and integration coverage for core services.
- Continue async reliability refactoring for long-running monitoring paths.
- Improve accessibility and localization readiness across major views.
- Formalize release signing and distribution hardening.

## 🤝 Contributing

Contributions are welcome. Before opening a pull request, please read [`docs/CONTRIBUTING.md`](docs/CONTRIBUTING.md) and [`docs/CODE_OF_CONDUCT.md`](docs/CODE_OF_CONDUCT.md).

For bugs, feature requests, or packaging issues, open a GitHub issue with reproduction steps and your Windows version.

## 📄 License

ThreadPilot is licensed under the **GNU Affero General Public License v3.0**. See [`LICENSE`](LICENSE).

## 💬 Support the Project

<div align="center">

**Built with care for Windows power users.**

[![GitHub Issues](https://img.shields.io/badge/Report%20a%20Bug-GitHub%20Issues-d73a49?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
[![GitHub Discussions](https://img.shields.io/badge/Ask%20a%20Question-Discussions-6f42c1?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)
[![PayPal](https://img.shields.io/badge/Support%20Development-PayPal-00457C?logo=paypal&logoColor=white)](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

If ThreadPilot is useful to you, consider starring the repository, opening thoughtful issues, sharing feedback, or supporting development with a small donation.

</div>
