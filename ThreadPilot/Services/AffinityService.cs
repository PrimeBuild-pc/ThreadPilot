using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ThreadPilot.Helpers;

namespace ThreadPilot.Services
{
    public class AffinityService
    {
        public void SetAffinity(Process process, long affinityMask)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            // Make sure at least one CPU is selected
            if (affinityMask == 0)
                throw new ArgumentException("At least one CPU core must be selected.");

            try
            {
                process.ProcessorAffinity = new IntPtr(affinityMask);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set affinity for process {process.Id}: {ex.Message}");
                throw;
            }
        }

        public void SetAffinity(int processId, long affinityMask)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                SetAffinity(process, affinityMask);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get process {processId}: {ex.Message}");
                throw new InvalidOperationException($"Failed to access process with ID {processId}. It may have terminated or requires elevated privileges.");
            }
        }

        // For processes that cannot be accessed through the Process class (like system processes),
        // we need to use the Windows API directly
        public bool TrySetAffinityNative(int processId, long affinityMask)
        {
            IntPtr processHandle = IntPtr.Zero;
            
            try
            {
                processHandle = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_SET_INFORMATION | NativeMethods.PROCESS_QUERY_INFORMATION,
                    false,
                    processId);

                if (processHandle == IntPtr.Zero)
                {
                    Debug.WriteLine($"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                IntPtr affinityMaskPtr = new IntPtr(affinityMask);
                if (!NativeMethods.SetProcessAffinityMask(processHandle, affinityMaskPtr))
                {
                    Debug.WriteLine($"Failed to set affinity for process {processId}. Error: {Marshal.GetLastWin32Error()}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception setting affinity for process {processId}: {ex.Message}");
                return false;
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(processHandle);
                }
            }
        }

        public long GetSystemAffinityMask()
        {
            IntPtr processHandle = NativeMethods.GetCurrentProcess();
            IntPtr processAffinityMask;
            IntPtr systemAffinityMask;

            if (!NativeMethods.GetProcessAffinityMask(processHandle, out processAffinityMask, out systemAffinityMask))
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to get system affinity mask. Error: {error}");
                return (1L << Environment.ProcessorCount) - 1; // Fallback: all cores
            }

            return systemAffinityMask.ToInt64();
        }
    }
}
