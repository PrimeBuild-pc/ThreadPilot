using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents system hardware and OS information
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
        public int PhysicalCores { get; set; }
        
        /// <summary>
        /// Number of logical CPU cores/threads
        /// </summary>
        public int LogicalCores { get; set; }
        
        /// <summary>
        /// Base CPU clock speed in MHz
        /// </summary>
        public int BaseCpuSpeed { get; set; }
        
        /// <summary>
        /// Current CPU clock speed in MHz (if available)
        /// </summary>
        public int CurrentCpuSpeed { get; set; }
        
        /// <summary>
        /// CPU temperature in Celsius (if available)
        /// </summary>
        public float? CpuTemperature { get; set; }
        
        /// <summary>
        /// CPU architecture (x64, ARM64, etc.)
        /// </summary>
        public string CpuArchitecture { get; set; } = string.Empty;
        
        /// <summary>
        /// Total system RAM in MB
        /// </summary>
        public long TotalRam { get; set; }
        
        /// <summary>
        /// Available system RAM in MB
        /// </summary>
        public long AvailableRam { get; set; }
        
        /// <summary>
        /// RAM usage percentage
        /// </summary>
        public int RamUsagePercentage { get; set; }
        
        /// <summary>
        /// CPU usage percentage (average across all cores)
        /// </summary>
        public int CpuUsagePercentage { get; set; }
        
        /// <summary>
        /// CPU usage per core as percentage
        /// </summary>
        public List<int> CpuCoreUsagePercentages { get; set; } = new List<int>();
        
        /// <summary>
        /// Windows OS version
        /// </summary>
        public string OsVersion { get; set; } = string.Empty;
        
        /// <summary>
        /// Current active power plan/profile name
        /// </summary>
        public string CurrentPowerPlan { get; set; } = string.Empty;
        
        /// <summary>
        /// System uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }
        
        /// <summary>
        /// Current date and time when the information was retrieved
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Returns a formatted brief description of the system
        /// </summary>
        public string GetSystemSummary()
        {
            return $"{CpuName} | {LogicalCores} Threads | {TotalRam / 1024} GB RAM | {OsVersion}";
        }
    }
}