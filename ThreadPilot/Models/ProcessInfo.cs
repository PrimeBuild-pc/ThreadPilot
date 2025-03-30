namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a system process
    /// </summary>
    public class ProcessInfo
    {
        /// <summary>
        /// Gets or sets the process ID
        /// </summary>
        public int ProcessId { get; set; }
        
        /// <summary>
        /// Gets or sets the process name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent { get; set; }
        
        /// <summary>
        /// Gets or sets the memory usage in MB
        /// </summary>
        public double MemoryUsageMB { get; set; }
        
        /// <summary>
        /// Gets or sets the thread count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Gets or sets the process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Gets or sets the process affinity mask
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the process is a system process
        /// </summary>
        public bool IsSystemProcess { get; set; }
    }
    
    /// <summary>
    /// Represents process priority levels
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle priority class
        /// </summary>
        Idle = 64,
        
        /// <summary>
        /// Below normal priority class
        /// </summary>
        BelowNormal = 16384,
        
        /// <summary>
        /// Normal priority class
        /// </summary>
        Normal = 32,
        
        /// <summary>
        /// Above normal priority class
        /// </summary>
        AboveNormal = 32768,
        
        /// <summary>
        /// High priority class
        /// </summary>
        High = 128,
        
        /// <summary>
        /// Real-time priority class (use with caution)
        /// </summary>
        RealTime = 256
    }
}