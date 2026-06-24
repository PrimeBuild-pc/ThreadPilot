/*
 * ThreadPilot - Windows process memory priority P/Invoke declarations.
 */
namespace ThreadPilot.Platforms.Windows
{
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    internal static partial class ProcessMemoryPriorityNativeMethods
    {
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
    }
}
