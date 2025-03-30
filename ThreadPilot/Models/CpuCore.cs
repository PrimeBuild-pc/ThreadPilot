using System;

namespace ThreadPilot.Models
{
    /// <summary>
    /// CPU core class
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Core ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Core number (0-based)
        /// </summary>
        public int Number { get; set; }
        
        /// <summary>
        /// Processor ID (for multi-CPU systems)
        /// </summary>
        public int ProcessorId { get; set; }
        
        /// <summary>
        /// NUMA node
        /// </summary>
        public int NumaNode { get; set; }
        
        /// <summary>
        /// Core usage percentage
        /// </summary>
        public float UsagePercentage { get; set; }
        
        /// <summary>
        /// Core frequency in MHz
        /// </summary>
        public int FrequencyMHz { get; set; }
        
        /// <summary>
        /// Gets a value indicating whether the core is a performance core (as opposed to efficiency core)
        /// </summary>
        public bool IsPerformanceCore { get; set; }
        
        /// <summary>
        /// Gets the core name
        /// </summary>
        public string CoreName => $"Core {Number}";
        
        /// <summary>
        /// Gets the core type name
        /// </summary>
        public string CoreTypeName => IsPerformanceCore ? "Performance" : "Efficiency";
        
        /// <summary>
        /// Gets the core frequency in GHz
        /// </summary>
        public float FrequencyGHz => FrequencyMHz / 1000f;
    }
}