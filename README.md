<div align="center">

# ThreadPilot ✈️

**A free and open-source Windows process and power plan manager for deterministic performance workflows.**

[![Build](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml/badge.svg)](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml)
[![Release](https://img.shields.io/github/v/release/PrimeBuild-pc/ThreadPilot?sort=semver)](https://github.com/PrimeBuild-pc/ThreadPilot/releases)
[![winget](https://img.shields.io/winget/v/PrimeBuild.ThreadPilot?label=winget)](https://github.com/microsoft/winget-pkgs/tree/master/manifests/p/PrimeBuild/ThreadPilot)
[![Windows](https://img.shields.io/badge/Windows-11-blue?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)](LICENSE)
[![Issues](https://img.shields.io/github/issues/PrimeBuild-pc/ThreadPilot)](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
[![Discussions](https://img.shields.io/badge/Discussions-GitHub-6f42c1?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)

[Install](#-install) • [Features](#-features) • [Screenshots](#-screenshots) • [Build](#-build-from-source) • [Support](#-support-the-project)

</div>

[![Thread-Pilotbanner.png](https://i.postimg.cc/sDZLXMqr/Thread-Pilotbanner.png)](https://postimg.cc/cr0hkLd9)

## What is ThreadPilot?

ThreadPilot is a modern Windows desktop application for users who want predictable control over process behavior, CPU affinity, priority, power plans, and rule-driven performance workflows.

It is designed as an open-source alternative for power users who need Process Lasso-style capabilities, automation support, system tray controls, and a Windows 11-first experience.

## ✨ Features

- Live process management with refresh, filtering, and high-volume process handling.
- CPU affinity and priority controls with topology-aware logic.
- I/O and scheduler-related tuning utilities.
- Rule-based automation for power plan switching when selected processes start or stop.
- Conditional profiles, tray controls, Live Metrics, and dashboard views.
- Administrator-aware Windows desktop workflow.
- CI-backed release artifacts and package-manager distribution.

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

### Download from GitHub Releases

[![Download Latest Release](https://img.shields.io/badge/Download-Latest%20Release-2ea44f?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)
[![Portable ZIP Assets](https://img.shields.io/badge/Portable%20ZIP-Release%20Assets-1f6feb?logo=github)](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest)

| Package | Recommended use |
|---|---|
| `ThreadPilot_v<version>_Setup.exe` | Standard Windows installer for most users |
| `ThreadPilot_v<version>_singlefile_win-x64.zip` | Portable/no-install deployment |

Optional checksum verification:

```powershell
Get-FileHash .\ThreadPilot_v<version>_Setup.exe -Algorithm SHA256
Get-FileHash .\ThreadPilot_v<version>_singlefile_win-x64.zip -Algorithm SHA256
```

Compare the result with `SHA256SUMS.txt` from the same release.

## 🖼️ Screenshots

<img width="2470" height="1696" alt="Gemini_Generated_Image_xzgtdpxzgtdpxzgt" src="https://github.com/user-attachments/assets/e535e496-8b7d-4d38-883b-e0c7f68a610f" />

## ⚙️ Requirements

- Windows 11, build 22000 or newer.
- Administrator privileges to launch and manage system-level process settings.
- .NET 8 SDK only if you want to build from source.

## 🚀 Usage Notes

ThreadPilot uses an administrator-required manifest and requests elevation at startup. If UAC elevation is declined, the application exits instead of continuing in a limited mode.

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
