# Release Quality Gates

This document defines Go/No-Go gates for ThreadPilot releases.

## Gate Checklist

| Gate | Criteria | Owner | Status |
|---|---|---|---|
| Build integrity | Release build succeeds with no blocking errors | CI/CD | Complete |
| Unit/integration tests | All tests pass on Release config | QA | Complete |
| Dependency security | No Critical/High vulnerabilities from dotnet audit | Security | Complete |
| Secret hygiene | Gitleaks scan has no active findings | Security | Complete |
| Performance baseline | Idle tray footprint collected and archived | Performance | Complete |
| Packaging | Installer + portable + checksums generated | Release Eng | Complete |
| Documentation | Release notes, runbook, changelog updated | Tech Writer | Complete |
| Compatibility | Smoke test on Windows 11 and Windows 10 best-effort | QA | Deferred (release candidate pass) |

## Mandatory Commands

```powershell
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release --no-restore
dotnet test ThreadPilot_1.sln --configuration Release --no-build
dotnet list ThreadPilot.csproj package --vulnerable --include-transitive
```

## Go/No-Go Rule

Release can proceed only when all gates are marked complete or explicitly waived with a written risk note.

## Evidence Snapshot (2026-04-15)

- Build: `dotnet build ThreadPilot_1.sln --configuration Release --no-restore` (success).
- Tests: `dotnet test Tests/ThreadPilot.Core.Tests/ThreadPilot.Core.Tests.csproj --configuration Release` (20 passed, 0 failed).
- Dependency security: `dotnet list ThreadPilot.csproj package --vulnerable --include-transitive` (no vulnerable packages).
- Secret hygiene: local gitleaks run, report `docs/audits/GITLEAKS_REPORT_2026-04-15.json` (no leaks).
- Packaging: installer/msix/packages/checksums generated in `artifacts/release/`.
