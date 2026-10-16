/*
 * ThreadPilot - Windows process memory priority P/Invoke declarations.
 */
namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    internal static partial class ProcessMemoryPriorityNativeMethods
    {
        internal const int ProcessIoPriority = 33;
        internal const string SeIncreaseBasePriorityPrivilege = "SeIncreaseBasePriorityPrivilege";
        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint SePrivilegeEnabled = 0x00000002;
        private const uint TokenPrivilegesInformationClass = 3;
        private static readonly object PrivilegeLock = new();
        private static readonly System.Collections.Generic.Dictionary<string, PrivilegeState> ActivePrivileges = new(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LuidAndAttributes
        {
            public Luid Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public Luid Luid;
            public uint Attributes;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial SafeProcessHandle OpenProcess(
            ProcessAccessFlags access,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
            uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize);

        [LibraryImport("kernel32.dll", EntryPoint = "GetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetProcessPowerThrottling(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref PowerThrottlingState processInformation,
            uint processInformationSize);

        [LibraryImport("kernel32.dll", EntryPoint = "SetProcessInformation", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessPowerThrottling(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref PowerThrottlingState processInformation,
            uint processInformationSize);

        [LibraryImport("ntdll.dll")]
        public static partial int NtQueryInformationProcess(
            SafeProcessHandle process,
            int processInformationClass,
            ref int processInformation,
            int processInformationLength,
            out uint returnLength);

        [LibraryImport("ntdll.dll")]
        public static partial int NtSetInformationProcess(
            SafeProcessHandle process,
            int processInformationClass,
            ref int processInformation,
            int processInformationLength);

        [LibraryImport("kernel32.dll")]
        public static partial IntPtr GetCurrentProcess();

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool OpenProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out SafeAccessTokenHandle tokenHandle);

        [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool LookupPrivilegeValue(
            string? lpSystemName,
            string lpName,
            out Luid lpLuid);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AdjustTokenPrivileges(
            SafeAccessTokenHandle tokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
            ref TokenPrivileges newState,
            uint bufferLength,
            out TokenPrivileges previousState,
            out uint returnLength);

        [LibraryImport("advapi32.dll", EntryPoint = "AdjustTokenPrivileges", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool AdjustTokenPrivileges(
            SafeAccessTokenHandle tokenHandle,
            [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
            ref TokenPrivileges newState,
            uint bufferLength,
            IntPtr previousState,
            IntPtr returnLength);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetTokenInformation(
            SafeAccessTokenHandle tokenHandle,
            uint tokenInformationClass,
            IntPtr tokenInformation,
            uint tokenInformationLength,
            out uint returnLength);

        internal static IDisposable AcquirePrivilegeScope(string privilegeName)
        {
            if (string.IsNullOrWhiteSpace(privilegeName))
            {
                return EmptyDisposable.Instance;
            }

            lock (PrivilegeLock)
            {
                if (ActivePrivileges.TryGetValue(privilegeName, out var existingState))
                {
                    existingState.RefCount++;
                    return new ScopedPrivilege(existingState);
                }

                if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var tokenHandle))
                {
                    return EmptyDisposable.Instance;
                }

                using (tokenHandle)
                {
                    if (tokenHandle.IsInvalid)
                    {
                        return EmptyDisposable.Instance;
                    }

                    if (!LookupPrivilegeValue(null, privilegeName, out var luid))
                    {
                        return EmptyDisposable.Instance;
                    }

                    var tp = new TokenPrivileges
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SePrivilegeEnabled,
                    };

                    bool adjusted = AdjustTokenPrivileges(
                        tokenHandle,
                        false,
                        ref tp,
                        (uint)Marshal.SizeOf<TokenPrivileges>(),
                        out var previousState,
                        out _);

                    int lastError = Marshal.GetLastWin32Error();
                    if (!adjusted || lastError != 0)
                    {
                        return EmptyDisposable.Instance;
                    }

                    var newState = new PrivilegeState(privilegeName, previousState);
                    ActivePrivileges[privilegeName] = newState;
                    return new ScopedPrivilege(newState);
                }
            }
        }

        internal static bool TryGetPrivilegeState(string privilegeName, out bool isPresent, out bool isEnabled)
        {
            isPresent = false;
            isEnabled = false;

            if (string.IsNullOrWhiteSpace(privilegeName))
            {
                return false;
            }

            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var tokenHandle))
            {
                return false;
            }

            using (tokenHandle)
            {
                if (tokenHandle.IsInvalid)
                {
                    return false;
                }

                if (!LookupPrivilegeValue(null, privilegeName, out var targetLuid))
                {
                    return false;
                }

                GetTokenInformation(tokenHandle, TokenPrivilegesInformationClass, IntPtr.Zero, 0, out uint requiredLength);
                if (requiredLength == 0)
                {
                    return false;
                }

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredLength);
                try
                {
                    if (!GetTokenInformation(tokenHandle, TokenPrivilegesInformationClass, buffer, requiredLength, out _))
                    {
                        return false;
                    }

                    int count = Marshal.ReadInt32(buffer);
                    int offset = 4;
                    for (int i = 0; i < count; i++)
                    {
                        var item = Marshal.PtrToStructure<LuidAndAttributes>(IntPtr.Add(buffer, offset));
                        if (item.Luid.LowPart == targetLuid.LowPart && item.Luid.HighPart == targetLuid.HighPart)
                        {
                            isPresent = true;
                            isEnabled = (item.Attributes & SePrivilegeEnabled) != 0;
                            return true;
                        }

                        offset += Marshal.SizeOf<LuidAndAttributes>();
                    }

                    return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        internal static bool IsPrivilegeEnabled(string privilegeName)
        {
            return TryGetPrivilegeState(privilegeName, out var isPresent, out var isEnabled) && isPresent && isEnabled;
        }

        internal static bool IsPrivilegePresent(string privilegeName)
        {
            return TryGetPrivilegeState(privilegeName, out var isPresent, out _) && isPresent;
        }

        private sealed class PrivilegeState
        {
            public PrivilegeState(string privilegeName, TokenPrivileges previousState)
            {
                this.PrivilegeName = privilegeName;
                this.PreviousState = previousState;
                this.RefCount = 1;
            }

            public string PrivilegeName { get; }

            public TokenPrivileges PreviousState { get; }

            public int RefCount { get; set; }

            public void Restore()
            {
                if (this.PreviousState.PrivilegeCount > 0)
                {
                    if (OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var tokenHandle))
                    {
                        using (tokenHandle)
                        {
                            if (!tokenHandle.IsInvalid)
                            {
                                var previous = this.PreviousState;
                                AdjustTokenPrivileges(
                                    tokenHandle,
                                    false,
                                    ref previous,
                                    0,
                                    IntPtr.Zero,
                                    IntPtr.Zero);
                            }
                        }
                    }
                }
            }
        }

        private sealed class ScopedPrivilege : IDisposable
        {
            private readonly PrivilegeState state;
            private int disposed;

            public ScopedPrivilege(PrivilegeState state)
            {
                this.state = state;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.disposed, 1) != 0)
                {
                    return;
                }

                lock (PrivilegeLock)
                {
                    this.state.RefCount--;
                    if (this.state.RefCount <= 0)
                    {
                        ActivePrivileges.Remove(this.state.PrivilegeName);
                        this.state.Restore();
                    }
                }
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static EmptyDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
