using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// System information service interface
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Get system information
        /// </summary>
        /// <returns>System information</returns>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Get CPU cores
        /// </summary>
        /// <returns>CPU cores</returns>
        IEnumerable<CpuCore> GetCpuCores();
        
        /// <summary>
        /// Get CPU usage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        float GetCpuUsage();
        
        /// <summary>
        /// Get memory usage
        /// </summary>
        /// <returns>Memory usage in MB</returns>
        long GetMemoryUsage();
        
        /// <summary>
        /// Get total memory
        /// </summary>
        /// <returns>Total memory in MB</returns>
        long GetTotalMemory();
        
        /// <summary>
        /// Get current power plan name
        /// </summary>
        /// <returns>Power plan name</returns>
        string GetCurrentPowerPlanName();
    }
}