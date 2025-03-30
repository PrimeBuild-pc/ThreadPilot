using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the process service
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Get all processes
        /// </summary>
        IList<ProcessInfo> GetProcesses();
        
        /// <summary>
        /// Get a process by ID
        /// </summary>
        ProcessInfo? GetProcessById(int id);
        
        /// <summary>
        /// Set process affinity mask
        /// </summary>
        bool SetProcessAffinity(int processId, long affinityMask);
        
        /// <summary>
        /// Set process priority
        /// </summary>
        bool SetProcessPriority(int processId, int priority);
        
        /// <summary>
        /// End a process
        /// </summary>
        bool EndProcess(int processId);
        
        /// <summary>
        /// Get all process affinity rules
        /// </summary>
        IList<ProcessAffinityRule> GetAffinityRules();
        
        /// <summary>
        /// Add or update a process affinity rule
        /// </summary>
        bool SaveAffinityRule(ProcessAffinityRule rule);
        
        /// <summary>
        /// Delete a process affinity rule
        /// </summary>
        bool DeleteAffinityRule(ProcessAffinityRule rule);
        
        /// <summary>
        /// Apply all enabled affinity rules
        /// </summary>
        void ApplyAffinityRules();
        
        /// <summary>
        /// Optimize processes according to predefined rules
        /// </summary>
        void OptimizeProcesses();
    }
}