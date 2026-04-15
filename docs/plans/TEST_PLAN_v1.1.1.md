# ThreadPilot Test Plan v1.1.1

Date: 2026-04-15
Scope: Critical-path unit and integration validation aligned to RELEASE_PLAN phases.

## Coverage Targets

- Critical services (P0): target >= 80% line coverage.
- Supporting services (P1/P2): target >= 60% line coverage.

## Coverage Matrix

| Component | Priority | Current test type | Required additions |
|---|---|---|---|
| ProcessMonitorManagerService | P0 | Integration-lite | Add conflict and recovery scenarios |
| ProcessMonitorService | P0 | Integration-lite | Add fallback polling behavior tests |
| RetryPolicyService | P0 | Unit | Added transient/non-retriable tests |
| PowerPlanService | P0 | Unit (security) | Extend with denied-access and fallback tests |
| ElevationService | P0 | Unit | Add UAC failure-path tests |
| SystemTrayService | P1 | None | Add context menu state tests |
| ApplicationSettingsService | P1 | None | Add persistence corruption tests |

## Implemented Test Additions (This Cycle)

- RetryPolicyServiceTests:
  - retries transient faults until success
  - does not retry when predicate blocks retries

## Next Test Additions

1. ProcessMonitorService adaptive polling interval transitions.
2. ProcessMonitorService WMI recovery fallback path.
3. App-level unobserved task exception handler behavior (non-crashing path).
4. Settings import validation for malformed payloads.

## CI Commands

```powershell
dotnet restore ThreadPilot_1.sln
dotnet build ThreadPilot_1.sln --configuration Release --no-restore
dotnet test ThreadPilot_1.sln --configuration Release --no-build --collect:"XPlat Code Coverage"
```

## Exit Criteria

- No failing tests on Release configuration.
- No flaky test observed in 3 consecutive CI runs.
- Critical-path coverage trend is non-decreasing.
