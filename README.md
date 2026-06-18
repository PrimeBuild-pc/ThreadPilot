<div align="center">

# ThreadPilot ✈️

**A free and open-source Windows process control center for deterministic CPU, priority, memory, and power-plan workflows.**

[![Build](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml/badge.svg)](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml)
[![codecov](https://codecov.io/gh/PrimeBuild-pc/ThreadPilot/branch/main/graph/badge.svg)](https://codecov.io/gh/PrimeBuild-pc/ThreadPilot)
[![Release](https://img.shields.io/github/v/release/PrimeBuild-pc/ThreadPilot?sort=semver)](https://github.com/PrimeBuild-pc/ThreadPilot/releases)
[![Downloads](https://img.shields.io/github/downloads/PrimeBuild-pc/ThreadPilot/latest/total?label=latest%20downloads&logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)
[![winget](https://img.shields.io/winget/v/PrimeBuild.ThreadPilot?label=winget)](https://github.com/microsoft/winget-pkgs/tree/master/manifests/p/PrimeBuild/ThreadPilot)
[![Chocolatey](https://img.shields.io/chocolatey/v/threadpilot?label=chocolatey)](https://community.chocolatey.org/packages/threadpilot)
[![Windows](https://img.shields.io/badge/Windows-11-blue?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)](LICENSE)
[![Issues](https://img.shields.io/github/issues/PrimeBuild-pc/ThreadPilot)](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
[![Discussions](https://img.shields.io/badge/Discussions-GitHub-6f42c1?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)

[Install](#-install) • [Features](#-features) • [Screenshots](#-screenshots) • [Build](#-build-from-source) • [Support](#-support-the-project)

</div>

<img width="1672" height="941" alt="cover" src="https://github.com/user-attachments/assets/858ec350-b997-4683-9df1-cc196307ede8" />

## What is ThreadPilot?

ThreadPilot is a modern Windows process control center for users who want predictable control over process behavior, CPU affinity, CPU Sets, priority, memory priority, power plans, and saved process rules.

It is designed as an open-source alternative for power users who need Process Lasso-style capabilities, automation support, system tray controls, and a Windows 11-first experience. ThreadPilot is not a performance overlay: its primary job is applying explicit process controls safely and making the result visible.

## ✨ Features

- Live process management with refresh, filtering, context-menu actions, and a selected-process summary.
- Topology-aware CPU affinity through `CpuSelection`, including CPU Sets support, processor groups, and safe handling for systems with more than 64 logical processors.
- Safer CPU indexing in new affinity paths: CPU64 no longer aliases CPU0.
- Intel hybrid CPU handling through Windows topology and `EfficiencyClass`, not hardcoded SKU lists.
- AMD CCD/L3-aware preset generation, also topology-driven instead of hardcoded SKU lists.
- Default gaming-oriented CPU presets for common foreground-game workflows.
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

[![Download Latest Release](https://img.shields.io/badge/Download-Latest%20Release-2ea44f?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)
[![Portable ZIP Assets](https://img.shields.io/badge/Portable%20ZIP-Release%20Assets-1f6feb?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)

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

<img width="1672" height="941" alt="banner" src="https://github.com/user-attachments/assets/b8504579-8cff-439d-a6a6-fcc73cfa2994" />

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
- Change history is tracked in [`docs/CHANGELOG.md`](docs/CHANGELOG.md).
- Coverage focuses on business/application code and excludes generated build artifacts.

## 🧭 Project Documentation

- [`docs/README.md`](docs/README.md)
- [`docs/CONTRIBUTING.md`](docs/CONTRIBUTING.md)
- [`docs/CODE_OF_CONDUCT.md`](docs/CODE_OF_CONDUCT.md)
- [`docs/RELEASE_SIGNING.md`](docs/RELEASE_SIGNING.md)
- [`docs/release/PACKAGING.md`](docs/release/PACKAGING.md)
- [`docs/reference/ARCHITECTURE_GUIDE.md`](docs/reference/ARCHITECTURE_GUIDE.md)
- [`docs/reference/DEVELOPER_GUIDE.md`](docs/reference/DEVELOPER_GUIDE.md)
- [`docs/reference/API_REFERENCE.md`](docs/reference/API_REFERENCE.md)
- [`docs/reference/PROJECT_STRUCTURE.md`](docs/reference/PROJECT_STRUCTURE.md)
- [`docs/reference/UI_STYLE_GUIDE.md`](docs/reference/UI_STYLE_GUIDE.md)

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
