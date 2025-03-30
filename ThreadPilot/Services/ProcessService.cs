using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Process service implementation
    /// </summary>
    public class ProcessService : IProcessService
    {
        private readonly Dictionary<int, string> _processDescriptions = new Dictionary<int, string>();
        
        /// <summary>
        /// Get processes
        /// </summary>
        /// <param name="limit">Maximum number of processes to return (0 for all)</param>
        /// <returns>Process information collection</returns>
        public IEnumerable<ProcessInfo> GetProcesses(int limit = 0)
        {
            var processes = new List<ProcessInfo>();
            
            try
            {
                var rawProcesses = Process.GetProcesses();
                
                foreach (var process in rawProcesses)
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            Description = GetProcessDescription(process.Id, process.ProcessName),
                            ThreadCount = process.Threads.Count,
                            MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
                            Priority = ConvertPriorityClass(process.PriorityClass),
                            Affinity = (long)process.ProcessorAffinity
                        };
                        
                        // Get CPU usage - approximate since WMI is expensive
                        processInfo.CpuUsagePercentage = GetProcessCpuUsage(process.Id);
                        
                        processes.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error adding process {process.Id}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
                
                // Sort by CPU usage
                processes = processes.OrderByDescending(p => p.CpuUsagePercentage).ToList();
                
                // Apply limit
                if (limit > 0 && processes.Count > limit)
                {
                    processes = processes.Take(limit).ToList();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes: {ex.Message}");
            }
            
            return processes;
        }
        
        /// <summary>
        /// Get process by ID
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Process information</returns>
        public ProcessInfo GetProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                
                var processInfo = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    Description = GetProcessDescription(process.Id, process.ProcessName),
                    ThreadCount = process.Threads.Count,
                    MemoryUsageMB = process.WorkingSet64 / (1024 * 1024),
                    Priority = ConvertPriorityClass(process.PriorityClass),
                    Affinity = (long)process.ProcessorAffinity
                };
                
                // Get CPU usage
                processInfo.CpuUsagePercentage = GetProcessCpuUsage(process.Id);
                
                process.Dispose();
                
                return processInfo;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting process {processId}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">Priority</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.PriorityClass = ConvertPriority(priority);
                process.Dispose();
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process priority: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="coreIndices">Core indices</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessAffinity(int processId, IEnumerable<int> coreIndices)
        {
            try
            {
                if (coreIndices == null || !coreIndices.Any())
                {
                    return false;
                }
                
                // Convert core indices to affinity mask
                long affinityMask = 0;
                foreach (var coreIndex in coreIndices)
                {
                    affinityMask |= (1L << coreIndex);
                }
                
                var process = Process.GetProcessById(processId);
                process.ProcessorAffinity = (IntPtr)affinityMask;
                process.Dispose();
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process affinity: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Terminate process
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool TerminateProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
                process.Dispose();
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error terminating process: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Get process description
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="processName">Process name</param>
        /// <returns>Process description</returns>
        private string GetProcessDescription(int processId, string processName)
        {
            if (_processDescriptions.TryGetValue(processId, out var description))
            {
                return description;
            }
            
            try
            {
                // Try to get description from WMI
                using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        description = obj["Description"]?.ToString() ?? string.Empty;
                        
                        if (string.IsNullOrEmpty(description))
                        {
                            description = processName;
                        }
                        
                        _processDescriptions[processId] = description;
                        return description;
                    }
                }
                
                // Fallback to process name
                description = processName;
                _processDescriptions[processId] = description;
                return description;
            }
            catch (Exception)
            {
                // Fallback to process name in case of error
                description = processName;
                _processDescriptions[processId] = description;
                return description;
            }
        }
        
        /// <summary>
        /// Get process CPU usage percentage
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>CPU usage percentage</returns>
        private float GetProcessCpuUsage(int processId)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_PerfFormattedData_PerfProc_Process WHERE IDProcess = {processId}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return float.Parse(obj["PercentProcessorTime"]?.ToString() ?? "0");
                    }
                }
                
                return 0;
            }
            catch (Exception)
            {
                // If we can't get real CPU usage, approximate it using thread count
                try
                {
                    var process = Process.GetProcessById(processId);
                    int threadCount = process.Threads.Count;
                    process.Dispose();
                    
                    // Very rough approximation
                    return Math.Min(threadCount * 0.5f, 100);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }
        
        /// <summary>
        /// Convert ProcessPriorityClass to ProcessPriority
        /// </summary>
        /// <param name="priorityClass">Priority class</param>
        /// <returns>Process priority</returns>
        private ProcessPriority ConvertPriorityClass(ProcessPriorityClass priorityClass)
        {
            switch (priorityClass)
            {
                case ProcessPriorityClass.Idle:
                    return ProcessPriority.Idle;
                case ProcessPriorityClass.BelowNormal:
                    return ProcessPriority.BelowNormal;
                case ProcessPriorityClass.Normal:
                    return ProcessPriority.Normal;
                case ProcessPriorityClass.AboveNormal:
                    return ProcessPriority.AboveNormal;
                case ProcessPriorityClass.High:
                    return ProcessPriority.High;
                case ProcessPriorityClass.RealTime:
                    return ProcessPriority.Realtime;
                default:
                    return ProcessPriority.Normal;
            }
        }
        
        /// <summary>
        /// Convert ProcessPriority to ProcessPriorityClass
        /// </summary>
        /// <param name="priority">Process priority</param>
        /// <returns>Priority class</returns>
        private ProcessPriorityClass ConvertPriority(ProcessPriority priority)
        {
            switch (priority)
            {
                case ProcessPriority.Idle:
                    return ProcessPriorityClass.Idle;
                case ProcessPriority.BelowNormal:
                    return ProcessPriorityClass.BelowNormal;
                case ProcessPriority.Normal:
                    return ProcessPriorityClass.Normal;
                case ProcessPriority.AboveNormal:
                    return ProcessPriorityClass.AboveNormal;
                case ProcessPriority.High:
                    return ProcessPriorityClass.High;
                case ProcessPriority.Realtime:
                    return ProcessPriorityClass.RealTime;
                default:
                    return ProcessPriorityClass.Normal;
            }
        }
    }
}