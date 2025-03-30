using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Model class for system information
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// CPU name
        /// </summary>
        public string CpuName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of physical CPU cores
        /// </summary>
        public int PhysicalCores { get; set; }
        
        /// <summary>
        /// Number of logical CPU cores
        /// </summary>
        public int LogicalCores { get; set; }
        
        /// <summary>
        /// CPU base clock speed in MHz
        /// </summary>
        public double CpuBaseClockMhz { get; set; }
        
        /// <summary>
        /// Current CPU clock speed in MHz
        /// </summary>
        public double CpuCurrentClockMhz { get; set; }
        
        /// <summary>
        /// Current CPU load as a percentage
        /// </summary>
        public double CpuLoad { get; set; }
        
        /// <summary>
        /// CPU temperature in Celsius
        /// </summary>
        public double CpuTemperature { get; set; }
        
        /// <summary>
        /// Total RAM in GB
        /// </summary>
        public double TotalRamGb { get; set; }
        
        /// <summary>
        /// Available RAM in GB
        /// </summary>
        public double AvailableRamGb { get; set; }
        
        /// <summary>
        /// RAM usage as a percentage
        /// </summary>
        public double RamUsagePercent { get; set; }
        
        /// <summary>
        /// List of core loads for each logical core
        /// </summary>
        public List<double> CoreLoads { get; set; } = new();
        
        /// <summary>
        /// Operating system information
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;
        
        /// <summary>
        /// System uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }
        
        /// <summary>
        /// Time the info was captured
        /// </summary>
        public DateTime CaptureTime { get; set; } = DateTime.Now;
    }
}