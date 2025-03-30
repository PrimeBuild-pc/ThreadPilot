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
    /// Implementation of the system information service
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        // Performance counters for tracking CPU usage
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter[] _coreCounters;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfoService()
        {
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            
            // Get the number of logical cores
            int coreCount = Environment.ProcessorCount;
            
            // Initialize core counters
            _coreCounters = new PerformanceCounter[coreCount];
            for (int i = 0; i < coreCount; i++)
            {
                _coreCounters[i] = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
            }
            
            // Take initial readings (this will reset the counters)
            _cpuCounter.NextValue();
            foreach (var counter in _coreCounters)
            {
                counter.NextValue();
            }
        }
        
        /// <summary>
        /// Get system information
        /// </summary>
        public SystemInfo GetSystemInfo()
        {
            try
            {
                // Create system info object
                var systemInfo = new SystemInfo
                {
                    CaptureTime = DateTime.Now,
                    CpuName = GetCpuName(),
                    PhysicalCores = GetPhysicalCoreCount(),
                    LogicalCores = Environment.ProcessorCount,
                    CpuBaseClockMhz = GetCpuBaseClockMhz(),
                    CpuCurrentClockMhz = GetCpuCurrentClockMhz(),
                    CpuLoad = GetCpuUsage(),
                    CpuTemperature = GetCpuTemperature(),
                    TotalRamGb = GetTotalMemoryGb(),
                    AvailableRamGb = GetAvailableMemoryGb(),
                    OperatingSystem = GetOperatingSystemInfo(),
                    Uptime = GetSystemUptime()
                };
                
                // Calculate RAM usage percentage
                systemInfo.RamUsagePercent = Math.Round(
                    (1 - (systemInfo.AvailableRamGb / systemInfo.TotalRamGb)) * 100, 1);
                
                // Get core loads
                systemInfo.CoreLoads = GetCoreUsages().ToList();
                
                return systemInfo;
            }
            catch (Exception ex)
            {
                // Return a basic system info object with error information
                return new SystemInfo
                {
                    CpuName = "Error getting CPU info: " + ex.Message,
                    LogicalCores = Environment.ProcessorCount
                };
            }
        }
        
        /// <summary>
        /// Get CPU usage
        /// </summary>
        public double GetCpuUsage()
        {
            try
            {
                return Math.Round(_cpuCounter.NextValue(), 1);
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get available memory in GB
        /// </summary>
        public double GetAvailableMemoryGb()
        {
            try
            {
                using var pc = new PerformanceCounter("Memory", "Available Bytes");
                return Math.Round(pc.NextValue() / 1024 / 1024 / 1024, 2);
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get CPU temperature in Celsius
        /// </summary>
        public double GetCpuTemperature()
        {
            try
            {
                // TODO: Implement actual temperature reading
                // This requires hardware-specific code or WMI queries that may not work on all systems
                Random random = new Random();
                return 45 + random.NextDouble() * 20; // Simulate between 45-65°C
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get core usage for each CPU core
        /// </summary>
        public double[] GetCoreUsages()
        {
            try
            {
                double[] usages = new double[_coreCounters.Length];
                
                for (int i = 0; i < _coreCounters.Length; i++)
                {
                    usages[i] = Math.Round(_coreCounters[i].NextValue(), 1);
                }
                
                return usages;
            }
            catch
            {
                return new double[Environment.ProcessorCount];
            }
        }
        
        /// <summary>
        /// Get CPU name
        /// </summary>
        private string GetCpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"].ToString() ?? "Unknown CPU";
                }
                
                return "Unknown CPU";
            }
            catch
            {
                return "Unknown CPU";
            }
        }
        
        /// <summary>
        /// Get physical core count
        /// </summary>
        private int GetPhysicalCoreCount()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT NumberOfCores FROM Win32_Processor");
                int coreCount = 0;
                
                foreach (var obj in searcher.Get())
                {
                    coreCount += int.Parse(obj["NumberOfCores"].ToString() ?? "0");
                }
                
                return coreCount > 0 ? coreCount : Environment.ProcessorCount;
            }
            catch
            {
                return Environment.ProcessorCount;
            }
        }
        
        /// <summary>
        /// Get CPU base clock speed in MHz
        /// </summary>
        private double GetCpuBaseClockMhz()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return double.Parse(obj["CurrentClockSpeed"].ToString() ?? "0");
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get CPU current clock speed in MHz
        /// </summary>
        private double GetCpuCurrentClockMhz()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT CurrentClockSpeed FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return double.Parse(obj["CurrentClockSpeed"].ToString() ?? "0");
                }
                
                return 0;
            }
            catch
            {
                // In a real implementation, this would try to get the actual current clock speed,
                // which can vary from the base clock due to turbo boost, etc.
                return GetCpuBaseClockMhz();
            }
        }
        
        /// <summary>
        /// Get total memory in GB
        /// </summary>
        private double GetTotalMemoryGb()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    return Math.Round(
                        long.Parse(obj["TotalPhysicalMemory"].ToString() ?? "0") / 1024.0 / 1024.0 / 1024.0, 2);
                }
                
                return 0;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Get operating system information
        /// </summary>
        private string GetOperatingSystemInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    string caption = obj["Caption"].ToString() ?? "Unknown OS";
                    string version = obj["Version"].ToString() ?? "";
                    
                    return $"{caption} ({version})";
                }
                
                return "Unknown OS";
            }
            catch
            {
                return "Unknown OS";
            }
        }
        
        /// <summary>
        /// Get system uptime
        /// </summary>
        private TimeSpan GetSystemUptime()
        {
            try
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount64);
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }
    }
}