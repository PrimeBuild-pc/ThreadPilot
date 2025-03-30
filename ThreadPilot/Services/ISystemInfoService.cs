using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for retrieving system information
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Get current system information
        /// </summary>
        /// <returns>System information</returns>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Get information about CPU cores
        /// </summary>
        /// <returns>List of CPU cores</returns>
        List<CpuCore> GetCpuCores();
        
        /// <summary>
        /// Get current memory usage
        /// </summary>
        /// <returns>Memory usage in MB and percentage</returns>
        (double TotalMB, double UsedMB, double UsagePercent) GetMemoryUsage();
        
        /// <summary>
        /// Get current CPU usage percentage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        double GetCpuUsagePercentage();
        
        /// <summary>
        /// Get active CPU cores count (not parked)
        /// </summary>
        /// <returns>Number of active cores</returns>
        int GetActiveCoresCount();
        
        /// <summary>
        /// Get the total number of CPU cores
        /// </summary>
        /// <returns>Total number of cores</returns>
        int GetTotalCoresCount();
    }
}