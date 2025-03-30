using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for system information operations
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Gets the system information
        /// </summary>
        /// <returns>The system information</returns>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Gets the list of CPU cores
        /// </summary>
        /// <returns>A list of CPU cores</returns>
        List<CpuCore> GetCpuCores();
        
        /// <summary>
        /// Gets the CPU utilization per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to utilization percentages</returns>
        Dictionary<int, double> GetCpuCoreUtilization();
        
        /// <summary>
        /// Gets the overall CPU utilization
        /// </summary>
        /// <returns>The overall CPU utilization percentage</returns>
        double GetOverallCpuUtilization();
        
        /// <summary>
        /// Gets the memory utilization
        /// </summary>
        /// <returns>The memory utilization percentage</returns>
        double GetMemoryUtilization();
        
        /// <summary>
        /// Gets the CPU frequency per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to frequencies in MHz</returns>
        Dictionary<int, int> GetCpuCoreFrequency();
        
        /// <summary>
        /// Gets the CPU temperature per core
        /// </summary>
        /// <returns>A dictionary mapping core indices to temperatures in Celsius</returns>
        Dictionary<int, double> GetCpuCoreTemperature();
        
        /// <summary>
        /// Gets the overall CPU temperature
        /// </summary>
        /// <returns>The overall CPU temperature in Celsius</returns>
        double GetOverallCpuTemperature();
        
        /// <summary>
        /// Gets the system uptime
        /// </summary>
        /// <returns>The system uptime</returns>
        TimeSpan GetSystemUptime();
        
        /// <summary>
        /// Gets the operating system version
        /// </summary>
        /// <returns>The operating system version</returns>
        string GetOsVersion();
        
        /// <summary>
        /// Gets whether the system is running on a laptop
        /// </summary>
        /// <returns>True if running on a laptop, false otherwise</returns>
        bool IsLaptop();
        
        /// <summary>
        /// Gets the current power profile name
        /// </summary>
        /// <returns>The current power profile name</returns>
        string GetCurrentPowerProfileName();
        
        /// <summary>
        /// Starts monitoring system information
        /// </summary>
        /// <param name="updateInterval">The update interval in milliseconds</param>
        void StartMonitoring(int updateInterval = 1000);
        
        /// <summary>
        /// Stops monitoring system information
        /// </summary>
        void StopMonitoring();
        
        /// <summary>
        /// Occurs when system information is updated
        /// </summary>
        event EventHandler<SystemInfo> SystemInfoUpdated;
    }
}