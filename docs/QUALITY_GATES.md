# Release Quality Gates

This document defines Go/No-Go gates for ThreadPilot releases.

## Gate Checklist

| Gate | Criteria | Owner | Status |
|---|---|---|---|
| Build integrity | Release build succeeds with no blocking errors | CI/CD | Complete |
| Core automated tests | `Tests/ThreadPilot.Core.Tests` passes on Release config | QA | Complete |
| Coverage gate | CI parses Cobertura and fails below the active threshold | QA | Complete |
| Dependency security | No Critical/High vulnerabilities from dotnet audit | Security | Complete |
| Secret hygiene | Gitleaks scan has no active findings | Security | Complete |
| Performance baseline | Idle tray footprint collected and archived | Performance | Complete |
| Packaging | Installer + release zips + checksums + packaging evidence generated | Release Eng | Complete |
| Documentation | Release notes, runbook, changelog updated | Tech Writer | Complete |
| Compatibility | Smoke test on Windows 11 and Windows 10 best-effort | QA | Deferred (release candidate pass) |

## Mandatory Commands

```powershell
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release --no-restore
dotnet test Tests/ThreadPilot.Core.Tests/ThreadPilot.Core.Tests.csproj --configuration Release --no-restore --collect:"XPlat Code Coverage" --settings "Tests/ThreadPilot.Core.Tests/coverlet.runsettings" --results-directory TestResults
dotnet list ThreadPilot.csproj package --vulnerable --include-transitive
```

## Coverage Scope

- The coverage badge tracks business/application code.
- Generated build artifacts are excluded via `coverlet.runsettings`.
- Legacy runtime harness files under `Tests/*.cs` are not part of the CI coverage contract.

## Active Coverage Threshold

The repository now uses a real Cobertura-parsed gate in CI.

- Transitional threshold: `1.5%` line coverage
- Reason: this matches the current honest baseline while service coverage is still being expanded
- Planned ratchet: raise progressively as additional deterministic service tests land

## Go/No-Go Rule

Release can proceed only when all gates are marked complete or explicitly waived with a written risk note.

## Evidence Snapshot (2026-04-21)

- Build: `dotnet build ThreadPilot_1.sln --configuration Release --no-restore` (success in CI release path).
- Tests: `dotnet test Tests/ThreadPilot.Core.Tests/ThreadPilot.Core.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --settings "Tests/ThreadPilot.Core.Tests/coverlet.runsettings"` (`45` passed, `0` failed in the current hardening branch).
- Coverage: Cobertura from the current hardening branch is `7.52%` line / `5.50%` branch and is enforced by CI against the transitional `1.5%` line gate.
- Dependency security: `dotnet list ThreadPilot.csproj package --vulnerable --include-transitive` (no vulnerable packages).
- Secret hygiene: local gitleaks run, report `docs/audits/GITLEAKS_REPORT_2026-04-15.json` (no leaks).
- Packaging: installer/packages/checksums plus winget/chocolatey evidence artifacts generated in `artifacts/release/` / workflow artifacts.
