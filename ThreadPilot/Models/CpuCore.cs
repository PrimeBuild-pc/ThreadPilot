namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a CPU core
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Gets or sets the core index
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Gets or sets the logical processor index
        /// </summary>
        public int LogicalProcessorIndex { get; set; }
        
        /// <summary>
        /// Gets or sets the core ID
        /// </summary>
        public int CoreId { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is hyperthreaded
        /// </summary>
        public bool IsHyperthreaded { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is a performance core
        /// </summary>
        public bool IsPerformanceCore { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is an efficiency core
        /// </summary>
        public bool IsEfficiencyCore { get; set; }
        
        /// <summary>
        /// Gets or sets the current utilization in percent
        /// </summary>
        public double Utilization { get; set; }
        
        /// <summary>
        /// Gets or sets the current frequency in MHz
        /// </summary>
        public int Frequency { get; set; }
        
        /// <summary>
        /// Gets or sets the current temperature in Celsius
        /// </summary>
        public double Temperature { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is parked
        /// </summary>
        public bool IsParked { get; set; }
        
        /// <summary>
        /// Gets the core type (Performance, Efficiency, Normal)
        /// </summary>
        public string CoreType
        {
            get
            {
                if (IsPerformanceCore)
                    return "Performance";
                    
                if (IsEfficiencyCore)
                    return "Efficiency";
                    
                return "Normal";
            }
        }
        
        /// <summary>
        /// Gets the display name of the core
        /// </summary>
        public string DisplayName
        {
            get
            {
                return $"Core {CoreId} ({CoreType})";
            }
        }
    }
}