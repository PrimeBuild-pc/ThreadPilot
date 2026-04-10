# ThreadPilot

ThreadPilot is a Windows process and power plan manager built with WPF and .NET 8.
It is focused on deterministic performance workflows for power users, gamers, and admins.

## Highlights

- Process management with CPU affinity and priority controls.
- Rule-based automation for process-triggered power plan behavior.
- Global power plan management with explicit separation from per-process actions.
- System tweaks panel for advanced Windows performance settings.
- Performance Intelligence dashboard with hotspots, rule impact, and timeline events.
- Light/Dark theming with runtime switching.

## Requirements

- Windows 10/11 (x64)
- .NET 8 SDK for building from source
- Administrator privileges for full functionality

## Build

```bash
dotnet build "ThreadPilot_1.sln" --configuration Release
```

## Run

```bash
dotnet run --project "ThreadPilot.csproj" --configuration Release
```

Useful arguments:

- `--start-minimized`
- `--autostart`
- `--test` (console test mode)

## Test Mode

Console test mode runs via the app entrypoint and uses `TestRunner.cs`.

```bash
dotnet run --project "ThreadPilot.csproj" --configuration Release -- --test
```

Notes:

- In environments where elevation is required to start the app process, test mode may need an elevated terminal.
- `dotnet test` will only execute tests in dedicated test projects; this repository currently uses an integrated runtime test runner.

## Architecture Notes

- MVVM with CommunityToolkit.Mvvm.
- DI configuration in `Services/ServiceConfiguration.cs`.
- Rules and automation in `Services/ProcessPowerPlanAssociationService.cs` and `ViewModels/ProcessPowerPlanAssociationViewModel.cs`.
- Performance dashboard in `Views/PerformanceView.xaml` and `ViewModels/PerformanceViewModel.cs`.

## License

GPLv3. See `LICENSE`.
