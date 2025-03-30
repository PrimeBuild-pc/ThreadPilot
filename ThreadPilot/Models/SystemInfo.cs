using System;
using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents information about the system.
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Gets or sets the processor name.
        /// </summary>
        public string ProcessorName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of physical cores.
        /// </summary>
        public int PhysicalCores { get; set; }

        /// <summary>
        /// Gets or sets the number of logical cores.
        /// </summary>
        public int LogicalCores { get; set; }

        /// <summary>
        /// Gets or sets the CPU utilization percentage.
        /// </summary>
        public float CpuUtilization { get; set; }

        /// <summary>
        /// Gets or sets the CPU temperature in degrees Celsius.
        /// </summary>
        public float CpuTemperature { get; set; }

        /// <summary>
        /// Gets or sets the CPU cores information.
        /// </summary>
        public List<CpuCore> Cores { get; set; } = new List<CpuCore>();

        /// <summary>
        /// Gets or sets the total RAM in GB.
        /// </summary>
        public float TotalRam { get; set; }

        /// <summary>
        /// Gets or sets the used RAM in GB.
        /// </summary>
        public float UsedRam { get; set; }

        /// <summary>
        /// Gets or sets the RAM utilization percentage.
        /// </summary>
        public float RamUtilization { get; set; }

        /// <summary>
        /// Gets or sets the battery percentage (if applicable).
        /// </summary>
        public float? BatteryPercentage { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the system has a battery.
        /// </summary>
        public bool HasBattery { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the system is on AC power.
        /// </summary>
        public bool IsOnAcPower { get; set; }

        /// <summary>
        /// Gets or sets the operating system name.
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current power scheme GUID.
        /// </summary>
        public Guid CurrentPowerScheme { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last update.
        /// </summary>
        public DateTime LastUpdateTimestamp { get; set; }
    }

    /// <summary>
    /// Represents information about a CPU core.
    /// </summary>
    public class CpuCore
    {
        /// <summary>
        /// Gets or sets the core index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a logical or physical core.
        /// </summary>
        public bool IsLogical { get; set; }

        /// <summary>
        /// Gets or sets the core utilization percentage.
        /// </summary>
        public float Utilization { get; set; }

        /// <summary>
        /// Gets or sets the core frequency in MHz.
        /// </summary>
        public float Frequency { get; set; }

        /// <summary>
        /// Gets or sets the core temperature in degrees Celsius.
        /// </summary>
        public float? Temperature { get; set; }
    }
}