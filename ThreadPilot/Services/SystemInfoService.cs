using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of system information service
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        private readonly INotificationService _notificationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfoService()
        {
            _notificationService = ServiceLocator.Get<INotificationService>();
        }
        
        /// <summary>
        /// Get system information
        /// </summary>
        /// <returns>System information</returns>
        public SystemInfo GetSystemInfo()
        {
            var systemInfo = new SystemInfo();
            
            try
            {
                // Get OS information
                systemInfo.OperatingSystem = Environment.OSVersion.VersionString;
                systemInfo.OsVersion = GetOSName();
                
                // Get processor information
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        systemInfo.ProcessorName = obj["Name"]?.ToString() ?? "Unknown";
                        systemInfo.ProcessorCores = Convert.ToInt32(obj["NumberOfCores"]);
                        systemInfo.ProcessorLogicalCores = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                        break; // Just take the first CPU
                    }
                }
                
                // Calculate performance and efficiency cores (simplified)
                // In a real implementation, this would use more detailed CPU information
                if (IsHybridProcessor(systemInfo.ProcessorName))
                {
                    // Estimate for common hybrid processors
                    systemInfo.PerformanceCoreCount = systemInfo.ProcessorCores / 2;
                    systemInfo.EfficiencyCoreCount = systemInfo.ProcessorCores - systemInfo.PerformanceCoreCount;
                    systemInfo.SupportsThreadDirector = true;
                }
                else
                {
                    systemInfo.PerformanceCoreCount = systemInfo.ProcessorCores;
                    systemInfo.EfficiencyCoreCount = 0;
                    systemInfo.SupportsThreadDirector = false;
                }
                
                // Get CPU usage
                systemInfo.CpuUsage = GetCpuUsage();
                
                // Get memory information
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        long totalMemoryKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                        long freeMemoryKB = Convert.ToInt64(obj["FreePhysicalMemory"]);
                        
                        systemInfo.TotalMemoryGB = totalMemoryKB / 1024.0 / 1024.0;
                        systemInfo.AvailableMemoryGB = freeMemoryKB / 1024.0 / 1024.0;
                        break;
                    }
                }
                
                // Get system uptime
                systemInfo.UpTime = GetSystemUpTime();
                
                // Get power information
                systemInfo.IsOnBattery = IsOnBattery();
                systemInfo.BatteryChargePercent = GetBatteryChargePercent();
                systemInfo.CurrentPowerProfile = GetCurrentPowerProfile();
                
                // Get GPU information
                systemInfo.GpuName = GetGpuName();
                systemInfo.GpuUsage = GetGpuUsage();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system info: {ex.Message}");
                _notificationService.ShowError($"Error getting system information: {ex.Message}");
            }
            
            return systemInfo;
        }
        
        /// <summary>
        /// Get CPU cores
        /// </summary>
        /// <returns>List of CPU cores</returns>
        public List<CpuCore> GetCpuCores()
        {
            var cores = new List<CpuCore>();
            
            try
            {
                // Get logical processors
                int logicalProcessors = Environment.ProcessorCount;
                
                // Create core objects
                for (int i = 0; i < logicalProcessors; i++)
                {
                    bool isLogical = i % 2 == 1; // Simplified - assuming every second is a logical core
                    int physicalCore = i / 2;
                    
                    // Simplified E-Core detection (in reality this would be more complex)
                    // For demonstration, treat later cores as E-cores in hybrid CPUs
                    bool isEfficiencyCore = false;
                    
                    // Get processor name to check if it's a hybrid CPU
                    string processorName = string.Empty;
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            processorName = obj["Name"]?.ToString() ?? string.Empty;
                            break;
                        }
                    }
                    
                    if (IsHybridProcessor(processorName))
                    {
                        // Simplified logic: half the physical cores are P-cores, half are E-cores
                        int totalPhysicalCores = logicalProcessors / 2;
                        int performanceCores = totalPhysicalCores / 2;
                        
                        isEfficiencyCore = (physicalCore >= performanceCores) && !isLogical;
                    }
                    
                    var core = new CpuCore
                    {
                        Index = i,
                        PhysicalCore = physicalCore,
                        IsLogicalCore = isLogical,
                        IsEfficiencyCore = isEfficiencyCore,
                        BaseClockMHz = 2500, // Placeholder values
                        CurrentClockMHz = 3500,
                        MaxClockMHz = 4500,
                        CpuUsage = GetCoreUsage(i),
                        PowerUsageWatts = 5.0,
                        TemperatureCelsius = 40.0 + (GetCoreUsage(i) / 5.0),
                        IsParked = false
                    };
                    
                    cores.Add(core);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU cores: {ex.Message}");
                _notificationService.ShowError($"Error getting CPU cores: {ex.Message}");
            }
            
            return cores;
        }
        
        /// <summary>
        /// Reset CPU cores to default settings
        /// </summary>
        /// <returns>True if successful</returns>
        public bool ResetCpuCores()
        {
            try
            {
                // In a real implementation, this would reset core parking and other settings
                
                // For this demo, we'll just simulate success
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting CPU cores: {ex.Message}");
                _notificationService.ShowError($"Error resetting CPU cores: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Optimize CPU cores for best performance
        /// </summary>
        /// <returns>True if successful</returns>
        public bool OptimizeCpuCores()
        {
            try
            {
                // In a real implementation, this would:
                // 1. Unpark all cores
                // 2. Set optimal power settings
                // 3. Optimize core scheduling
                
                // For this demo, we'll just simulate success
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error optimizing CPU cores: {ex.Message}");
                _notificationService.ShowError($"Error optimizing CPU cores: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Check if the processor is a hybrid processor
        /// </summary>
        /// <param name="processorName">Processor name</param>
        /// <returns>True if hybrid</returns>
        private bool IsHybridProcessor(string processorName)
        {
            // Check if the processor name contains indicators of hybrid architecture
            return processorName.Contains("12th Gen") || 
                   processorName.Contains("13th Gen") || 
                   processorName.Contains("14th Gen") ||
                   processorName.Contains("Hybrid") ||
                   processorName.Contains("Alder Lake") ||
                   processorName.Contains("Raptor Lake") ||
                   processorName.Contains("Meteor Lake");
        }
        
        /// <summary>
        /// Get the OS name
        /// </summary>
        /// <returns>OS name</returns>
        private string GetOSName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Caption"]?.ToString() ?? "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting OS name: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// Get the CPU usage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        private double GetCpuUsage()
        {
            try
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                counter.NextValue(); // First call always returns 0
                System.Threading.Thread.Sleep(100); // Wait a moment to get a valid reading
                return counter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU usage: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Get the usage of a specific core
        /// </summary>
        /// <param name="coreIndex">Core index</param>
        /// <returns>Core usage percentage</returns>
        private double GetCoreUsage(int coreIndex)
        {
            try
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", coreIndex.ToString());
                counter.NextValue(); // First call always returns 0
                System.Threading.Thread.Sleep(50); // Wait a moment to get a valid reading
                return counter.NextValue();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting core usage: {ex.Message}");
                
                // Return a simulated value based on core index
                Random random = new Random();
                return 10.0 + (coreIndex % 4) * 10 + random.Next(20);
            }
        }
        
        /// <summary>
        /// Get the system uptime
        /// </summary>
        /// <returns>System uptime</returns>
        private TimeSpan GetSystemUpTime()
        {
            try
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system uptime: {ex.Message}");
                return TimeSpan.Zero;
            }
        }
        
        /// <summary>
        /// Check if the system is running on battery
        /// </summary>
        /// <returns>True if on battery</returns>
        private bool IsOnBattery()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        // If we get any battery object, the system has a battery
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking battery status: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Get the battery charge percentage
        /// </summary>
        /// <returns>Battery charge percentage or null if not available</returns>
        private double? GetBatteryChargePercent()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining FROM Win32_Battery"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        if (obj["EstimatedChargeRemaining"] != null)
                        {
                            return Convert.ToDouble(obj["EstimatedChargeRemaining"]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting battery charge: {ex.Message}");
            }
            
            return null;
        }
        
        /// <summary>
        /// Get the current power profile
        /// </summary>
        /// <returns>Power profile name</returns>
        private string GetCurrentPowerProfile()
        {
            try
            {
                // In a real implementation, this would use the Windows power API
                // For this demo, we'll just return a placeholder
                return "Balanced";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power profile: {ex.Message}");
                return "Unknown";
            }
        }
        
        /// <summary>
        /// Get the GPU name
        /// </summary>
        /// <returns>GPU name</returns>
        private string GetGpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Name"]?.ToString() ?? "Unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting GPU name: {ex.Message}");
            }
            
            return "Unknown";
        }
        
        /// <summary>
        /// Get the GPU usage
        /// </summary>
        /// <returns>GPU usage percentage</returns>
        private double GetGpuUsage()
        {
            try
            {
                // In a real implementation, this would use GPU-specific counters
                // For this demo, we'll just return a random value
                Random random = new Random();
                return random.Next(5, 60);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting GPU usage: {ex.Message}");
                return 0;
            }
        }
    }
}