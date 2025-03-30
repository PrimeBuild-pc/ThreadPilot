using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for system information service
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
        /// <returns>List of CPU cores</returns>
        List<CpuCore> GetCpuCores();
        
        /// <summary>
        /// Reset CPU cores to default settings
        /// </summary>
        /// <returns>True if successful</returns>
        bool ResetCpuCores();
        
        /// <summary>
        /// Optimize CPU cores for best performance
        /// </summary>
        /// <returns>True if successful</returns>
        bool OptimizeCpuCores();
    }
}