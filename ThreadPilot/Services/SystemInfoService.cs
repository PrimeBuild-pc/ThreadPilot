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
        // Windows API imports
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }
        
        // Performance counters
        private readonly PerformanceCounter _cpuCounter;
        private readonly Dictionary<int, PerformanceCounter> _coreCpuCounters = new Dictionary<int, PerformanceCounter>();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfoService()
        {
            // Initialize performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call will always return 0
                
                var coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    var coreCounter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                    coreCounter.NextValue(); // First call will always return 0
                    _coreCpuCounters.Add(i, coreCounter);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing performance counters: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets system information
        /// </summary>
        /// <returns>System information</returns>
        public SystemInfo GetSystemInfo()
        {
            var cpuUsage = GetCpuUsage();
            var memoryUsage = GetMemoryUsage();
            
            return new SystemInfo
            {
                ProcessorName = GetProcessorName(),
                CpuUsagePercentage = cpuUsage,
                MemoryUsageMB = memoryUsage.Used,
                TotalMemoryMB = memoryUsage.Total,
                LogicalProcessorCount = GetLogicalProcessorCount(),
                PhysicalProcessorCount = GetPhysicalProcessorCount(),
                PerformanceCoreCount = GetPerformanceCoreCount() ?? 0,
                EfficiencyCoreCount = GetEfficiencyCoreCount() ?? 0,
                OperatingSystem = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName
            };
        }
        
        /// <summary>
        /// Gets CPU cores information
        /// </summary>
        /// <returns>List of CPU cores</returns>
        public IEnumerable<CpuCore> GetCpuCores()
        {
            var cores = new List<CpuCore>();
            var logicalProcessorCount = GetLogicalProcessorCount();
            var performanceCoreCount = GetPerformanceCoreCount() ?? logicalProcessorCount;
            
            for (int i = 0; i < logicalProcessorCount; i++)
            {
                float usagePercentage = 0;
                if (_coreCpuCounters.TryGetValue(i, out var counter))
                {
                    usagePercentage = counter.NextValue();
                }
                
                cores.Add(new CpuCore
                {
                    Id = i,
                    Number = i,
                    ProcessorId = 0, // Default to processor 0
                    NumaNode = 0, // Default to NUMA node 0
                    UsagePercentage = usagePercentage,
                    FrequencyMHz = 0, // To be implemented
                    IsPerformanceCore = i < performanceCoreCount
                });
            }
            
            return cores;
        }
        
        /// <summary>
        /// Gets processor name
        /// </summary>
        /// <returns>Processor name</returns>
        public string GetProcessorName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Name"].ToString().Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processor name: {ex.Message}");
            }
            
            return "Unknown Processor";
        }
        
        /// <summary>
        /// Gets total CPU usage percentage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        public float GetCpuUsage()
        {
            try
            {
                return _cpuCounter?.NextValue() ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU usage: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Gets memory usage information
        /// </summary>
        /// <returns>Memory usage information (used/total in MB)</returns>
        public (ulong Used, ulong Total) GetMemoryUsage()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    var totalMB = memStatus.ullTotalPhys / (1024 * 1024);
                    var availableMB = memStatus.ullAvailPhys / (1024 * 1024);
                    var usedMB = totalMB - availableMB;
                    
                    return (usedMB, totalMB);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting memory usage: {ex.Message}");
            }
            
            return (0, 0);
        }
        
        /// <summary>
        /// Gets the number of logical processors (cores)
        /// </summary>
        /// <returns>Number of logical processors</returns>
        public int GetLogicalProcessorCount()
        {
            return Environment.ProcessorCount;
        }
        
        /// <summary>
        /// Gets the number of physical processors (sockets)
        /// </summary>
        /// <returns>Number of physical processors</returns>
        public int GetPhysicalProcessorCount()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT NumberOfProcessors FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return Convert.ToInt32(obj["NumberOfProcessors"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting physical processor count: {ex.Message}");
            }
            
            return 1; // Default to 1 if not found
        }
        
        /// <summary>
        /// Gets the number of performance cores
        /// </summary>
        /// <returns>Number of performance cores or null if not available</returns>
        public int? GetPerformanceCoreCount()
        {
            // This is a simplified implementation
            // In reality, hybrid CPU detection requires more advanced techniques
            
            try
            {
                var processorName = GetProcessorName();
                
                // Check if this is a hybrid CPU (Intel 12th gen or newer)
                if (processorName.Contains("12th Gen") || 
                    processorName.Contains("13th Gen") || 
                    processorName.Contains("14th Gen") ||
                    processorName.Contains("Core i") && (processorName.Contains("12") || processorName.Contains("13") || processorName.Contains("14")))
                {
                    // For Intel 12th gen, we can estimate performance cores based on model
                    if (processorName.Contains("i9"))
                    {
                        return 16; // i9 typically has 8P + 8E cores
                    }
                    else if (processorName.Contains("i7"))
                    {
                        return 8; // i7 typically has 8P + 4E cores
                    }
                    else if (processorName.Contains("i5"))
                    {
                        return 6; // i5 typically has 6P + 4E cores
                    }
                    else
                    {
                        return Environment.ProcessorCount / 2; // Estimate
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting performance core count: {ex.Message}");
            }
            
            return null; // Not a hybrid CPU
        }
        
        /// <summary>
        /// Gets the number of efficiency cores
        /// </summary>
        /// <returns>Number of efficiency cores or null if not available</returns>
        public int? GetEfficiencyCoreCount()
        {
            // This is a simplified implementation
            // In reality, hybrid CPU detection requires more advanced techniques
            
            var performanceCores = GetPerformanceCoreCount();
            if (performanceCores.HasValue)
            {
                return Environment.ProcessorCount - performanceCores.Value;
            }
            
            return null; // Not a hybrid CPU
        }
    }
}