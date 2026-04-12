# ThreadPilot v1.1.0

[![Windows CI](https://img.shields.io/badge/CI-Windows-success)](.github/workflows/ci-devsecops.yml)
[![Windows](https://img.shields.io/badge/Windows-11%20official%20%7C%2010%20best--effort-blue?logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-AGPLv3-blue.svg)](LICENSE)

ThreadPilot is a free and open-source Windows process and power plan manager focused on deterministic performance workflows.

The project targets users who need Process Lasso style capabilities in a modern WPF desktop application with enterprise-grade reliability, security hardening, and automation support.

## Key Features

- Process management with live refresh, filtering, and high-volume process handling.
- CPU affinity and priority controls with topology-aware logic.
- I/O and scheduling related tuning utilities.
- Rule-driven power plan switching based on process start/stop events.
- Conditional profiles, system tray controls, and runtime monitoring.
- Windows 11 first-class support; Windows 10 support is best effort.

## Screenshots

<img width="1474" height="920" alt="Screenshot 2025-12-21 044753" src="https://github.com/user-attachments/assets/77fc944e-d2a1-4cc7-9915-e01b0776106c" />

## Requirements

- Windows 11 (official support), Windows 10 22H2+ (best effort).
- .NET 8 SDK for source builds.
- Administrator privileges for advanced process and power operations.

## Installation

### Prebuilt Release
1. Download the latest package from Releases.
2. Extract the archive.
3. Start ThreadPilot as Administrator for full functionality.

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
./build-release.ps1
```

## Quality and Security

- CI validates build, formatting, analyzers, vulnerability checks, and secret scanning.
- Security disclosures are handled through private GitHub advisories. See SECURITY.md.
- Change history is tracked in CHANGELOG.md.

## Repository Docs

- docs/README.md
- docs/RELEASE_SIGNING.md
- ARCHITECTURE_GUIDE.md
- API_REFERENCE.md
- DEVELOPER_GUIDE.md
- PROJECT_STRUCTURE.md
- UI_STYLE_GUIDE.md

## Contributing

See CONTRIBUTING.md and CODE_OF_CONDUCT.md before opening pull requests.

## Roadmap

- Expand dedicated unit and integration coverage for core services.
- Continue async reliability refactoring for long-running monitoring paths.
- Improve accessibility and localization readiness in all major views.
- Formalize release signing and distribution hardening.

## License

Licensed under GNU Affero General Public License v3.0. See LICENSE.

## Support

**Made for Windows power users☕** [](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

  * **Issues**: [https://github.com/PrimeBuild-pc/ThreadPilot/issues](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
  * **Discussions**: [https://github.com/PrimeBuild-pc/ThreadPilot/discussions](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)