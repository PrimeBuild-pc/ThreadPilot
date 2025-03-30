using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// System information service implementation
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;
        private readonly ManagementObjectSearcher _processorSearcher;
        private readonly ManagementObjectSearcher _osSearcher;
        private readonly ManagementObjectSearcher _computerSystemSearcher;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfoService()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                
                _processorSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                _osSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                _computerSystemSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                
                // First call to NextValue might return 0
                _cpuCounter.NextValue();
                
                // Short delay to get accurate initial reading
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing SystemInfoService: {ex.Message}");
            }
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
                // Get CPU information
                foreach (ManagementObject processor in _processorSearcher.Get())
                {
                    systemInfo.CpuName = processor["Name"]?.ToString() ?? "Unknown CPU";
                    systemInfo.CpuFrequencyMHz = int.Parse(processor["MaxClockSpeed"]?.ToString() ?? "0");
                    break; // Just get the first processor for now
                }
                
                // Get OS information
                foreach (ManagementObject os in _osSearcher.Get())
                {
                    systemInfo.OsNameAndVersion = $"{os["Caption"]} {os["Version"]}";
                    systemInfo.Uptime = TimeSpan.FromSeconds(Convert.ToDouble(os["LastBootUpTime"]));
                    break;
                }
                
                // Get memory information
                foreach (ManagementObject computerSystem in _computerSystemSearcher.Get())
                {
                    systemInfo.TotalMemoryMB = Convert.ToInt64(computerSystem["TotalPhysicalMemory"]) / (1024 * 1024);
                    break;
                }
                
                // Get current usage information
                systemInfo.CpuUsagePercentage = GetCpuUsage();
                systemInfo.MemoryUsageMB = systemInfo.TotalMemoryMB - GetMemoryUsage();
                
                // Get process count
                systemInfo.ProcessCount = Process.GetProcesses().Length;
                
                // Get CPU cores information
                systemInfo.CpuCores = GetCpuCores();
                
                // Get current power plan
                systemInfo.PowerPlanName = GetCurrentPowerPlanName();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting system information: {ex.Message}");
            }
            
            return systemInfo;
        }
        
        /// <summary>
        /// Get CPU cores
        /// </summary>
        /// <returns>CPU cores</returns>
        public IEnumerable<CpuCore> GetCpuCores()
        {
            var cores = new List<CpuCore>();
            
            try
            {
                // Get processor core information
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PerfFormattedData_PerfOS_Processor"))
                {
                    int coreIndex = 0;
                    
                    foreach (var core in searcher.Get())
                    {
                        var name = core["Name"]?.ToString();
                        
                        // Skip _Total
                        if (name == "_Total")
                        {
                            continue;
                        }
                        
                        var cpuCore = new CpuCore
                        {
                            Id = coreIndex,
                            Number = int.Parse(name ?? "0"),
                            ProcessorId = 0, // Default to first processor for now
                            NumaNode = 0, // Default to first NUMA node for now
                            UsagePercentage = float.Parse(core["PercentProcessorTime"]?.ToString() ?? "0"),
                            FrequencyMHz = GetCpuFrequency(),
                            IsPerformanceCore = coreIndex < 8 // Simple heuristic - first cores are usually performance cores
                        };
                        
                        cores.Add(cpuCore);
                        coreIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU cores: {ex.Message}");
                
                // Create dummy cores for testing
                for (int i = 0; i < 8; i++)
                {
                    cores.Add(new CpuCore
                    {
                        Id = i,
                        Number = i,
                        ProcessorId = 0,
                        NumaNode = 0,
                        UsagePercentage = 0,
                        FrequencyMHz = 3000,
                        IsPerformanceCore = i < 4
                    });
                }
            }
            
            return cores;
        }
        
        /// <summary>
        /// Get CPU usage
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
        /// Get memory usage
        /// </summary>
        /// <returns>Memory usage in MB</returns>
        public long GetMemoryUsage()
        {
            try
            {
                return (long)(_ramCounter?.NextValue() ?? 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting memory usage: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Get total memory
        /// </summary>
        /// <returns>Total memory in MB</returns>
        public long GetTotalMemory()
        {
            try
            {
                foreach (ManagementObject computerSystem in _computerSystemSearcher.Get())
                {
                    return Convert.ToInt64(computerSystem["TotalPhysicalMemory"]) / (1024 * 1024);
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting total memory: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Get CPU frequency
        /// </summary>
        /// <returns>CPU frequency in MHz</returns>
        private int GetCpuFrequency()
        {
            try
            {
                foreach (ManagementObject processor in _processorSearcher.Get())
                {
                    return int.Parse(processor["MaxClockSpeed"]?.ToString() ?? "0");
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting CPU frequency: {ex.Message}");
                return 3000; // Default to 3 GHz
            }
        }
        
        /// <summary>
        /// Get current power plan name
        /// </summary>
        /// <returns>Power plan name</returns>
        public string GetCurrentPowerPlanName()
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "powercfg";
                    process.StartInfo.Arguments = "/getactivescheme";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    // Extract power plan name
                    int startIndex = output.LastIndexOf('(');
                    int endIndex = output.LastIndexOf(')');
                    
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        return output.Substring(startIndex + 1, endIndex - startIndex - 1);
                    }
                    
                    return "Unknown";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting power plan name: {ex.Message}");
                return "Unknown";
            }
        }
    }
}