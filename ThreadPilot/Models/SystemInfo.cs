using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// System information class
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Processor name
        /// </summary>
        public string ProcessorName { get; set; } = string.Empty;
        
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public float CpuUsagePercentage { get; set; }
        
        /// <summary>
        /// Memory usage in MB
        /// </summary>
        public ulong MemoryUsageMB { get; set; }
        
        /// <summary>
        /// Total memory in MB
        /// </summary>
        public ulong TotalMemoryMB { get; set; }
        
        /// <summary>
        /// Logical processor count
        /// </summary>
        public int LogicalProcessorCount { get; set; }
        
        /// <summary>
        /// Physical processor count
        /// </summary>
        public int PhysicalProcessorCount { get; set; }
        
        /// <summary>
        /// Performance core count (hybrid CPUs)
        /// </summary>
        public int PerformanceCoreCount { get; set; }
        
        /// <summary>
        /// Efficiency core count (hybrid CPUs)
        /// </summary>
        public int EfficiencyCoreCount { get; set; }
        
        /// <summary>
        /// Operating system information
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;
        
        /// <summary>
        /// Machine name
        /// </summary>
        public string MachineName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets the memory usage percentage
        /// </summary>
        public float MemoryUsagePercentage => TotalMemoryMB > 0 ? (float)MemoryUsageMB / TotalMemoryMB * 100 : 0;
        
        /// <summary>
        /// Gets a value indicating whether the system has a hybrid CPU
        /// </summary>
        public bool HasHybridCpu => PerformanceCoreCount > 0 && EfficiencyCoreCount > 0;
    }
}