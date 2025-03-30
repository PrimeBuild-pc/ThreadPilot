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
        SystemInfo GetSystemInfo();
        
        /// <summary>
        /// Unpark all CPU cores
        /// </summary>
        void UnparkAllCores();
    }
}