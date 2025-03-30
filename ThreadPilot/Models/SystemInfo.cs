using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about the system
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Gets or sets the CPU name
        /// </summary>
        public string CpuName { get; set; }
        
        /// <summary>
        /// Gets or sets the CPU vendor
        /// </summary>
        public string CpuVendor { get; set; }
        
        /// <summary>
        /// Gets or sets the number of physical cores
        /// </summary>
        public int PhysicalCoreCount { get; set; }
        
        /// <summary>
        /// Gets or sets the number of logical processors
        /// </summary>
        public int LogicalProcessorCount { get; set; }
        
        /// <summary>
        /// Gets or sets the list of core information
        /// </summary>
        public List<CpuCore> Cores { get; set; } = new List<CpuCore>();
        
        /// <summary>
        /// Gets or sets the CPU socket count
        /// </summary>
        public int CpuSocketCount { get; set; }
        
        /// <summary>
        /// Gets or sets the total system memory in bytes
        /// </summary>
        public long TotalMemory { get; set; }
        
        /// <summary>
        /// Gets or sets the available system memory in bytes
        /// </summary>
        public long AvailableMemory { get; set; }
        
        /// <summary>
        /// Gets or sets the CPU architecture
        /// </summary>
        public string CpuArchitecture { get; set; }
        
        /// <summary>
        /// Gets or sets the operating system name
        /// </summary>
        public string OsName { get; set; }
        
        /// <summary>
        /// Gets or sets the operating system version
        /// </summary>
        public string OsVersion { get; set; }
        
        /// <summary>
        /// Gets or sets whether the system has multiple CPU packages
        /// </summary>
        public bool HasMultiplePackages { get; set; }
        
        /// <summary>
        /// Gets or sets whether the system has hybrid cores (P-cores and E-cores)
        /// </summary>
        public bool HasHybridCores { get; set; }
        
        /// <summary>
        /// Gets or sets the system uptime
        /// </summary>
        public TimeSpan Uptime { get; set; }
        
        /// <summary>
        /// Gets or sets the overall CPU utilization percentage
        /// </summary>
        public double CpuUtilization { get; set; }
        
        /// <summary>
        /// Gets or sets the memory utilization percentage
        /// </summary>
        public double MemoryUtilization { get; set; }
        
        /// <summary>
        /// Gets or sets the current maximum CPU frequency in MHz
        /// </summary>
        public int MaxCpuFrequency { get; set; }
        
        /// <summary>
        /// Gets or sets the current CPU package temperature in Celsius
        /// </summary>
        public double CpuTemperature { get; set; }
        
        /// <summary>
        /// Gets or sets the system manufacturer
        /// </summary>
        public string SystemManufacturer { get; set; }
        
        /// <summary>
        /// Gets or sets the system model
        /// </summary>
        public string SystemModel { get; set; }
        
        /// <summary>
        /// Gets or sets whether the system is a laptop
        /// </summary>
        public bool IsLaptop { get; set; }
        
        /// <summary>
        /// Gets or sets the current power profile name
        /// </summary>
        public string CurrentPowerProfileName { get; set; }
    }
}