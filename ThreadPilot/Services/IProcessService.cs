using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the process service.
    /// </summary>
    public interface IProcessService
    {
        /// <summary>
        /// Gets a list of all running processes.
        /// </summary>
        /// <returns>A list of ProcessInfo objects.</returns>
        List<ProcessInfo> GetAllProcesses();

        /// <summary>
        /// Gets information about a process by its ID.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>The ProcessInfo object, or null if not found.</returns>
        ProcessInfo? GetProcessById(int processId);

        /// <summary>
        /// Gets information about a process by its name.
        /// </summary>
        /// <param name="processName">The process name.</param>
        /// <returns>The first ProcessInfo object with the specified name, or null if not found.</returns>
        ProcessInfo? GetProcessByName(string processName);

        /// <summary>
        /// Gets all processes with the specified name.
        /// </summary>
        /// <param name="processName">The process name.</param>
        /// <returns>A list of ProcessInfo objects with the specified name.</returns>
        List<ProcessInfo> GetProcessesByName(string processName);

        /// <summary>
        /// Gets all processes matching the specified pattern.
        /// </summary>
        /// <param name="pattern">The process name pattern (supports wildcards * and ?).</param>
        /// <returns>A list of ProcessInfo objects matching the pattern.</returns>
        List<ProcessInfo> GetProcessesByPattern(string pattern);

        /// <summary>
        /// Sets the processor affinity for a process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <param name="affinityMask">The affinity mask.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetProcessAffinity(int processId, long affinityMask);

        /// <summary>
        /// Sets the processor affinity for a process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <param name="affinityMask">The affinity mask.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetProcessAffinity(ProcessInfo processInfo, long affinityMask);

        /// <summary>
        /// Sets the priority for a process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <param name="priority">The process priority.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetProcessPriority(int processId, ProcessPriority priority);

        /// <summary>
        /// Sets the priority for a process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <param name="priority">The process priority.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetProcessPriority(ProcessInfo processInfo, ProcessPriority priority);

        /// <summary>
        /// Terminates a process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool TerminateProcess(int processId);

        /// <summary>
        /// Terminates a process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool TerminateProcess(ProcessInfo processInfo);

        /// <summary>
        /// Suspends a process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SuspendProcess(int processId);

        /// <summary>
        /// Suspends a process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SuspendProcess(ProcessInfo processInfo);

        /// <summary>
        /// Resumes a suspended process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ResumeProcess(int processId);

        /// <summary>
        /// Resumes a suspended process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool ResumeProcess(ProcessInfo processInfo);

        /// <summary>
        /// Restarts a process.
        /// </summary>
        /// <param name="processId">The process ID.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool RestartProcess(int processId);

        /// <summary>
        /// Restarts a process.
        /// </summary>
        /// <param name="processInfo">The ProcessInfo object.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool RestartProcess(ProcessInfo processInfo);

        /// <summary>
        /// Creates a process from a file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="arguments">The command line arguments.</param>
        /// <returns>The created ProcessInfo object, or null if creation failed.</returns>
        ProcessInfo? CreateProcess(string filePath, string arguments = "");

        /// <summary>
        /// Applies a process affinity rule.
        /// </summary>
        /// <param name="rule">The ProcessAffinityRule object.</param>
        /// <returns>The number of processes modified.</returns>
        int ApplyProcessRule(ProcessAffinityRule rule);

        /// <summary>
        /// Applies a list of process affinity rules.
        /// </summary>
        /// <param name="rules">The list of ProcessAffinityRule objects.</param>
        /// <returns>The number of processes modified.</returns>
        int ApplyProcessRules(List<ProcessAffinityRule> rules);

        /// <summary>
        /// Gets the processor affinity mask for all available processors.
        /// </summary>
        /// <returns>The affinity mask for all available processors.</returns>
        long GetAllProcessorsMask();

        /// <summary>
        /// Gets a processor affinity mask for the specified core indices.
        /// </summary>
        /// <param name="coreIndices">The list of core indices.</param>
        /// <returns>The affinity mask for the specified cores.</returns>
        long GetAffinityMaskForCores(IEnumerable<int> coreIndices);

        /// <summary>
        /// Converts a processor affinity mask to a list of core indices.
        /// </summary>
        /// <param name="affinityMask">The affinity mask.</param>
        /// <returns>The list of core indices.</returns>
        List<int> GetCoreIndicesFromAffinityMask(long affinityMask);

        /// <summary>
        /// Gets a string representation of a processor affinity mask.
        /// </summary>
        /// <param name="affinityMask">The affinity mask.</param>
        /// <returns>The string representation of the affinity mask.</returns>
        string GetAffinityMaskString(long affinityMask);

        /// <summary>
        /// Gets a processor affinity mask from a string representation.
        /// </summary>
        /// <param name="affinityMaskString">The string representation of the affinity mask.</param>
        /// <returns>The affinity mask, or -1 if parsing failed.</returns>
        long GetAffinityMaskFromString(string affinityMaskString);

        /// <summary>
        /// Event that is raised when a process is started.
        /// </summary>
        event EventHandler<ProcessInfo>? ProcessStarted;

        /// <summary>
        /// Event that is raised when a process is terminated.
        /// </summary>
        event EventHandler<ProcessInfo>? ProcessTerminated;

        /// <summary>
        /// Event that is raised when a process's priority is changed.
        /// </summary>
        event EventHandler<ProcessInfo>? ProcessPriorityChanged;

        /// <summary>
        /// Event that is raised when a process's affinity is changed.
        /// </summary>
        event EventHandler<ProcessInfo>? ProcessAffinityChanged;
    }
}