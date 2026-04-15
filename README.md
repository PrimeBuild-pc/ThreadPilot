# ThreadPilot ✈️ v1.1.1

[![Build](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml/badge.svg)](https://github.com/PrimeBuild-pc/ThreadPilot/actions/workflows/ci-devsecops.yml)
[![Release](https://img.shields.io/github/v/release/PrimeBuild-pc/ThreadPilot?sort=semver)](https://github.com/PrimeBuild-pc/ThreadPilot/releases)
[![Coverage](https://codecov.io/gh/PrimeBuild-pc/ThreadPilot/branch/main/graph/badge.svg)](https://codecov.io/gh/PrimeBuild-pc/ThreadPilot)
[![winget](https://img.shields.io/winget/v/PrimeBuild.ThreadPilot)](https://github.com/microsoft/winget-pkgs)
[![Windows](https://img.shields.io/badge/Windows-11%20official%20%7C%2010%20best--effort-blue?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)](LICENSE)

ThreadPilot is a free and open-source Windows process and power plan manager focused on deterministic performance workflows.

The project targets users who need Process Lasso style capabilities in a modern WPF desktop application with enterprise-grade reliability, security hardening, and automation support.

[![Thread-Pilotbanner.png](https://i.postimg.cc/sDZLXMqr/Thread-Pilotbanner.png)](https://postimg.cc/cr0hkLd9)

## ✨ Key Features

- Process management with live refresh, filtering, and high-volume process handling.
- CPU affinity and priority controls with topology-aware logic.
- I/O and scheduling related tuning utilities.
- Rule-driven power plan switching based on process start/stop events.
- Conditional profiles, system tray controls, and runtime monitoring.
- Windows 11 first-class support; Windows 10 support is best effort.

## Screenshots

<img width="2470" height="1696" alt="Gemini_Generated_Image_xzgtdpxzgtdpxzgt" src="https://github.com/user-attachments/assets/e535e496-8b7d-4d38-883b-e0c7f68a610f" />

## ⚙️ Requirements

- Windows 11 (official support), Windows 10 22H2+ (best effort).
- .NET 8 SDK for source builds.
- Administrator privileges are required only for advanced process and power operations.

## 📦 Download

Latest artifacts are published on each tagged release in [GitHub Releases](https://github.com/PrimeBuild-pc/ThreadPilot/releases).

| Package | File name | Recommended use |
|---|---|---|
| Installer (Recommended) | `ThreadPilot_v1.1.1_Setup.exe` | Standard Windows installer (Inno Setup) for most users |
| Portable | `ThreadPilot_v1.1.1_singlefile_win-x64.zip` | No-install deployment for power users |
| MSIX (Secondary) | `ThreadPilot_1.1.1.0_win-x64.msix` | Advanced/enterprise sideload scenarios |

Verification example:

```powershell
Get-FileHash .\ThreadPilot_v1.1.1_Portable.zip -Algorithm SHA256
```

Install flow summary:

1. Download the package matching your deployment model.
2. Installer package (recommended): run `ThreadPilot_vX.Y.Z_Setup.exe` and complete the wizard.
3. Portable package: extract ZIP and launch `ThreadPilot.exe`.
4. MSIX package (secondary): install only if your environment supports sideload trust requirements.

Notes:

- ThreadPilot runs with a least-privilege manifest (`asInvoker`) and requests elevation only for operations that need administrator rights.
- If UAC elevation is declined for a privileged action, the application continues running in limited mode.
- In `Power Plans > Custom Power Plans`, use `Add .pow File` to add new custom plans directly from the app.
- The first opening of Performance shows a blocking onboarding modal with blurred background for clarity.

## Installation

Install from winget:

```powershell
winget install PrimeBuild.ThreadPilot
```

Install from Chocolatey:

```powershell
choco install threadpilot
```

Direct installer (latest release):

- https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest

Verify SHA256 before running the installer:

```powershell
Get-FileHash .\ThreadPilot_v1.1.1_Setup.exe -Algorithm SHA256
```

Compare the output with `SHA256SUMS.txt` from the same release.

### Build from Source

```powershell
git clone https://github.com/PrimeBuild-pc/ThreadPilot.git
cd ThreadPilot
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release
dotnet run --project ThreadPilot.csproj --configuration Release
```

Useful startup arguments:

- --start-minimized
- --autostart
- --test
- --smoke-test

## Usage Examples

Run integrated runtime tests:

```powershell
dotnet run --project ThreadPilot.csproj --configuration Release -- --test
```

Publish a self-contained build:

```powershell
dotnet publish ThreadPilot.csproj --configuration Release --runtime win-x64 --self-contained true
```

Build release artifacts via script:

```powershell
./build/build-release.ps1
```

## Quality and Security

- CI validates build, formatting, analyzers, vulnerability checks, and secret scanning.
- Security disclosures are handled through private GitHub advisories. See docs/SECURITY.md.
- Change history is tracked in docs/CHANGELOG.md.

## Comparison with Process Lasso

| Capability | ThreadPilot | Process Lasso |
|---|---|---|
| Open source | Yes (AGPL v3) | No |
| Rule-based power plan switching | Yes | Yes |
| CPU affinity and priority controls | Yes | Yes |
| Modern Windows 11 Fluent UI | Yes | Partial |
| Scriptable CI release artifacts | Yes | Not applicable |
| Package manager distribution | winget + Chocolatey | Varies by edition |

## Repository Docs

- docs/README.md
- docs/RELEASE_SIGNING.md
- docs/reference/ARCHITECTURE_GUIDE.md
- docs/reference/API_REFERENCE.md
- docs/reference/DEVELOPER_GUIDE.md
- docs/reference/PROJECT_STRUCTURE.md
- docs/reference/UI_STYLE_GUIDE.md
- docs/release/PACKAGING.md

## Contributing

See docs/CONTRIBUTING.md and docs/CODE_OF_CONDUCT.md before opening pull requests.

## 🛠️ Roadmap

- Expand dedicated unit and integration coverage for core services.
- Continue async reliability refactoring for long-running monitoring paths.
- Improve accessibility and localization readiness in all major views.
- Formalize release signing and distribution hardening.

## 📄 License

Licensed under GNU Affero General Public License v3.0. See LICENSE.

## 📞 Support

**Made with love for Windows power users☕** [PayPal.me](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

  * **Issues**: [https://github.com/PrimeBuild-pc/ThreadPilot/issues](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
  * **Discussions**: [https://github.com/PrimeBuild-pc/ThreadPilot/discussions](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)
