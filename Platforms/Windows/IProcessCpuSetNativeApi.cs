namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    internal interface IProcessCpuSetNativeApi
    {
        SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId);

        bool SetProcessDefaultCpuSets(SafeProcessHandle process, uint[]? cpuSetIds, uint cpuSetIdCount);

        bool GetProcessTimes(
            SafeProcessHandle process,
            out FILETIME creationTime,
            out FILETIME exitTime,
            out FILETIME kernelTime,
            out FILETIME userTime);

        bool GetSystemCpuSetInformation(
            IntPtr information,
            uint bufferLength,
            ref uint returnedLength,
            SafeProcessHandle process,
            uint flags);

        int GetLastWin32Error();
    }

    internal sealed class ProcessCpuSetNativeApi : IProcessCpuSetNativeApi
    {
        public static ProcessCpuSetNativeApi Instance { get; } = new();

        private ProcessCpuSetNativeApi()
        {
        }

        public SafeProcessHandle OpenProcess(ProcessAccessFlags access, bool inheritHandle, uint processId)
        {
            return CpuSetNativeMethods.OpenProcess(access, inheritHandle, processId);
        }

        public bool SetProcessDefaultCpuSets(SafeProcessHandle process, uint[]? cpuSetIds, uint cpuSetIdCount)
        {
            return CpuSetNativeMethods.SetProcessDefaultCpuSets(process, cpuSetIds, cpuSetIdCount);
        }

        public bool GetProcessTimes(
            SafeProcessHandle process,
            out FILETIME creationTime,
            out FILETIME exitTime,
            out FILETIME kernelTime,
            out FILETIME userTime)
        {
            return CpuSetNativeMethods.GetProcessTimes(process, out creationTime, out exitTime, out kernelTime, out userTime);
        }

        public bool GetSystemCpuSetInformation(
            IntPtr information,
            uint bufferLength,
            ref uint returnedLength,
            SafeProcessHandle process,
            uint flags)
        {
            return CpuSetNativeMethods.GetSystemCpuSetInformation(information, bufferLength, ref returnedLength, process, flags);
        }

        public int GetLastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }
    }
}
