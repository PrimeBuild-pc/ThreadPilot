# P/Invoke Audit Report

Date: 2026-04-15
Scope: Native interop declarations and safety posture.

## Inventory Snapshot

| File | API/Pattern | Current State | Recommendation |
|---|---|---|---|
| App.xaml.cs | DllImport(kernel32) | Minimal use (debug console) | Keep debug-only guard |
| MainWindow.xaml.cs | DllImport(dwmapi) | UI theme attribute call | Validate return codes and graceful fallback |
| Platforms/Windows/CpuSetNativeMethods.cs | LibraryImport + SafeProcessHandle | Strong pattern | Keep as reference implementation |
| Services/CpuTopologyService.cs | DllImport(kernel32, SetLastError=true) | Acceptable | Add explicit error logging on Win32 failures |
| Services/KeyboardShortcutService.cs | DllImport(user32) | Needs review | Ensure unregister and handle lifecycle coverage |
| Services/ProcessService.cs | DllImport(kernel32 SetThreadExecutionState) | Acceptable | Keep exception boundaries and explicit result checks |

## Findings

1. SafeHandle usage is already present in CPU set path and should be preferred for any new handle-based interop.
2. Some legacy DllImport declarations can be gradually migrated to LibraryImport for source-generated marshalling where supported.
3. Interop calls should consistently capture and log Win32 error codes for post-mortem diagnostics.

## Action Items

- Add interop-focused analyzer review in release gate checklist.
- Add targeted tests around keyboard shortcut registration/unregistration lifecycle.
- Document safe interop patterns for contributors.
