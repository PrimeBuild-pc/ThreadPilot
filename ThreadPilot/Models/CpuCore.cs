namespace ThreadPilot.Models
{
    /// <summary>
    /// CPU core information
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Core index
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Physical core number
        /// </summary>
        public int PhysicalCore { get; set; }
        
        /// <summary>
        /// Whether this is a logical core (hyperthreading)
        /// </summary>
        public bool IsLogicalCore { get; set; }
        
        /// <summary>
        /// Whether this is an efficiency core (E-core)
        /// </summary>
        public bool IsEfficiencyCore { get; set; }
        
        /// <summary>
        /// Current CPU usage percentage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// Base clock speed in MHz
        /// </summary>
        public int BaseClockMHz { get; set; }
        
        /// <summary>
        /// Current clock speed in MHz
        /// </summary>
        public int CurrentClockMHz { get; set; }
        
        /// <summary>
        /// Maximum clock speed in MHz
        /// </summary>
        public int MaxClockMHz { get; set; }
        
        /// <summary>
        /// Core power usage in watts
        /// </summary>
        public double PowerUsageWatts { get; set; }
        
        /// <summary>
        /// Core temperature in Celsius
        /// </summary>
        public double TemperatureCelsius { get; set; }
        
        /// <summary>
        /// Whether the core is parked
        /// </summary>
        public bool IsParked { get; set; }
        
        /// <summary>
        /// Gets the core type string
        /// </summary>
        public string CoreType => IsEfficiencyCore ? "E-Core" : "P-Core";
        
        /// <summary>
        /// Gets the core description
        /// </summary>
        public string CoreDescription
        {
            get
            {
                string coreType = IsEfficiencyCore ? "E" : "P";
                string logicalInfo = IsLogicalCore ? " (HT)" : "";
                return $"Core {Index} ({coreType}-Core{logicalInfo})";
            }
        }
    }
}