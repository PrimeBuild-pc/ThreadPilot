using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// System information
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public SystemInfo()
        {
            CpuCores = new List<CpuCore>();
        }
        
        /// <summary>
        /// CPU name
        /// </summary>
        public string? CpuName { get; set; }
        
        /// <summary>
        /// CPU usage
        /// </summary>
        public double CpuUsage { get; set; }
        
        /// <summary>
        /// CPU temperature
        /// </summary>
        public double CpuTemperature { get; set; }
        
        /// <summary>
        /// CPU frequency
        /// </summary>
        public double CpuFrequency { get; set; }
        
        /// <summary>
        /// Operating system name
        /// </summary>
        public string? OsName { get; set; }
        
        /// <summary>
        /// Operating system version
        /// </summary>
        public string? OsVersion { get; set; }
        
        /// <summary>
        /// System uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }
        
        /// <summary>
        /// Process count
        /// </summary>
        public int ProcessCount { get; set; }
        
        /// <summary>
        /// Thread count
        /// </summary>
        public int ThreadCount { get; set; }
        
        /// <summary>
        /// Total memory (KB)
        /// </summary>
        public double TotalMemory { get; set; }
        
        /// <summary>
        /// Available memory (KB)
        /// </summary>
        public double AvailableMemory { get; set; }
        
        /// <summary>
        /// Used memory (KB)
        /// </summary>
        public double UsedMemory { get; set; }
        
        /// <summary>
        /// Memory usage (percentage)
        /// </summary>
        public double MemoryUsage { get; set; }
        
        /// <summary>
        /// CPU cores
        /// </summary>
        public List<CpuCore> CpuCores { get; }
    }
}