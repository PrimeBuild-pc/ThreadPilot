# Dependency Remediation Plan

Date: 2026-04-15
Reference Artifacts:

- docs/audits/OUTDATED_PACKAGES_2026-04-15.json
- docs/audits/DEPENDENCY_AUDIT_2026-04-15.csv

## Current Risk Posture

- Known vulnerable packages: none detected.
- Update debt: moderate (major-version drift on Microsoft.Extensions and System.* packages).

## Priority Matrix

| Priority | Package Group | Current | Latest | Action |
|---|---|---|---|---|
| P0 | Security hotfixes (if any CVE appears) | varies | varies | Immediate patch and release |
| P1 | Microsoft.Extensions.* | 9.0.0 | 10.0.6 | Plan controlled upgrade branch |
| P1 | System.* runtime libraries | 8/9 | 10.0.6 | Upgrade with compatibility test sweep |
| P2 | CommunityToolkit.Mvvm | 8.4.0 | 8.4.2 | Minor update in maintenance window |

## Remediation Execution Plan

1. Create branch `chore/dependency-upgrade-wave1`.
2. Upgrade low-risk minor updates first (CommunityToolkit.Mvvm).
3. Run full build/test + manual smoke on Windows 10/11.
4. Upgrade Microsoft.Extensions and System.* as a second wave.
5. Validate:
   - startup and tray lifecycle
   - process monitoring and affinity flows
   - power plan switch and persistence
6. Re-run vulnerability and outdated scans and compare diffs.

## Validation Gates for Each Wave

```powershell
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release --no-restore
dotnet test ThreadPilot_1.sln --configuration Release --no-build
dotnet list ThreadPilot.csproj package --vulnerable --include-transitive
dotnet list ThreadPilot.csproj package --outdated --include-transitive
```

## Rollback Strategy

- Keep each wave in separate commits for quick revert.
- If regressions appear in process control paths, revert affected package group and retain scan artifacts for incident review.
