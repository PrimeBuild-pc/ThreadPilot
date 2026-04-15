# Safe Win32 Interop Examples

This guide provides safe patterns for native interop in ThreadPilot.

## 1) Prefer SafeHandle for Owned Handles

- Use SafeHandle-derived types for APIs that return handles.
- Dispose handles deterministically (`using` or explicit dispose paths).

## 2) Enable Error Propagation

- Use `SetLastError = true` in declarations where Win32 sets an error code.
- On failure, inspect `Marshal.GetLastWin32Error()` and log actionable context.

## 3) Validate Inputs Before Native Calls

- Guard pointers, lengths, and IDs.
- Reject invalid process IDs and empty operation payloads early.

## 4) Isolate Native Call Boundaries

- Keep P/Invoke calls inside dedicated helper/service classes.
- Wrap with typed exceptions at service boundary (`ThreadPilotException` hierarchy).

## 5) Example Pattern (Pseudo)

```csharp
if (processId <= 0)
{
    throw new ArgumentOutOfRangeException(nameof(processId));
}

using SafeProcessHandle handle = NativeMethods.OpenProcess(flags, false, (uint)processId);
if (handle.IsInvalid)
{
    int win32 = Marshal.GetLastWin32Error();
    throw new InvalidOperationException($"OpenProcess failed with Win32 error {win32}.");
}
```

## 6) Logging Hygiene

- Sanitize user-controlled strings before logging.
- Keep logs structured (event type + process + operation + result).
