using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents CPU core information
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Core index
        /// </summary>
        public int Index { get; set; }
        
        /// <summary>
        /// Whether this is a logical (SMT/HT) or physical core
        /// </summary>
        public bool IsLogical { get; set; }
        
        /// <summary>
        /// Current usage percentage (0-100)
        /// </summary>
        public double Usage { get; set; }
        
        /// <summary>
        /// Current temperature in Celsius (if available)
        /// </summary>
        public double? Temperature { get; set; }
        
        /// <summary>
        /// Current frequency in MHz
        /// </summary>
        public int Frequency { get; set; }
        
        /// <summary>
        /// Physical core this logical core belongs to (for logical cores)
        /// </summary>
        public int? PhysicalCoreIndex { get; set; }
    }
    
    /// <summary>
    /// Represents system information
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// CPU model name
        /// </summary>
        public string CpuName { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of physical CPU cores
        /// </summary>
        public int CoreCount { get; set; }
        
        /// <summary>
        /// Number of logical CPU processors/threads
        /// </summary>
        public int ProcessorCount { get; set; }
        
        /// <summary>
        /// Base CPU frequency in MHz
        /// </summary>
        public int BaseCpuFrequency { get; set; }
        
        /// <summary>
        /// Total system RAM in GB
        /// </summary>
        public double TotalRam { get; set; }
        
        /// <summary>
        /// Available system RAM in GB
        /// </summary>
        public double AvailableRam { get; set; }
        
        /// <summary>
        /// Operating system name and version
        /// </summary>
        public string OsInfo { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether CPU performance mode is enabled
        /// </summary>
        public bool IsPerformanceModeEnabled { get; set; }
        
        /// <summary>
        /// List of CPU cores
        /// </summary>
        public List<CpuCore> Cores { get; } = new List<CpuCore>();
        
        /// <summary>
        /// System uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }
    }
}