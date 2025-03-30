using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for process management operations
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Gets all running processes
        /// </summary>
        /// <returns>A list of processes</returns>
        List<ProcessInfo> GetAllProcesses();
        
        /// <summary>
        /// Gets a process by its ID
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>The process information or null if not found</returns>
        ProcessInfo GetProcessById(int processId);
        
        /// <summary>
        /// Gets processes by name pattern
        /// </summary>
        /// <param name="namePattern">The process name pattern</param>
        /// <returns>A list of matching processes</returns>
        List<ProcessInfo> GetProcessesByName(string namePattern);
        
        /// <summary>
        /// Sets the process priority
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="priority">The new priority</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);
        
        /// <summary>
        /// Sets the process affinity mask
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <param name="affinityMask">The new affinity mask</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Creates a process affinity mask from a list of core indices
        /// </summary>
        /// <param name="coreIndices">The core indices to include</param>
        /// <returns>The affinity mask</returns>
        long CreateAffinityMask(IEnumerable<int> coreIndices);
        
        /// <summary>
        /// Gets the core indices from an affinity mask
        /// </summary>
        /// <param name="affinityMask">The affinity mask</param>
        /// <returns>A list of core indices</returns>
        List<int> GetCoreIndicesFromMask(long affinityMask);
        
        /// <summary>
        /// Terminates a process
        /// </summary>
        /// <param name="processId">The process ID</param>
        /// <returns>True if successful, false otherwise</returns>
        bool TerminateProcess(int processId);
        
        /// <summary>
        /// Applies affinity rules to running processes
        /// </summary>
        /// <param name="rules">The list of affinity rules to apply</param>
        /// <returns>The number of rules applied successfully</returns>
        int ApplyAffinityRules(IEnumerable<ProcessAffinityRule> rules);
        
        /// <summary>
        /// Gets the CPU usage per process
        /// </summary>
        /// <returns>A dictionary mapping process IDs to CPU usage percentages</returns>
        Dictionary<int, double> GetProcessCpuUsage();
        
        /// <summary>
        /// Occurs when a process is started
        /// </summary>
        event EventHandler<ProcessInfo> ProcessStarted;
        
        /// <summary>
        /// Occurs when a process is terminated
        /// </summary>
        event EventHandler<int> ProcessTerminated;
    }
}