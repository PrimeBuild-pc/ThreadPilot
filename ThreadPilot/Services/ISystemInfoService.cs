using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the system information service
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Get system information
        /// </summary>
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Get CPU usage
        /// </summary>
        double GetCpuUsage();
        
        /// <summary>
        /// Get available memory in GB
        /// </summary>
        double GetAvailableMemoryGb();
        
        /// <summary>
        /// Get CPU temperature in Celsius
        /// </summary>
        double GetCpuTemperature();
        
        /// <summary>
        /// Get core usage for each CPU core
        /// </summary>
        double[] GetCoreUsages();
    }
}