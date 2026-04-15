# Development Guide (Operational)

This file provides a concise operational development workflow.

## Build

```powershell
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release --no-restore
```

## Test

```powershell
dotnet test ThreadPilot_1.sln --configuration Release --no-build
```

## Debug Notes

- Startup/unhandled exception handling is centralized in App.xaml.cs.
- Process monitoring orchestration is in ProcessMonitorManagerService.
- Tray lifecycle and UI startup flow are in MainWindow.xaml.cs.

## Common Tasks

- Install local git hooks:

```powershell
./build/install-git-hooks.ps1
```

- Package release artifacts:

```powershell
./build/build-installer.ps1 -Version "1.1.1"
./build/package-release-zips.ps1 -Version "1.1.1"
```

- Publish GitHub release (after artifacts are built):

```powershell
./build/create-github-release.ps1 -Version "1.1.1" -NotesFile "docs/release/RELEASE_NOTES.md"
```
