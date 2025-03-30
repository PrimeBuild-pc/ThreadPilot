namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a CPU core
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Gets or sets the core index
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Gets or sets the CPU package index
        /// </summary>
        public int PackageIndex { get; set; }
        
        /// <summary>
        /// Gets or sets the core ID
        /// </summary>
        public int CoreId { get; set; }
        
        /// <summary>
        /// Gets or sets the logical processor ID
        /// </summary>
        public int LogicalProcessorIndex { get; set; }
        
        /// <summary>
        /// Gets or sets the current utilization percentage
        /// </summary>
        public double Utilization { get; set; }
        
        /// <summary>
        /// Gets or sets the current frequency in MHz
        /// </summary>
        public int Frequency { get; set; }
        
        /// <summary>
        /// Gets or sets the maximum frequency in MHz
        /// </summary>
        public int MaxFrequency { get; set; }
        
        /// <summary>
        /// Gets or sets the current temperature in Celsius
        /// </summary>
        public double Temperature { get; set; }
        
        /// <summary>
        /// Gets or sets whether the core is a hyperthreaded core (logical processor)
        /// </summary>
        public bool IsHyperthreaded { get; set; }
        
        /// <summary>
        /// Gets or sets whether the core is part of an efficiency cluster (E-core)
        /// </summary>
        public bool IsEfficiencyCore { get; set; }
        
        /// <summary>
        /// Gets or sets whether the core is part of a performance cluster (P-core)
        /// </summary>
        public bool IsPerformanceCore { get; set; }
        
        /// <summary>
        /// Gets or sets the friendly name for the core
        /// </summary>
        public string Name => $"Core {CoreId}{(IsHyperthreaded ? " (HT)" : "")} {(IsEfficiencyCore ? "E" : IsPerformanceCore ? "P" : "")}";
    }
}