# ThreadPilot Project Structure

## Overview

This document reflects the repository as it exists today. It intentionally distinguishes:

- production application code
- xUnit coverage-driving tests
- legacy runtime smoke harnesses under `Tests/`

## Root Layout

```text
ThreadPilot/
├── .github/                  # CI, release, and security workflows
├── assets/                   # Static assets bundled into releases
├── build/                    # Release/package automation scripts
├── chocolatey/               # Chocolatey package template and install scripts
├── Converters/               # WPF binding converters
├── docs/                     # Reference, release, audit, and contributor docs
├── Helpers/                  # Shared helper utilities
├── Installer/                # Inno Setup installer definition
├── Models/                   # Domain/data models
├── Platforms/                # Windows-specific interop helpers
├── Properties/               # Publish profiles and app properties
├── Services/                 # Application and OS-integration services
├── Tests/
│   ├── ThreadPilot.Core.Tests/   # Real xUnit suite used by CI and Codecov
│   └── *.cs                      # Legacy runtime smoke/integration harnesses
├── Themes/                   # WPF theme resources
├── ViewModels/               # MVVM presentation logic
├── Views/                    # XAML views and code-behind
├── winget/                   # Submission scripts/reference manifests
├── App.xaml / App.xaml.cs    # Application bootstrap and DI entry
├── ThreadPilot.csproj        # Main WPF application project
└── ThreadPilot_1.sln         # Solution file
```

## Services

`Services/` contains the highest-value business and orchestration logic in the repo.

Key areas:

- `Abstractions/`: injectable seams such as settings storage, GitHub release client, and process runner
- `Core/`, `ProcessManagement/`: older organizational subfolders still present in the repo
- application services such as `ApplicationSettingsService`, `AutostartService`, `PowerPlanService`, `ProcessMonitorService`, `ProcessMonitorManagerService`
- infrastructure services such as `ServiceConfiguration`, `ServiceFactory`, `ServiceDisposalCoordinator`

## Tests

### Coverage-driving suite

`Tests/ThreadPilot.Core.Tests/` is the real automated test project.

- This is the suite executed by CI.
- This is the suite used for Cobertura/Codecov reporting.
- Coverage runsettings live in `Tests/ThreadPilot.Core.Tests/coverlet.runsettings`.

### Legacy harnesses

The other files directly under `Tests/` are not the xUnit suite.

They are retained as ad-hoc runtime smoke/integration harnesses used by debug-only `--test` mode in `App.xaml.cs`.

Current examples:

- `CpuTopologyServiceTests.cs`
- `ProcessSelectionTest.cs`
- `ExecutableBrowseTest.cs`
- `GameBoostIntegrationTest.cs`
- `ActiveApplicationsTest.cs`
- `TestRunner.cs`

These harnesses are useful for exploratory/manual checks, but they are not part of CI coverage expectations.

## Release and Packaging

Release automation is split across:

- `.github/workflows/release.yml`
- `build/`
- `Installer/`
- `chocolatey/`
- `winget/`

Generated release artifacts are expected under `artifacts/release/` during packaging runs.

## Notes

- `bin/`, `obj/`, and `TestResults*` directories are local/generated outputs and not part of the intended source structure.
- `docs/superpowers/plans/` contains implementation plans and is intentionally excluded from normal source-control expectations for GitHub publication.
