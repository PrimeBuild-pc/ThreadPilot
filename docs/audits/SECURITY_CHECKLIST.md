# Security Checklist and Risk Matrix

Date: 2026-04-15
Scope: Release readiness hardening (privileges, interop, process manipulation, configuration safety).

## Checklist

- [x] Validate privilege model behavior against product policy (requireAdministrator vs asInvoker).
- [x] Verify all process handle APIs use safe cleanup patterns.
- [x] Verify forbidden critical process list cannot be optimized (System, csrss, lsass, wininit).
- [x] Confirm configuration file writes stay in user-space writable paths.
- [x] Confirm no secrets are persisted in plaintext configuration.
- [x] Confirm structured logging sanitizes user-provided strings.
- [x] Confirm dependency vulnerability scan is green in CI.
- [x] Confirm secret scanning is green in CI.
- [x] Confirm security scanner binaries are downloaded at runtime in CI and not vendored in repository history.

## Validation Evidence

- User-space configuration path verified via `StoragePaths.AppDataRoot` (`%AppData%\\ThreadPilot`) and `ApplicationSettingsService` persistence path usage.
- Secret scan evidence: `docs/audits/GITLEAKS_REPORT_2026-04-15.json` (no leaks found).
- Dependency scan evidence: `docs/audits/VULNERABILITY_SCAN_2026-04-15.json` (no vulnerable packages).
- Scanner runtime policy: `.github/workflows/ci-devsecops.yml` downloads Gitleaks to `$RUNNER_TEMP` and does not require vendored scanner artifacts.

## P/Invoke Audit Snapshot

| API/Pattern | Location | Current status | Notes |
|---|---|---|---|
| OpenProcess (SafeProcessHandle) | Platforms/Windows/CpuSetNativeMethods.cs | Good | Uses SafeHandle wrapper |
| Process handle lifecycle | Platforms/Windows/ProcessCpuSetHandler.cs | Good | Dispose paths present |
| DllImport declarations | App.xaml.cs, MainWindow.xaml.cs, Services/* | Review required | Validate SetLastError and marshaling consistency |
| Keyboard hooks hotkeys | Services/KeyboardShortcutService.cs | Review required | Ensure unregister on dispose |

## Privilege Escalation Risk Matrix

| Risk | Severity | Likelihood | Mitigation |
|---|---|---|---|
| Over-broad elevated runtime | High | Medium | Keep privileged operations minimal and audited |
| Unauthorized process manipulation | High | Low-Medium | Enforce critical-process denylist and validation |
| Handle/resource leak in native interop | Medium | Medium | Continue SafeHandle usage + disposal tests |
| Logging injection via external input | Medium | Medium | Sanitize and structure user-provided strings |

## Immediate Next Actions

1. Add automated tests for critical-process denylist enforcement.
2. Add targeted interop analyzer rules for DllImport signatures.
3. Add release gate requiring successful vulnerability + secret scans.
