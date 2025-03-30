namespace ThreadPilot.Models
{
    /// <summary>
    /// Process information
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
        public string? Name { get; set; }
        
        /// <summary>
        /// Process description
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Process path
        /// </summary>
        public string? Path { get; set; }
        
        /// <summary>
        /// Process CPU usage (percentage)
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Process memory usage (KB)
        /// </summary>
        public double MemoryUsage { get; set; }
        
        /// <summary>
        /// Thread count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// CPU affinity mask (used to determine which cores the process can use)
        /// </summary>
        public long AffinityMask { get; set; }
        
        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriority Priority { get; set; }
        
        /// <summary>
        /// Is the process suspended
        /// </summary>
        public bool IsSuspended { get; set; }
        
        /// <summary>
        /// Is the process critical (system process)
        /// </summary>
        public bool IsCritical { get; set; }
    }
    
    /// <summary>
    /// Process priority
    /// </summary>
    public enum ProcessPriority
    {
        /// <summary>
        /// Idle
        /// </summary>
        Idle = 0,
        
        /// <summary>
        /// Below normal
        /// </summary>
        BelowNormal = 1,
        
        /// <summary>
        /// Normal
        /// </summary>
        Normal = 2,
        
        /// <summary>
        /// Above normal
        /// </summary>
        AboveNormal = 3,
        
        /// <summary>
        /// High
        /// </summary>
        High = 4,
        
        /// <summary>
        /// Realtime
        /// </summary>
        Realtime = 5
    }
}