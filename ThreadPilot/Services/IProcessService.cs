using System;
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
        /// Gets all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        IEnumerable<ProcessInfo> GetProcesses();
        
        /// <summary>
        /// Gets process by ID
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Process information or null if not found</returns>
        ProcessInfo GetProcessById(int processId);
        
        /// <summary>
        /// Gets processes by name
        /// </summary>
        /// <param name="processName">Process name</param>
        /// <returns>List of process information</returns>
        IEnumerable<ProcessInfo> GetProcessesByName(string processName);
        
        /// <summary>
        /// Sets process affinity (which cores the process can use)
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">Affinity mask (bit mask where each bit represents a core)</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Sets process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">Process priority</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Applies process affinity rules to matching processes
        /// </summary>
        /// <param name="rules">List of process affinity rules</param>
        /// <returns>Number of processes affected</returns>
        int ApplyAffinityRules(IEnumerable<ProcessAffinityRule> rules);
    }
}