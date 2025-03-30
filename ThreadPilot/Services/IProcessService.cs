using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for process service
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Get all processes
        /// </summary>
        IEnumerable<ProcessInfo> GetAllProcesses();
        
        /// <summary>
        /// Get process by ID
        /// </summary>
        /// <param name="processId">Process ID</param>
        ProcessInfo? GetProcessById(int processId);
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">Affinity mask</param>
        /// <returns>True if successful</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">Priority</param>
        /// <returns>True if successful</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Suspend process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>True if successful</returns>
        bool SuspendProcess(int processId);
        
        /// <summary>
        /// Resume process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>True if successful</returns>
        bool ResumeProcess(int processId);
    }
}