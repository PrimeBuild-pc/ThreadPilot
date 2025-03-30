using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Implementation of process service
    /// </summary>
    public class ProcessService : IProcessService
    {
        /// <summary>
        /// Gets all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        public IEnumerable<ProcessInfo> GetProcesses()
        {
            var processes = new List<ProcessInfo>();
            
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        var processInfo = GetProcessInfo(process);
                        if (processInfo != null)
                        {
                            processes.Add(processInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting process info for {process.ProcessName}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes: {ex.Message}");
            }
            
            return processes;
        }
        
        /// <summary>
        /// Gets process by ID
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <returns>Process information or null if not found</returns>
        public ProcessInfo GetProcessById(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    return GetProcessInfo(process);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting process by ID {processId}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets processes by name
        /// </summary>
        /// <param name="processName">Process name</param>
        /// <returns>List of process information</returns>
        public IEnumerable<ProcessInfo> GetProcessesByName(string processName)
        {
            var processes = new List<ProcessInfo>();
            
            try
            {
                foreach (var process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        var processInfo = GetProcessInfo(process);
                        if (processInfo != null)
                        {
                            processes.Add(processInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting process info for {process.ProcessName}: {ex.Message}");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes by name {processName}: {ex.Message}");
            }
            
            return processes;
        }
        
        /// <summary>
        /// Sets process affinity (which cores the process can use)
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">Affinity mask (bit mask where each bit represents a core)</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessAffinity(int processId, long affinityMask)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.ProcessorAffinity = new IntPtr(affinityMask);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process affinity for ID {processId}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Sets process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">Process priority</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.PriorityClass = GetSystemPriorityClass(priority);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process priority for ID {processId}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Applies process affinity rules to matching processes
        /// </summary>
        /// <param name="rules">List of process affinity rules</param>
        /// <returns>Number of processes affected</returns>
        public int ApplyAffinityRules(IEnumerable<ProcessAffinityRule> rules)
        {
            int affectedCount = 0;
            
            try
            {
                var processes = Process.GetProcesses();
                
                foreach (var rule in rules.Where(r => r.IsEnabled))
                {
                    var regex = new Regex(rule.ProcessNamePattern, RegexOptions.IgnoreCase);
                    var affinityMask = GetAffinityMask(rule.CoreIndices);
                    
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (regex.IsMatch(process.ProcessName))
                            {
                                // Set process affinity
                                process.ProcessorAffinity = new IntPtr(affinityMask);
                                
                                // Set process priority
                                process.PriorityClass = GetSystemPriorityClass(rule.ProcessPriority);
                                
                                affectedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error applying rule to process {process.ProcessName}: {ex.Message}");
                        }
                    }
                }
                
                // Dispose all processes
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying process affinity rules: {ex.Message}");
            }
            
            return affectedCount;
        }
        
        /// <summary>
        /// Gets process information
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>Process information</returns>
        private ProcessInfo GetProcessInfo(Process process)
        {
            if (process == null || process.Id == 0)
            {
                return null;
            }
            
            try
            {
                var processInfo = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    Description = GetProcessDescription(process),
                    Priority = GetProcessPriority(process.PriorityClass),
                    Affinity = (long)process.ProcessorAffinity,
                    ThreadCount = process.Threads.Count
                };
                
                // Get CPU and memory usage if process has not exited
                if (!process.HasExited)
                {
                    try
                    {
                        process.Refresh();
                        processInfo.CpuUsagePercentage = GetProcessCpuUsage(process);
                        processInfo.MemoryUsageMB = process.WorkingSet64 / (1024 * 1024);
                    }
                    catch (Exception)
                    {
                        // Ignore errors getting CPU/memory usage
                    }
                }
                
                return processInfo;
            }
            catch (Exception)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Gets process description from file version info
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>Process description or empty string if not available</returns>
        private string GetProcessDescription(Process process)
        {
            try
            {
                if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
                {
                    var fileVersionInfo = FileVersionInfo.GetVersionInfo(process.MainModule.FileName);
                    return fileVersionInfo.FileDescription ?? process.ProcessName;
                }
            }
            catch (Exception)
            {
                // Ignore errors getting file description
            }
            
            return process.ProcessName;
        }
        
        /// <summary>
        /// Gets process CPU usage
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>CPU usage percentage</returns>
        private float GetProcessCpuUsage(Process process)
        {
            // This is a simplified implementation 
            // Real implementation would require performance counters for accurate CPU usage per process
            
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process WHERE IDProcess = {process.Id}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var cpuUsage = Convert.ToSingle(obj["PercentProcessorTime"]);
                        return cpuUsage;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors getting CPU usage
            }
            
            // Return estimate based on process priority
            return process.PriorityClass switch
            {
                ProcessPriorityClass.RealTime => 90,
                ProcessPriorityClass.High => 70,
                ProcessPriorityClass.AboveNormal => 50,
                ProcessPriorityClass.Normal => 30,
                ProcessPriorityClass.BelowNormal => 15,
                ProcessPriorityClass.Idle => 5,
                _ => 0
            };
        }
        
        /// <summary>
        /// Converts system priority class to process priority enum
        /// </summary>
        /// <param name="priorityClass">System priority class</param>
        /// <returns>Process priority enum</returns>
        private ProcessPriority GetProcessPriority(ProcessPriorityClass priorityClass)
        {
            return priorityClass switch
            {
                ProcessPriorityClass.RealTime => ProcessPriority.Realtime,
                ProcessPriorityClass.High => ProcessPriority.High,
                ProcessPriorityClass.AboveNormal => ProcessPriority.AboveNormal,
                ProcessPriorityClass.Normal => ProcessPriority.Normal,
                ProcessPriorityClass.BelowNormal => ProcessPriority.BelowNormal,
                ProcessPriorityClass.Idle => ProcessPriority.Idle,
                _ => ProcessPriority.Normal
            };
        }
        
        /// <summary>
        /// Converts process priority enum to system priority class
        /// </summary>
        /// <param name="priority">Process priority enum</param>
        /// <returns>System priority class</returns>
        private ProcessPriorityClass GetSystemPriorityClass(ProcessPriority priority)
        {
            return priority switch
            {
                ProcessPriority.Realtime => ProcessPriorityClass.RealTime,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                _ => ProcessPriorityClass.Normal
            };
        }
        
        /// <summary>
        /// Gets affinity mask from core indices
        /// </summary>
        /// <param name="coreIndices">Core indices</param>
        /// <returns>Affinity mask</returns>
        private long GetAffinityMask(IEnumerable<int> coreIndices)
        {
            long affinityMask = 0;
            
            foreach (var coreIndex in coreIndices)
            {
                affinityMask |= (1L << coreIndex);
            }
            
            return affinityMask == 0 ? (1L << Environment.ProcessorCount) - 1 : affinityMask;
        }
    }
}