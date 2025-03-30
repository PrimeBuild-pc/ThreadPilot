namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about a CPU core
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Gets or sets the core ID (0-based)
        /// </summary>
        public int CoreId { get; set; }
        
        /// <summary>
        /// Gets or sets the processor ID (physical CPU number)
        /// </summary>
        public int ProcessorId { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the core is parked
        /// </summary>
        public bool IsParked { get; set; }
        
        /// <summary>
        /// Gets or sets the current usage percentage
        /// </summary>
        public double UsagePercent { get; set; }
        
        /// <summary>
        /// Gets or sets the current frequency in MHz
        /// </summary>
        public double CurrentFrequencyMhz { get; set; }
        
        /// <summary>
        /// Gets or sets the temperature in Celsius
        /// </summary>
        public double TemperatureCelsius { get; set; }
    }
}