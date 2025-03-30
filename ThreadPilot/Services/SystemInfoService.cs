using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for retrieving system information
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="SystemInfoService"/> class
        /// </summary>
        public SystemInfoService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                // First call to NextValue() always returns 0, so call it now and discard the result
                _cpuCounter.NextValue();
                _ramCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing performance counters: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Get system information
        /// </summary>
        /// <returns>System information</returns>
        public SystemInfo GetSystemInfo()
        {
            var systemInfo = new SystemInfo
            {
                OperatingSystem = GetOperatingSystemName(),
                CpuModel = GetCpuModel(),
                PhysicalCores = Environment.ProcessorCount / 2, // Simplified, not always accurate
                LogicalCores = Environment.ProcessorCount,
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsagePercent = GetMemoryUsage(),
                TotalRam = GetTotalRam(),
                AvailableRam = (long)_ramCounter.NextValue(),
                ActivePowerPlanName = GetActivePowerPlanName(),
                ActivePowerPlanGuid = GetActivePowerPlanGuid(),
                Cores = GetCpuCoreInfo(),
                CpuTemperature = 0, // Not implemented
                GpuModel = GetGpuModel(),
                GpuTemperature = 0, // Not implemented
                GpuUsagePercent = 0, // Not implemented
                GpuMemoryUsagePercent = 0, // Not implemented
                RunningProcesses = Process.GetProcesses().Length
            };
            
            systemInfo.MemoryUsagePercent = 100f - ((float)systemInfo.AvailableRam / systemInfo.TotalRam * 100f);
            
            return systemInfo;
        }
        
        /// <summary>
        /// Retrieve the current CPU usage percentage
        /// </summary>
        /// <returns>CPU usage percentage (0-100)</returns>
        public float GetCpuUsage()
        {
            try
            {
                return _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU usage: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Retrieve the current memory usage percentage
        /// </summary>
        /// <returns>Memory usage percentage (0-100)</returns>
        public float GetMemoryUsage()
        {
            try
            {
                long availableRam = (long)_ramCounter.NextValue();
                long totalRam = GetTotalRam();
                
                return 100f - ((float)availableRam / totalRam * 100f);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting memory usage: {ex.Message}");
                return 0;
            }
        }
        
        private string GetOperatingSystemName()
        {
            try
            {
                using var registryKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                
                if (registryKey != null)
                {
                    string productName = registryKey.GetValue("ProductName") as string;
                    string currentBuild = registryKey.GetValue("CurrentBuild") as string;
                    string displayVersion = registryKey.GetValue("DisplayVersion") as string;
                    
                    if (!string.IsNullOrEmpty(displayVersion))
                    {
                        return $"{productName} ({displayVersion}) Build {currentBuild}";
                    }
                    
                    return $"{productName} Build {currentBuild}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting OS info: {ex.Message}");
            }
            
            return Environment.OSVersion.VersionString;
        }
        
        private string GetCpuModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"].ToString().Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU model: {ex.Message}");
            }
            
            return "Unknown CPU";
        }
        
        private string GetGpuModel()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"].ToString().Trim();
                    
                    // Filter out non-GPU controllers
                    if (!name.Contains("Remote Desktop", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("RDP", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting GPU model: {ex.Message}");
            }
            
            return "Unknown GPU";
        }
        
        private long GetTotalRam()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                
                foreach (var obj in searcher.Get())
                {
                    ulong totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                    return (long)(totalBytes / (1024 * 1024)); // Convert to MB
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting total RAM: {ex.Message}");
            }
            
            return 0;
        }
        
        private string GetActivePowerPlanName()
        {
            try
            {
                Guid activePlanGuid = GetActivePowerPlanGuid();
                
                if (activePlanGuid != Guid.Empty)
                {
                    return GetPowerPlanName(activePlanGuid);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active power plan name: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        private Guid GetActivePowerPlanGuid()
        {
            try
            {
                // Get the active power plan GUID
                IntPtr activeGuidPtr = IntPtr.Zero;
                uint activeGuidSize = 16; // Size of a GUID
                
                uint result = PowerGetActiveScheme(IntPtr.Zero, ref activeGuidPtr);
                
                if (result == 0 && activeGuidPtr != IntPtr.Zero)
                {
                    Guid activeGuid = Marshal.PtrToStructure<Guid>(activeGuidPtr);
                    LocalFree(activeGuidPtr);
                    
                    return activeGuid;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active power plan GUID: {ex.Message}");
            }
            
            return Guid.Empty;
        }
        
        private string GetPowerPlanName(Guid guid)
        {
            try
            {
                IntPtr friendlyNamePtr = IntPtr.Zero;
                uint friendlyNameSize = 0;
                
                uint result = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, friendlyNamePtr, ref friendlyNameSize);
                
                if (result == 0 && friendlyNameSize > 0)
                {
                    friendlyNamePtr = Marshal.AllocHGlobal((int)friendlyNameSize);
                    
                    if (friendlyNamePtr != IntPtr.Zero)
                    {
                        result = PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, friendlyNamePtr, ref friendlyNameSize);
                        
                        if (result == 0)
                        {
                            string friendlyName = Marshal.PtrToStringUni(friendlyNamePtr);
                            Marshal.FreeHGlobal(friendlyNamePtr);
                            
                            return friendlyName;
                        }
                        
                        Marshal.FreeHGlobal(friendlyNamePtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power plan name: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        private List<CpuCore> GetCpuCoreInfo()
        {
            var cores = new List<CpuCore>();
            
            try
            {
                // Get logical processor info
                using var coreSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                var coreInfo = coreSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                
                if (coreInfo != null)
                {
                    // Get the number of cores
                    int physicalCores = Convert.ToInt32(coreInfo["NumberOfCores"]);
                    int logicalCores = Convert.ToInt32(coreInfo["NumberOfLogicalProcessors"]);
                    
                    // Get usage for each core
                    var performanceCounters = new List<PerformanceCounter>();
                    
                    for (int i = 0; i < logicalCores; i++)
                    {
                        var counter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                        counter.NextValue(); // First call to NextValue() always returns 0
                        performanceCounters.Add(counter);
                    }
                    
                    // Wait a bit for the counters to collect data
                    System.Threading.Thread.Sleep(100);
                    
                    for (int i = 0; i < logicalCores; i++)
                    {
                        cores.Add(new CpuCore
                        {
                            CoreNumber = i / (logicalCores / physicalCores),
                            ThreadNumber = i,
                            IsPhysicalCore = i % (logicalCores / physicalCores) == 0,
                            UsagePercent = performanceCounters[i].NextValue(),
                            CurrentFrequency = 0, // Not implemented
                            MaxFrequency = 0, // Not implemented
                            MinFrequency = 0, // Not implemented
                            Temperature = 0, // Not implemented
                            Voltage = 0, // Not implemented
                            Power = 0, // Not implemented
                            IsParked = false // Not implemented
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU core info: {ex.Message}");
            }
            
            return cores;
        }
        
        #region Native methods
        
        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, ref IntPtr ActivePolicyGuid);
        
        [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
        private static extern uint PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
        
        #endregion
    }
}