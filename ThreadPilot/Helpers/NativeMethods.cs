using System;
using System.Runtime.InteropServices;

namespace ThreadPilot.Helpers
{
    /// <summary>
    /// Contains P/Invoke declarations for Windows API functions used by ThreadPilot
    /// </summary>
    public static class NativeMethods
    {
        #region Process Management

        // Process access rights
        public const int PROCESS_QUERY_INFORMATION = 0x0400;
        public const int PROCESS_SET_INFORMATION = 0x0200;
        public const int PROCESS_VM_READ = 0x0010;
        public const int PROCESS_TERMINATE = 0x0001;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProcessAffinityMask(IntPtr hProcess, out IntPtr lpProcessAffinityMask, out IntPtr lpSystemAffinityMask);

        #endregion

        #region Registry Management

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegOpenKeyEx(
            IntPtr hKey,
            string subKey,
            uint options,
            int samDesired,
            out IntPtr phkResult);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            out uint lpType,
            IntPtr lpData,
            ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegCloseKey(IntPtr hKey);

        // Registry root keys
        public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        public static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

        // Registry access rights
        public const int KEY_READ = 0x20019;
        public const int KEY_WRITE = 0x20006;
        public const int KEY_ALL_ACCESS = 0xF003F;

        // Registry value types
        public const uint REG_NONE = 0;
        public const uint REG_SZ = 1;
        public const uint REG_EXPAND_SZ = 2;
        public const uint REG_BINARY = 3;
        public const uint REG_DWORD = 4;
        public const uint REG_DWORD_BIG_ENDIAN = 5;
        public const uint REG_LINK = 6;
        public const uint REG_MULTI_SZ = 7;

        #endregion

        #region System Information

        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32.dll")]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        #endregion

        #region Power Management

        [DllImport("powrprof.dll")]
        public static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            uint AccessFlags,
            uint Index,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("powrprof.dll")]
        public static extern uint PowerReadFriendlyName(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            IntPtr PowerSettingGuid,
            IntPtr Buffer,
            ref uint BufferSize);

        // Power enumerate access flags
        public const uint ACCESS_SCHEME = 0x00000010;
        public const uint ACCESS_SUBGROUP = 0x00000020;
        public const uint ACCESS_INDIVIDUAL_SETTING = 0x00000040;

        #endregion

        #region UI Management

        // Windows messaging constants
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MINIMIZE = 0xF020;
        public const int SC_CLOSE = 0xF060;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        // Show window constants
        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_RESTORE = 9;

        #endregion
    }
}
