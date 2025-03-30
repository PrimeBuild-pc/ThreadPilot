using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a process
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the process window title
        /// </summary>
        public string WindowTitle { get; set; }
        
        /// <summary>
        /// Gets or sets the process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Gets or sets the process affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Gets or sets the memory usage in bytes
        /// </summary>
        public long MemoryUsage { get; set; }
        
        /// <summary>
        /// Gets or sets the process start time
        /// </summary>
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// Gets or sets the process path
        /// </summary>
        public string ExecutablePath { get; set; }
        
        /// <summary>
        /// Gets or sets the process description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the process company name
        /// </summary>
        public string CompanyName { get; set; }
        
        /// <summary>
        /// Gets or sets the process is 64-bit flag
        /// </summary>
        public bool Is64Bit { get; set; }
        
        /// <summary>
        /// Gets or sets the list of associated module names
        /// </summary>
        public List<string> Modules { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the process threads count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Gets or sets whether the process is elevated (running as admin)
        /// </summary>
        public bool IsElevated { get; set; }
        
        /// <summary>
        /// Gets or sets whether the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
        
        /// <summary>
        /// Gets or sets whether the process is responding
        /// </summary>
        public bool IsResponding { get; set; }
    }
}