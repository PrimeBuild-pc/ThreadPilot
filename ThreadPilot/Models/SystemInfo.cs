namespace ThreadPilot.Models
{
    /// <summary>
    /// Contains system information
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Gets or sets the CPU name
        /// </summary>
        public string CpuName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the operating system information
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the total RAM in MB
        /// </summary>
        public double TotalRamMB { get; set; }
        
        /// <summary>
        /// Gets or sets the number of physical CPU cores
        /// </summary>
        public int PhysicalCores { get; set; }
        
        /// <summary>
        /// Gets or sets the number of logical CPU cores (threads)
        /// </summary>
        public int LogicalCores { get; set; }
        
        /// <summary>
        /// Gets or sets the active Windows power plan name
        /// </summary>
        public string PowerPlanName { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the CPU base clock speed in MHz
        /// </summary>
        public double BaseCpuFrequencyMhz { get; set; }
    }
}