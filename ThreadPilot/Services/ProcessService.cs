using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly INotificationService _notificationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessService()
        {
            _notificationService = ServiceLocator.Get<INotificationService>();
        }
        
        /// <summary>
        /// Get all running processes
        /// </summary>
        /// <returns>List of process information</returns>
        public List<ProcessInfo> GetProcesses()
        {
            var processes = new List<ProcessInfo>();
            
            try
            {
                // Get all running processes
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        var processInfo = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            Description = GetProcessDescription(process),
                            ExecutablePath = GetProcessPath(process),
                            StartTime = GetProcessStartTime(process),
                            MemoryUsageMB = process.WorkingSet64 / 1024.0 / 1024.0,
                            Priority = ConvertPriorityClass(process.PriorityClass),
                            AffinityMask = (long)process.ProcessorAffinity,
                            IsSystemProcess = IsSystemProcess(process),
                            IsElevated = IsProcessElevated(process)
                        };
                        
                        // Get CPU usage (would be more accurate over time)
                        processInfo.CpuUsage = GetProcessCpuUsage(process);
                        
                        processes.Add(processInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting process info for {process.ProcessName}: {ex.Message}");
                    }
                }
                
                // Sort by CPU usage descending
                processes = processes.OrderByDescending(p => p.CpuUsage).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting processes: {ex.Message}");
                _notificationService.ShowError($"Error getting processes: {ex.Message}");
            }
            
            return processes;
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="priority">New priority</param>
        /// <returns>True if successful</returns>
        public bool SetProcessPriority(int processId, ProcessPriority priority)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null)
                {
                    ProcessPriorityClass priorityClass = ConvertToPriorityClass(priority);
                    process.PriorityClass = priorityClass;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process priority: {ex.Message}");
                _notificationService.ShowError($"Error setting process priority: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        /// <param name="processId">Process ID</param>
        /// <param name="affinityMask">New affinity mask</param>
        /// <returns>True if successful</returns>
        public bool SetProcessAffinity(int processId, long affinityMask)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null)
                {
                    process.ProcessorAffinity = (IntPtr)affinityMask;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting process affinity: {ex.Message}");
                _notificationService.ShowError($"Error setting process affinity: {ex.Message}");
            }
            
            return false;
        }
        
        /// <summary>
        /// Apply a process affinity rule
        /// </summary>
        /// <param name="rule">Rule to apply</param>
        /// <returns>Number of processes affected</returns>
        public int ApplyAffinityRule(ProcessAffinityRule rule)
        {
            int affectedCount = 0;
            
            try
            {
                // Prepare the pattern
                var regex = new Regex(WildcardToRegex(rule.ProcessNamePattern), 
                                    RegexOptions.IgnoreCase);
                
                // Determine the affinity mask
                long affinityMask = rule.AffinityMask ?? rule.ComputeAffinityMask();
                
                // Get all running processes
                foreach (var process in Process.GetProcesses())
                {
                    bool matches = regex.IsMatch(process.ProcessName);
                    
                    // Check if this process should be affected
                    if ((matches && !rule.IsExcludeList) || (!matches && rule.IsExcludeList))
                    {
                        try
                        {
                            // Set priority if specified
                            if (rule.Priority.HasValue)
                            {
                                ProcessPriorityClass priorityClass = ConvertToPriorityClass(rule.Priority.Value);
                                process.PriorityClass = priorityClass;
                            }
                            
                            // Set affinity if specified
                            if (affinityMask != 0)
                            {
                                process.ProcessorAffinity = (IntPtr)affinityMask;
                            }
                            
                            affectedCount++;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error applying rule to process {process.ProcessName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying process rule: {ex.Message}");
                _notificationService.ShowError($"Error applying process rule: {ex.Message}");
            }
            
            return affectedCount;
        }
        
        /// <summary>
        /// Convert wildcard pattern to regex
        /// </summary>
        /// <param name="pattern">Wildcard pattern</param>
        /// <returns>Regex pattern</returns>
        private string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                   .Replace("\\*", ".*")
                   .Replace("\\?", ".") + "$";
        }
        
        /// <summary>
        /// Get process description
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>Process description</returns>
        private string GetProcessDescription(Process process)
        {
            try
            {
                string query = $"SELECT Description FROM Win32_Process WHERE ProcessId = {process.Id}";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string? description = obj["Description"]?.ToString();
                        return !string.IsNullOrEmpty(description) ? description : process.ProcessName;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting process description: {ex.Message}");
            }
            
            return process.ProcessName;
        }
        
        /// <summary>
        /// Get process executable path
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>Process path</returns>
        private string GetProcessPath(Process process)
        {
            try
            {
                string query = $"SELECT ExecutablePath FROM Win32_Process WHERE ProcessId = {process.Id}";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string? path = obj["ExecutablePath"]?.ToString();
                        return !string.IsNullOrEmpty(path) ? path : string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting process path: {ex.Message}");
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Get process start time
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>Process start time</returns>
        private DateTime GetProcessStartTime(Process process)
        {
            try
            {
                return process.StartTime;
            }
            catch (Exception)
            {
                // Some system processes won't allow access to their start time
                return DateTime.Now;
            }
        }
        
        /// <summary>
        /// Get process CPU usage
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>CPU usage percentage</returns>
        private double GetProcessCpuUsage(Process process)
        {
            try
            {
                // In a real implementation, this would calculate actual CPU usage
                // For this demo, we'll use a random value based on the process ID
                Random random = new Random(process.Id);
                return Math.Round(random.NextDouble() * 10.0, 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting process CPU usage: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// Check if a process is a system process
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>True if system process</returns>
        private bool IsSystemProcess(Process process)
        {
            try
            {
                // Check common system processes
                string name = process.ProcessName.ToLower();
                string[] systemProcessNames = {
                    "system", "smss", "csrss", "wininit", "services", "lsass", "svchost",
                    "winlogon", "dwm", "explorer", "spoolsv", "taskhost", "taskhostw"
                };
                
                // Check by ID or name
                return process.Id <= 4 || systemProcessNames.Contains(name);
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Check if a process is elevated
        /// </summary>
        /// <param name="process">Process</param>
        /// <returns>True if elevated</returns>
        private bool IsProcessElevated(Process process)
        {
            try
            {
                // This is simplified for demo purposes
                string[] elevatedProcessNames = {
                    "mmc", "taskmgr", "regedit", "services"
                };
                
                return elevatedProcessNames.Contains(process.ProcessName.ToLower());
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// Convert .NET priority class to our enum
        /// </summary>
        /// <param name="priorityClass">Priority class</param>
        /// <returns>Process priority</returns>
        private ProcessPriority ConvertPriorityClass(ProcessPriorityClass priorityClass)
        {
            return priorityClass switch
            {
                ProcessPriorityClass.Idle => ProcessPriority.Idle,
                ProcessPriorityClass.BelowNormal => ProcessPriority.BelowNormal,
                ProcessPriorityClass.Normal => ProcessPriority.Normal,
                ProcessPriorityClass.AboveNormal => ProcessPriority.AboveNormal,
                ProcessPriorityClass.High => ProcessPriority.High,
                ProcessPriorityClass.RealTime => ProcessPriority.RealTime,
                _ => ProcessPriority.Normal
            };
        }
        
        /// <summary>
        /// Convert our enum to .NET priority class
        /// </summary>
        /// <param name="priority">Process priority</param>
        /// <returns>Priority class</returns>
        private ProcessPriorityClass ConvertToPriorityClass(ProcessPriority priority)
        {
            return priority switch
            {
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal
            };
        }
    }
}