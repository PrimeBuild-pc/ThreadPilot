# ThreadPilot v1.2.0 Release Notes Draft

## Highlights

- CPU topology v2 with topology-aware `CpuSelection`, CPU Sets, processor groups, and safer handling above 64 logical processors.
- New safe affinity paths where CPU64 no longer aliases CPU0.
- Intel hybrid handling through topology and `EfficiencyClass`, plus AMD CCD/L3-aware preset generation.
- Memory priority support and persistent process rules.
- Apply saved rules automatically when matching processes start while ThreadPilot is running.
- Process tab context menu actions, Save as rule, Apply now, and selected-process summary.

## Added

- CPU topology v2 and `CpuSelection` for topology-aware affinity.
- Group-aware CPU Sets support and processor group safety.
- Memory priority controls.
- Persistent process rules with runtime apply-at-process-start support.
- Process tab context menu actions and selected-process summary.
- Optional Diagnostics view hidden by default.

## Changed

- Default presets are gaming-oriented and generated from topology rather than hardcoded CPU SKU lists.
- Intel hybrid behavior uses Windows topology and `EfficiencyClass`.
- AMD behavior uses CCD/L3-aware preset generation.
- Project version updated to 1.2.0.

## Fixed

- Startup no longer fails from a read-only selected-process summary binding.
- CPU64 no longer aliases CPU0 in new safe affinity paths.
- Persistent rule auto-apply cancellation does not log shutdown/future cancellation as a warning.

## Safety

- High CPU priority shows a warning and Realtime priority remains blocked.
- ThreadPilot does not bypass anti-cheat or protected-process restrictions.
- Administrator rights may help ordinary access-denied cases but do not bypass protected processes.

## Compatibility and Upgrade Notes

- Requires Windows 11 build 22000 or newer.
- Existing legacy affinity profiles continue to load.
- New saved rules prefer topology-aware `CpuSelection` when safe topology mapping is available.
- Apply at process start works only while ThreadPilot is running.

## Known Non-Goals

- No anti-cheat bypass.
- No Windows Service.
- No registry or IFEO persistence.
- No generated release artifacts yet.
- No GitHub release or tag yet.

## Release Artifact Status

- Installer, portable ZIP, checksums, package metadata verification, and release upload remain pending manual validation.
