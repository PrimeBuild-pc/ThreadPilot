using System;
using ThreadPilot.Services;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a running process
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
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Process executable path
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Process description or title
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// CPU usage percentage (0-100)
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsage { get; set; }
        
        /// <summary>
        /// Current CPU affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Process priority class
        /// </summary>
        public ProcessPriorityClass Priority { get; set; }
        
        /// <summary>
        /// Start time of the process
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Whether the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
        
        /// <summary>
        /// Whether the process has a window
        /// </summary>
        public bool HasWindow { get; set; }
        
        /// <summary>
        /// Whether the process can have its affinity modified
        /// </summary>
        public bool IsOptimizable { get; set; }
        
        /// <summary>
        /// Thread count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Process uptime
        /// </summary>
        public TimeSpan Uptime => DateTime.Now - StartTime;
    }
}