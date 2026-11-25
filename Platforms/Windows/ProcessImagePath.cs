namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// Reads a process's executable path with QueryFullProcessImageName and PROCESS_QUERY_LIMITED_INFORMATION.
    /// Process.MainModule needs PROCESS_VM_READ to walk the module list in the target's memory, which anti-cheats
    /// (BattlEye on Arma Reforger, for one) refuse, so their games looked pathless and were never detected.
    /// </summary>
    internal static partial class ProcessImagePath
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int ErrorInsufficientBuffer = 122;
        private const int MaxPathChars = 32768;

        /// <summary>Gets the Win32 path of the process image, or null when Windows does not give it.</summary>
        public static string? TryGet(int processId)
        {
            if (processId <= 0)
            {
                return null;
            }

            using var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)processId);
            if (handle.IsInvalid)
            {
                return null;
            }

            // Almost every path fits in 1024 chars; long-path installs get one retry with the largest size.
            foreach (var capacity in new[] { 1024, MaxPathChars })
            {
                var buffer = new char[capacity];
                var length = buffer.Length;
                if (QueryFullProcessImageName(handle, 0, buffer, ref length))
                {
                    return length > 0 ? new string(buffer, 0, length) : null;
                }

                if (Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer)
                {
                    return null;
                }
            }

            return null;
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

        [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref int size);
    }
}
