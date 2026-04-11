# ThreadPilot ✈️ <sup><kbd>v1.0.0</kbd></sup>

[![Status](https://img.shields.io/badge/Status-v1.0.0-success.svg)]()
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows%20Presentation%20Foundation-blue?logo=microsoft&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![License](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/Release-Latest-brightgreen)](../../releases)
[![Architecture](https://img.shields.io/badge/Architecture-x64-red?logo=windows&logoColor=white)](https://docs.microsoft.com/en-us/windows/win32/)

**ThreadPilot** is a Windows application for advanced **process management**, **CPU affinity control**, and **power plan automation**.  
Built with WPF and .NET 8, it is focused on deterministic performance workflows for power users, gamers, and system admins who want precise control over system resources.

[![Thread-Pilotbanner.png](https://i.postimg.cc/sDZLXMqr/Thread-Pilotbanner.png)](https://postimg.cc/cr0hkLd9)

## ✨ Features & Highlights

- **Advanced Process Management:** Real-time monitoring with CPU affinity and priority controls.
- **Enterprise-Grade Rules & Automation:** Rule-based automation for process-triggered power plan behavior, featuring graceful failure handling for Anti-Cheat protected games (Vanguard, EAC, BattlEye).
- **Performance Intelligence Dashboard:** Interactive dashboard with process hotspots, rule impact analysis, and timeline events.
- **Global vs. Per-Process Control:** Explicit visual separation of global power plan management from per-process actions.
- **System Tweaks:** Advanced Windows performance settings panel.
- **Dynamic Theming:** Light/Dark theming with seamless runtime switching.
- **Profile-based Configuration:** Persistent settings, customizable notification levels, and low-overhead minimized tray mode.

<img width="2470" height="1696" alt="Gemini_Generated_Image_xzgtdpxzgtdpxzgt" src="https://github.com/user-attachments/assets/e535e496-8b7d-4d38-883b-e0c7f68a610f" />

## ⚙️ Requirements

- **OS:** Windows 10 / 11 (x64)
- **Privileges:** Administrator privileges required for full WMI functionality and process control.
- **SDK:** .NET 8.0 SDK (only required for building from source).

---

## 📦 Installation

### Portable / Setup (Recommended)
1. Download the latest `ThreadPilot_v1.0.0` release from [GitHub Releases](../../releases)
2. Extract the archive
3. Run `ThreadPilot.exe` **as Administrator**

### Build from source
```bash
git clone [https://github.com/PrimeBuild-pc/ThreadPilot.git](https://github.com/PrimeBuild-pc/ThreadPilot.git)
cd ThreadPilot
dotnet build "ThreadPilot_1.sln" --configuration Release
dotnet run --project "ThreadPilot.csproj" --configuration Release
````

*Useful startup arguments: `--start-minimized`, `--autostart`, `--test`*

-----

## 🧪 Test Mode

Console test mode runs via the app entrypoint and uses the integrated `TestRunner.cs`.
*(Note: `dotnet test` will only execute tests in dedicated test projects; this repository currently uses an integrated runtime test runner).*

```bash
dotnet run --project "ThreadPilot.csproj" --configuration Release -- --test
```

*Note: In environments where elevation is required to start the app process, test mode requires an elevated terminal.*

-----

## 🛠️ Architecture & Tech Stack

  * **.NET 8.0** & **WPF**
  * **MVVM** pattern using `CommunityToolkit.Mvvm`
  * **Dependency Injection** (`Services/ServiceConfiguration.cs`)
  * **Rules & Automation Engine** (`Services/ProcessPowerPlanAssociationService.cs` & `ViewModels/ProcessPowerPlanAssociationViewModel.cs`)
  * **Performance Dashboard** (`Views/PerformanceView.xaml` & `ViewModels/PerformanceViewModel.cs`)

-----

## 📄 License

This project is licensed under the **GNU General Public License v3.0**.
See [LICENSE](https://www.google.com/search?q=LICENSE) for details.

-----

## 📞 Support

**Made for Windows power users☕** [](https://paypal.me/PrimeBuildOfficial?country.x=IT&locale.x=it_IT)

  * **Issues**: [https://github.com/PrimeBuild-pc/ThreadPilot/issues](https://github.com/PrimeBuild-pc/ThreadPilot/issues)
  * **Discussions**: [https://github.com/PrimeBuild-pc/ThreadPilot/discussions](https://github.com/PrimeBuild-pc/ThreadPilot/discussions)
