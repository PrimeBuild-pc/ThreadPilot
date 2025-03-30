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
        /// Get system information including CPU, RAM, and core details
        /// </summary>
        /// <returns>System information</returns>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Get CPU usage as a percentage
        /// </summary>
        /// <returns>CPU usage percentage (0-100)</returns>
        double GetCpuUsage();
        
        /// <summary>
        /// Get memory usage as a percentage
        /// </summary>
        /// <returns>Memory usage percentage (0-100)</returns>
        double GetMemoryUsage();
        
        /// <summary>
        /// Get CPU core usage
        /// </summary>
        /// <returns>List of CPU cores with usage information</returns>
        List<CpuCore> GetCoreUsage();
    }
}