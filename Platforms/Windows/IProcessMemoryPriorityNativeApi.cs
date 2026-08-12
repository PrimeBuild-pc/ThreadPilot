/*
 * ThreadPilot - Windows process memory priority native API abstraction.
 */
namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    public interface IProcessMemoryPriorityNativeApi
    {
        bool IsSupported { get; }

        SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId);

        bool GetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize);

        bool SetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize);

        int GetLastWin32Error();

        int QueryIoPriority(SafeProcessHandle process, ref int priority, out uint returnLength)
        {
            returnLength = 0;
            return unchecked((int)0xC0000002);
        }

        int SetIoPriority(SafeProcessHandle process, ref int priority) => unchecked((int)0xC0000002);
    }

    public sealed class ProcessMemoryPriorityNativeApi : IProcessMemoryPriorityNativeApi
    {
        public static ProcessMemoryPriorityNativeApi Instance { get; } = new();

        private ProcessMemoryPriorityNativeApi()
        {
        }

        public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(6, 2);

        public SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId)
        {
            return ProcessMemoryPriorityNativeMethods.OpenProcess(access, inheritHandle, processId);
        }

        public bool GetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize)
        {
            return ProcessMemoryPriorityNativeMethods.GetProcessInformation(
                process,
                processInformationClass,
                ref processInformation,
                processInformationSize);
        }

        public bool SetProcessInformation(
            SafeProcessHandle process,
            ProcessInformationClass processInformationClass,
            ref MemoryPriorityInformation processInformation,
            uint processInformationSize)
        {
            return ProcessMemoryPriorityNativeMethods.SetProcessInformation(
                process,
                processInformationClass,
                ref processInformation,
                processInformationSize);
        }

        public int GetLastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }

        public int QueryIoPriority(SafeProcessHandle process, ref int priority, out uint returnLength) =>
            ProcessMemoryPriorityNativeMethods.NtQueryInformationProcess(
                process,
                ProcessMemoryPriorityNativeMethods.ProcessIoPriority,
                ref priority,
                sizeof(int),
                out returnLength);

        public int SetIoPriority(SafeProcessHandle process, ref int priority) =>
            ProcessMemoryPriorityNativeMethods.NtSetInformationProcess(
                process,
                ProcessMemoryPriorityNativeMethods.ProcessIoPriority,
                ref priority,
                sizeof(int));
    }

    public enum ProcessInformationClass
    {
        ProcessMemoryPriority = 0,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryPriorityInformation
    {
        public uint MemoryPriority;
    }
}
