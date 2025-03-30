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
        /// Get all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        List<ProcessInfo> GetProcesses();
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">New priority</param>
        /// <returns>True if successful</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">New affinity mask</param>
        /// <returns>True if successful</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Apply a process affinity rule
        /// </summary>
        /// <param name="rule">Rule to apply</param>
        /// <returns>Number of processes affected</returns>
        int ApplyAffinityRule(ProcessAffinityRule rule);
    }
}