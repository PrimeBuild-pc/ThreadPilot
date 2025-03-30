using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model class for process information
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
        /// Process description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Process executable path
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;
        
        /// <summary>
        /// CPU usage as a percentage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public double MemoryUsageMb { get; set; }
        
        /// <summary>
        /// Number of threads
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Process start time
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Current process affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Current process priority
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// Indicates if the process is responding
        /// </summary>
        public bool IsResponding { get; set; }
        
        /// <summary>
        /// Indicates if the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
        
        /// <summary>
        /// Indicates if the process is 64-bit
        /// </summary>
        public bool Is64Bit { get; set; }
        
        /// <summary>
        /// Indicates if the process is elevated
        /// </summary>
        public bool IsElevated { get; set; }
        
        /// <summary>
        /// Company name of the process
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;
    }
}