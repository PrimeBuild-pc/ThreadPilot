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
