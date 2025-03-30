using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for system information retrieval service
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Get current system information
        /// </summary>
        /// <returns>SystemInfo with current hardware and OS details</returns>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Get information about CPU cores
        /// </summary>
        /// <returns>List of CPU cores</returns>
        List<CpuCore> GetCpuCores();
        
        /// <summary>
        /// Refresh CPU core information and usage statistics
        /// </summary>
        void RefreshCpuCoreStats();
        
        /// <summary>
        /// Get the current CPU usage as a percentage
        /// </summary>
        /// <returns>CPU usage percentage</returns>
        int GetCpuUsage();
        
        /// <summary>
        /// Get the current memory usage as a percentage
        /// </summary>
        /// <returns>Memory usage percentage</returns>
        int GetMemoryUsage();
        
        /// <summary>
        /// Check if core parking is enabled in the current power plan
        /// </summary>
        /// <returns>True if core parking is enabled</returns>
        bool IsCoreParkingEnabled();
        
        /// <summary>
        /// Get the current processor performance boost mode
        /// </summary>
        /// <returns>Boost mode (0-3)</returns>
        int GetProcessorBoostMode();
        
        /// <summary>
        /// Get the current system responsiveness value
        /// </summary>
        /// <returns>System responsiveness value (0-100)</returns>
        int GetSystemResponsiveness();
        
        /// <summary>
        /// Get the current network throttling index
        /// </summary>
        /// <returns>Network throttling index</returns>
        int GetNetworkThrottlingIndex();
    }
}