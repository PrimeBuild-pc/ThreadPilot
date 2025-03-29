using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ThreadPilot.Models;

namespace ThreadPilot.Helpers
{
    public static class CoreAffinityHelper
    {
        /// <summary>
        /// Gets information about all available CPU cores.
        /// </summary>
        public static List<CoreInfo> GetCoreInfo()
        {
            var result = new List<CoreInfo>();
            var processorCount = Environment.ProcessorCount;
            
            for (int i = 0; i < processorCount; i++)
            {
                result.Add(new CoreInfo
                {
                    CoreNumber = i,
                    IsSelected = false
                });
            }
            
            return result;
        }

        /// <summary>
        /// Updates core selection based on affinity mask.
        /// </summary>
        public static void UpdateCoreSelection(List<CoreInfo> cores, long affinityMask)
        {
            for (int i = 0; i < cores.Count; i++)
            {
                // Check if the bit at position i is set in the affinity mask
                cores[i].IsSelected = ((affinityMask >> i) & 1) == 1;
            }
        }

        /// <summary>
        /// Calculates affinity mask from selected cores.
        /// </summary>
        public static long CalculateAffinityMask(IEnumerable<CoreInfo> cores)
        {
            long mask = 0;
            int i = 0;
            
            foreach (var core in cores)
            {
                if (core.IsSelected)
                {
                    // Set the bit at position of the core number
                    mask |= (1L << i);
                }
                i++;
            }
            
            return mask;
        }

        /// <summary>
        /// Checks if a processor is physically present and enabled.
        /// </summary>
        public static bool IsProcessorPresent(int processorIndex)
        {
            try
            {
                // Get system info
                var systemInfo = new NativeMethods.SYSTEM_INFO();
                NativeMethods.GetSystemInfo(out systemInfo);
                
                // Check if the processor is in the active processor mask
                long activeMask = systemInfo.dwActiveProcessorMask.ToInt64();
                return ((activeMask >> processorIndex) & 1) == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking processor presence: {ex.Message}");
                return processorIndex < Environment.ProcessorCount;
            }
        }

        /// <summary>
        /// Gets the total number of physical cores (not logical processors).
        /// </summary>
        public static int GetPhysicalProcessorCount()
        {
            try
            {
                // This is a simplified approach - in a real app you'd want to use
                // WMI to more accurately determine physical vs. logical cores
                int processorCount = Environment.ProcessorCount;
                bool isHyperThreadingEnabled = IsHyperThreadingEnabled();
                
                if (isHyperThreadingEnabled)
                {
                    return processorCount / 2;
                }
                
                return processorCount;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting physical processor count: {ex.Message}");
                return Environment.ProcessorCount;
            }
        }

        /// <summary>
        /// Attempts to determine if hyperthreading is enabled.
        /// </summary>
        public static bool IsHyperThreadingEnabled()
        {
            try
            {
                // This is a simplified check - in a real app you'd use a more robust method
                // using WMI or other system information APIs
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                
                foreach (var obj in searcher.Get())
                {
                    int cores = int.Parse(obj["NumberOfCores"].ToString());
                    int logical = int.Parse(obj["NumberOfLogicalProcessors"].ToString());
                    
                    // If there are more logical processors than cores, hyperthreading is enabled
                    return logical > cores;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking hyperthreading: {ex.Message}");
                return false;
            }
        }
    }
}
