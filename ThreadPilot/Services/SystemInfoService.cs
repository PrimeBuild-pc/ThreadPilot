using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of system information service
    /// </summary>
    public class SystemInfoService : ISystemInfoService
    {
        // PerformanceCounter for CPU usage
        private readonly PerformanceCounter _cpuCounter;
        
        // Random for generating demo data
        private readonly Random _random = new Random();
        
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfoService()
        {
            try
            {
                // Initialize performance counter
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // First call to NextValue() will always return 0
            }
            catch (Exception)
            {
                // In case of error, we'll provide demo data
            }
        }
        
        /// <summary>
        /// Get system information
        /// </summary>
        public SystemInfo GetSystemInfo()
        {
            try
            {
                // In a real application, we would get the actual system information
                // For now, we'll return demo data
                
                var systemInfo = new SystemInfo
                {
                    CpuName = GetCpuName(),
                    CpuUsage = GetCpuUsage(),
                    CpuTemperature = 45 + _random.NextDouble() * 15,
                    CpuFrequency = 3000 + _random.NextDouble() * 1000,
                    OsName = Environment.OSVersion.Platform.ToString(),
                    OsVersion = Environment.OSVersion.VersionString,
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount),
                    ProcessCount = Process.GetProcesses().Length,
                    ThreadCount = 1000 + _random.Next(500),
                    TotalMemory = 16 * 1024 * 1024, // 16 GB in KB
                    AvailableMemory = (3 + _random.NextDouble() * 5) * 1024 * 1024, // 3-8 GB in KB
                };
                
                // Calculate used memory and memory usage
                systemInfo.UsedMemory = systemInfo.TotalMemory - systemInfo.AvailableMemory;
                systemInfo.MemoryUsage = (double)systemInfo.UsedMemory / systemInfo.TotalMemory * 100;
                
                // Create CPU cores
                var coreCount = Environment.ProcessorCount;
                
                for (var i = 0; i < coreCount; i++)
                {
                    var isParked = i >= coreCount / 2 && _random.NextDouble() < 0.3;
                    
                    systemInfo.CpuCores.Add(new CpuCore
                    {
                        Id = i,
                        Name = $"Core {i}",
                        Usage = isParked ? 0 : _random.NextDouble() * 100,
                        Temperature = isParked ? 35 : 40 + _random.NextDouble() * 20,
                        Frequency = isParked ? 800 : 3000 + _random.NextDouble() * 1000,
                        IsParked = isParked
                    });
                }
                
                return systemInfo;
            }
            catch (Exception)
            {
                // In case of error, we'll provide demo data
                return GetDemoSystemInfo();
            }
        }
        
        /// <summary>
        /// Unpark all CPU cores
        /// </summary>
        public void UnparkAllCores()
        {
            // In a real application, we would unpark all cores here
            // For now, we'll just log a message
            Debug.WriteLine("Unparking all CPU cores");
        }
        
        /// <summary>
        /// Get CPU name
        /// </summary>
        private string GetCpuName()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["Name"]?.ToString() ?? "Unknown CPU";
                }
            }
            catch (Exception)
            {
                // In case of error, return a default value
            }
            
            return "Intel Core i9-12900K";
        }
        
        /// <summary>
        /// Get CPU usage
        /// </summary>
        private double GetCpuUsage()
        {
            try
            {
                return _cpuCounter.NextValue();
            }
            catch (Exception)
            {
                // In case of error, return a default value
                return _random.NextDouble() * 100;
            }
        }
        
        /// <summary>
        /// Get demo system information
        /// </summary>
        private SystemInfo GetDemoSystemInfo()
        {
            var systemInfo = new SystemInfo
            {
                CpuName = "Intel Core i9-12900K",
                CpuUsage = _random.NextDouble() * 100,
                CpuTemperature = 45 + _random.NextDouble() * 15,
                CpuFrequency = 3000 + _random.NextDouble() * 1000,
                OsName = "Windows",
                OsVersion = "11 Pro",
                Uptime = TimeSpan.FromHours(_random.Next(24 * 7)),
                ProcessCount = 100 + _random.Next(100),
                ThreadCount = 1000 + _random.Next(500),
                TotalMemory = 16 * 1024 * 1024, // 16 GB in KB
                AvailableMemory = (3 + _random.NextDouble() * 5) * 1024 * 1024, // 3-8 GB in KB
            };
            
            // Calculate used memory and memory usage
            systemInfo.UsedMemory = systemInfo.TotalMemory - systemInfo.AvailableMemory;
            systemInfo.MemoryUsage = (double)systemInfo.UsedMemory / systemInfo.TotalMemory * 100;
            
            // Create CPU cores
            var coreCount = 16; // 16 cores
            
            for (var i = 0; i < coreCount; i++)
            {
                var isParked = i >= coreCount / 2 && _random.NextDouble() < 0.3;
                
                systemInfo.CpuCores.Add(new CpuCore
                {
                    Id = i,
                    Name = $"Core {i}",
                    Usage = isParked ? 0 : _random.NextDouble() * 100,
                    Temperature = isParked ? 35 : 40 + _random.NextDouble() * 20,
                    Frequency = isParked ? 800 : 3000 + _random.NextDouble() * 1000,
                    IsParked = isParked
                });
            }
            
            return systemInfo;
        }
    }
}