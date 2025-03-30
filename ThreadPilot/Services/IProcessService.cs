using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing system processes
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Get a list of all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        List<ProcessInfo> GetRunningProcesses();
        
        /// <summary>
        /// Get process information by ID
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>Process information, or null if not found</returns>
        ProcessInfo? GetProcessById(int processId);
        
        /// <summary>
        /// Terminate a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>True if successful, false otherwise</returns>
        bool TerminateProcess(int processId);
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="priority">The priority level</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Set process core affinity
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="affinityMask">The CPU affinity mask (bit field)</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Get process affinity mask
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The process affinity mask or -1 if failed</returns>
        long GetProcessAffinity(int processId);
        
        /// <summary>
        /// Apply process affinity rules from a power profile
        /// </summary>
        /// <param name="profile">The power profile</param>
        /// <returns>Number of successfully applied rules</returns>
        int ApplyProcessAffinityRules(PowerProfile profile);
    }
}