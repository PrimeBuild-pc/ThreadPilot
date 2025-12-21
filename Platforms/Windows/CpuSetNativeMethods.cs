using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace ThreadPilot.Platforms.Windows
{
    /// <summary>
    /// P/Invoke declarations for Windows CPU Set APIs
    /// </summary>
    internal static partial class CpuSetNativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial SafeProcessHandle OpenProcess(ProcessAccessFlags access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetSystemCpuSetInformation(IntPtr Information, uint BufferLength, ref uint ReturnedLength, SafeProcessHandle Process, uint Flags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetProcessDefaultCpuSets(SafeProcessHandle Process, uint[]? CpuSetIds, uint CpuSetIdCount);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetProcessTimes(SafeProcessHandle hProcess, out FILETIME lpCreationTime, out FILETIME lpExitTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);
    }

    [Flags]
    public enum ProcessAccessFlags : uint
    {
        PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
        PROCESS_SET_LIMITED_INFORMATION = 0x00002000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public readonly ulong ULong => (((ulong)dwHighDateTime) << 32) + dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_CPU_SET_INFORMATION
    {
        public uint Size;
        public CPU_SET_INFORMATION_TYPE Type;
        public uint Id;
        public ushort Group;
        public byte LogicalProcessorIndex;
        public byte CoreIndex;
        public byte LastLevelCacheIndex;
        public byte NumaNodeIndex;
        public byte EfficiencyClass;
        public byte AllFlags;
        public uint Reserved;
        public ulong AllocationTag;
    }

    public enum CPU_SET_INFORMATION_TYPE : int
    {
        CpuSetInformation = 0
    }
}
