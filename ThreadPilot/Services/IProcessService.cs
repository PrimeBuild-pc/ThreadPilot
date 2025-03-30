using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for process management service
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Get information about all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        List<ProcessInfo> GetAllProcesses();
        
        /// <summary>
        /// Get information about a specific process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Process information or null if not found</returns>
        ProcessInfo GetProcessById(int processId);
        
        /// <summary>
        /// Get processes by name
        /// </summary>
        /// <param name="processName">Process name to search for</param>
        /// <returns>List of matching processes</returns>
        List<ProcessInfo> GetProcessesByName(string processName);
        
        /// <summary>
        /// Set CPU affinity for a process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">CPU affinity mask</param>
        /// <returns>True if successful</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Set CPU affinity for a process using core indices
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="coreIndices">List of core indices</param>
        /// <returns>True if successful</returns>
        bool SetProcessAffinityByCores(int processId, List<int> coreIndices);
        
        /// <summary>
        /// Set priority for a process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">Process priority</param>
        /// <returns>True if successful</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Apply a process affinity rule
        /// </summary>
        /// <param name="rule">The rule to apply</param>
        /// <returns>Number of processes the rule was applied to</returns>
        int ApplyAffinityRule(ProcessAffinityRule rule);
        
        /// <summary>
        /// Terminate a process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>True if successful</returns>
        bool TerminateProcess(int processId);
        
        /// <summary>
        /// Refresh process information
        /// </summary>
        void RefreshProcesses();
        
        /// <summary>
        /// Get a list of the most resource-intensive processes
        /// </summary>
        /// <param name="count">Maximum number of processes to return</param>
        /// <returns>List of resource-intensive processes</returns>
        List<ProcessInfo> GetTopResourceIntensiveProcesses(int count = 5);
        
        /// <summary>
        /// Check if process is running with elevated privileges
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>True if the process is elevated</returns>
        bool IsProcessElevated(int processId);
    }
}