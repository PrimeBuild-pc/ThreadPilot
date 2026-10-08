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

        [StructLayout(LayoutKind.Sequential)]
        internal struct Luid
        {
            public uint LowPart;
            public int HighPart;
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
            IntPtr previousState,
            IntPtr returnLength);

        internal static bool TryEnablePrivilege(string privilegeName)
        {
            if (string.IsNullOrWhiteSpace(privilegeName))
            {
                return false;
            }

            if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var tokenHandle))
            {
                return false;
            }

            using (tokenHandle)
            {
                if (tokenHandle.IsInvalid)
                {
                    return false;
                }

                if (!LookupPrivilegeValue(null, privilegeName, out var luid))
                {
                    return false;
                }

                var tp = new TokenPrivileges
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SePrivilegeEnabled,
                };

                if (!AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    return false;
                }

                return Marshal.GetLastWin32Error() == 0;
            }
        }
    }
}
