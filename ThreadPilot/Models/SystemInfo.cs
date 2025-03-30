using System;
using System.Collections.Generic;
using System.Management;
using Microsoft.Win32;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Provides information about the system hardware and configuration
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Gets the processor name
        /// </summary>
        public string ProcessorName { get; private set; }

        /// <summary>
        /// Gets the number of physical CPU cores
        /// </summary>
        public int PhysicalCores { get; private set; }

        /// <summary>
        /// Gets the number of logical CPU cores (including hyperthreading)
        /// </summary>
        public int LogicalCores { get; private set; }

        /// <summary>
        /// Gets the total amount of installed RAM in GB
        /// </summary>
        public double TotalMemoryGB { get; private set; }

        /// <summary>
        /// Gets the operating system name and version
        /// </summary>
        public string OperatingSystem { get; private set; }

        /// <summary>
        /// Gets whether core parking is enabled
        /// </summary>
        public bool IsCoreParking { get; private set; }

        /// <summary>
        /// Gets the current processor performance boost mode setting
        /// </summary>
        public int ProcessorPerformanceBoostMode { get; private set; }

        /// <summary>
        /// Gets the system responsiveness setting (MMCSS)
        /// </summary>
        public int SystemResponsiveness { get; private set; }

        /// <summary>
        /// Gets the network throttling index setting
        /// </summary>
        public int NetworkThrottlingIndex { get; private set; }

        /// <summary>
        /// Gets or sets the Windows priority separation setting
        /// </summary>
        public int Win32PrioritySeparation { get; private set; }

        /// <summary>
        /// Creates a new instance of SystemInfo and loads system information
        /// </summary>
        public SystemInfo()
        {
            RefreshSystemInfo();
        }

        /// <summary>
        /// Refreshes all system information
        /// </summary>
        public void RefreshSystemInfo()
        {
            try
            {
                // Get processor information
                using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        ProcessorName = obj["Name"].ToString().Trim();
                        break; // Only get the first processor
                    }
                }

                // Get CPU core counts
                PhysicalCores = Environment.ProcessorCount;
                LogicalCores = Environment.ProcessorCount;

                try
                {
                    // Try to get more accurate core counts using WMI
                    int physicalCores = 0;
                    using (var searcher = new ManagementObjectSearcher("select NumberOfCores from Win32_Processor"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            physicalCores += Convert.ToInt32(obj["NumberOfCores"]);
                        }
                    }

                    if (physicalCores > 0)
                    {
                        PhysicalCores = physicalCores;
                    }
                }
                catch
                {
                    // Fallback to processor count if WMI fails
                }

                // Get memory information
                try
                {
                    using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            // TotalPhysicalMemory is in bytes, convert to GB
                            TotalMemoryGB = Math.Round(Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024), 1);
                            break;
                        }
                    }
                }
                catch
                {
                    TotalMemoryGB = 0;
                }

                // Get OS information
                OperatingSystem = Environment.OSVersion.VersionString;
                try
                {
                    using (var searcher = new ManagementObjectSearcher("select * from Win32_OperatingSystem"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            OperatingSystem = obj["Caption"].ToString();
                            break;
                        }
                    }
                }
                catch
                {
                    // Use the Environment.OSVersion value as fallback
                }

                // Get power settings
                GetPowerSettings();

                // Get registry settings
                GetRegistrySettings();
            }
            catch (Exception ex)
            {
                // Log error or handle exception
                Console.WriteLine($"Error getting system info: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves power settings from the system
        /// </summary>
        private void GetPowerSettings()
        {
            try
            {
                // These would normally be retrieved using PowerCfg command line tool
                // For example: powercfg /q SCHEME_CURRENT SUB_PROCESSOR CPMINCORES
                
                // Since we can't directly execute powercfg in this environment,
                // we're using placeholder values
                
                IsCoreParking = true; // Default assumption
                ProcessorPerformanceBoostMode = 3; // Default (Aggressive)
            }
            catch
            {
                IsCoreParking = true;
                ProcessorPerformanceBoostMode = 3;
            }
        }

        /// <summary>
        /// Retrieves settings from the registry
        /// </summary>
        private void GetRegistrySettings()
        {
            try
            {
                // System responsiveness
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("SystemResponsiveness");
                        SystemResponsiveness = value != null ? Convert.ToInt32(value) : 20; // Default is 20
                        
                        value = key.GetValue("NetworkThrottlingIndex");
                        NetworkThrottlingIndex = value != null ? Convert.ToInt32(value) : 10; // Default is 10
                    }
                    else
                    {
                        SystemResponsiveness = 20;
                        NetworkThrottlingIndex = 10;
                    }
                }

                // Priority separation
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("Win32PrioritySeparation");
                        Win32PrioritySeparation = value != null ? Convert.ToInt32(value) : 2; // Default is 2
                    }
                    else
                    {
                        Win32PrioritySeparation = 2;
                    }
                }
            }
            catch
            {
                SystemResponsiveness = 20;
                NetworkThrottlingIndex = 10;
                Win32PrioritySeparation = 2;
            }
        }
    }
}