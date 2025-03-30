using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Process priority class
    /// </summary>
    public enum ProcessPriorityClass
    {
        /// <summary>
        /// Very low priority (Idle)
        /// </summary>
        Idle,
        
        /// <summary>
        /// Below normal priority
        /// </summary>
        BelowNormal,
        
        /// <summary>
        /// Normal priority
        /// </summary>
        Normal,
        
        /// <summary>
        /// Above normal priority
        /// </summary>
        AboveNormal,
        
        /// <summary>
        /// High priority
        /// </summary>
        High,
        
        /// <summary>
        /// Real-time priority (use with caution)
        /// </summary>
        RealTime
    }
    
    /// <summary>
    /// Service for working with system processes
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Get a list of all running processes
        /// </summary>
        /// <returns>List of running processes</returns>
        List<ProcessInfo> GetRunningProcesses();
        
        /// <summary>
        /// Get information about a specific process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Process information or null if not found</returns>
        ProcessInfo? GetProcessById(int processId);
        
        /// <summary>
        /// Set the CPU affinity mask for a process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">CPU affinity mask</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Set the priority class for a process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priorityClass">Priority class to set</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessPriority(int processId, ProcessPriorityClass priorityClass);
        
        /// <summary>
        /// Apply a process affinity rule to matching processes
        /// </summary>
        /// <param name="rule">Process affinity rule to apply</param>
        /// <returns>Number of processes that were modified</returns>
        int ApplyProcessRule(ProcessAffinityRule rule);
    }
}