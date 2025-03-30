using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Process priority level
    /// </summary>
    public enum ProcessPriority
    {
        Idle,
        BelowNormal,
        Normal,
        AboveNormal,
        High,
        RealTime
    }
    
    /// <summary>
    /// Information about a running process
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Process ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Process name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Process description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Process window title
        /// </summary>
        public string WindowTitle { get; set; }
        
        /// <summary>
        /// Process CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Process memory usage in MB
        /// </summary>
        public double MemoryUsage { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Current thread affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Whether the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
        
        /// <summary>
        /// Whether the process can be modified
        /// </summary>
        public bool CanModify { get; set; }
        
        /// <summary>
        /// Process path
        /// </summary>
        public string Path { get; set; }
        
        /// <summary>
        /// Process start time
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Number of threads in the process
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// List of CPU cores the process is using
        /// </summary>
        public List<int> UsedCores { get; set; }
        
        /// <summary>
        /// Convert from System.Diagnostics.Process to ProcessInfo
        /// </summary>
        /// <param name="process">Process object</param>
        /// <returns>ProcessInfo object</returns>
        public static ProcessInfo FromProcess(Process process)
        {
            try
            {
                var info = new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    // Description would come from a version info reader
                    WindowTitle = string.IsNullOrEmpty(process.MainWindowTitle) ? "-" : process.MainWindowTitle,
                    // CpuUsage would come from performance counter
                    MemoryUsage = process.WorkingSet64 / 1024.0 / 1024.0, // Convert to MB
                    Priority = ConvertPriorityClass(process.PriorityClass),
                    AffinityMask = (long)process.ProcessorAffinity,
                    IsSystemProcess = IsSystemProcess(process),
                    CanModify = CanBeModified(process),
                    Path = process.MainModule?.FileName ?? "-",
                    StartTime = process.StartTime,
                    ThreadCount = process.Threads.Count,
                    UsedCores = new List<int>() // This would be populated by a thread analysis service
                };
                
                return info;
            }
            catch (Exception)
            {
                // Return a limited info object for processes we can't fully access
                return new ProcessInfo
                {
                    Id = process.Id,
                    Name = process.ProcessName,
                    Description = "Access Denied",
                    WindowTitle = "-",
                    MemoryUsage = 0,
                    Priority = ProcessPriority.Normal,
                    IsSystemProcess = true,
                    CanModify = false,
                    Path = "-",
                    ThreadCount = 0,
                    UsedCores = new List<int>()
                };
            }
        }
        
        /// <summary>
        /// Convert a System.Diagnostics.ProcessPriorityClass to ProcessPriority
        /// </summary>
        /// <param name="priorityClass">Process priority class</param>
        /// <returns>ProcessPriority enum value</returns>
        private static ProcessPriority ConvertPriorityClass(ProcessPriorityClass priorityClass)
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
                    return ProcessPriority.RealTime;
                default:
                    return ProcessPriority.Normal;
            }
        }
        
        /// <summary>
        /// Check if a process is a system process
        /// </summary>
        /// <param name="process">Process to check</param>
        /// <returns>True if the process is a system process</returns>
        private static bool IsSystemProcess(Process process)
        {
            // This is a simplified check, a real implementation would be more thorough
            string[] systemProcessNames = { "System", "svchost", "services", "smss", "csrss", "wininit", "lsass", "winlogon" };
            
            foreach (string name in systemProcessNames)
            {
                if (process.ProcessName.ToLower() == name.ToLower())
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Check if a process can be modified
        /// </summary>
        /// <param name="process">Process to check</param>
        /// <returns>True if the process can be modified</returns>
        private static bool CanBeModified(Process process)
        {
            // This is a simplified check, a real implementation would be more thorough
            if (IsSystemProcess(process))
            {
                return false;
            }
            
            try
            {
                return process.MainModule != null;
            }
            catch
            {
                return false;
            }
        }
    }
}