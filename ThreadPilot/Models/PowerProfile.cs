using System.Collections.Generic;

namespace ThreadPilot.Models
{
    /// <summary>
    /// Represents a power profile configuration for system optimization.
    /// </summary>
    public class PowerProfile
    {
        /// <summary>
        /// Gets or sets the name of the power profile.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the power profile.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the author of the power profile.
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the version of the power profile.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets a value indicating whether the profile is for laptops.
        /// </summary>
        public bool IsForLaptop { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the profile is for desktops.
        /// </summary>
        public bool IsForDesktop { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the profile is bundled with the application.
        /// </summary>
        public bool IsBundled { get; set; }

        /// <summary>
        /// Gets or sets the profile creation date in ISO 8601 format.
        /// </summary>
        public string CreatedDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the profile last modified date in ISO 8601 format.
        /// </summary>
        public string LastModifiedDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of process affinity rules.
        /// </summary>
        public List<ProcessAffinityRule> ProcessRules { get; set; } = new List<ProcessAffinityRule>();

        /// <summary>
        /// Gets or sets the CPU frequency scaling mode.
        /// </summary>
        public CpuFrequencyMode CpuFrequencyMode { get; set; } = CpuFrequencyMode.Balanced;

        /// <summary>
        /// Gets or sets the CPU power saving mode.
        /// </summary>
        public CpuPowerMode CpuPowerMode { get; set; } = CpuPowerMode.Balanced;

        /// <summary>
        /// Gets or sets the energy preference mode.
        /// </summary>
        public EnergyPreference EnergyPreference { get; set; } = EnergyPreference.Balanced;

        /// <summary>
        /// Gets or sets the CPU boost mode.
        /// </summary>
        public CpuBoostMode CpuBoostMode { get; set; } = CpuBoostMode.Enabled;

        /// <summary>
        /// Gets or sets the display brightness when on AC power (0-100).
        /// </summary>
        public int DisplayBrightnessAc { get; set; } = 100;

        /// <summary>
        /// Gets or sets the display brightness when on battery (0-100).
        /// </summary>
        public int DisplayBrightnessBattery { get; set; } = 75;

        /// <summary>
        /// Gets or sets the disk timeout when on AC power (in minutes, 0 = never).
        /// </summary>
        public int DiskTimeoutAc { get; set; } = 20;

        /// <summary>
        /// Gets or sets the disk timeout when on battery (in minutes, 0 = never).
        /// </summary>
        public int DiskTimeoutBattery { get; set; } = 10;

        /// <summary>
        /// Gets or sets the sleep timeout when on AC power (in minutes, 0 = never).
        /// </summary>
        public int SleepTimeoutAc { get; set; } = 30;

        /// <summary>
        /// Gets or sets the sleep timeout when on battery (in minutes, 0 = never).
        /// </summary>
        public int SleepTimeoutBattery { get; set; } = 15;

        /// <summary>
        /// Gets or sets a value indicating whether to use Windows Notifications.
        /// </summary>
        public bool EnableNotifications { get; set; } = true;

        /// <summary>
        /// Gets or sets the file path to the power profile.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a rule for setting process affinity and priority.
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Gets or sets the process name pattern (supports wildcards * and ?).
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the process affinity mask.
        /// </summary>
        public long AffinityMask { get; set; } = -1; // -1 means use all cores

        /// <summary>
        /// Gets or sets the process priority.
        /// </summary>
        public ProcessPriority Priority { get; set; } = ProcessPriority.Normal;

        /// <summary>
        /// Gets or sets a value indicating whether the rule is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to apply the rule automatically.
        /// </summary>
        public bool ApplyAutomatically { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to apply the rule only when the process starts.
        /// </summary>
        public bool ApplyOnlyOnStart { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to restart processes to apply the rule.
        /// </summary>
        public bool RestartToApply { get; set; }
    }

    /// <summary>
    /// Represents the CPU frequency scaling mode.
    /// </summary>
    public enum CpuFrequencyMode
    {
        /// <summary>
        /// Power saving mode, reduces frequency to save power.
        /// </summary>
        PowerSaver,

        /// <summary>
        /// Balanced mode, adjusts frequency based on load.
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode, maximizes frequency for best performance.
        /// </summary>
        Performance,

        /// <summary>
        /// Ultimate performance mode, maintains maximum frequency at all times.
        /// </summary>
        UltimatePerformance
    }

    /// <summary>
    /// Represents the CPU power saving mode.
    /// </summary>
    public enum CpuPowerMode
    {
        /// <summary>
        /// Power saving mode, optimizes for energy efficiency.
        /// </summary>
        PowerSaver,

        /// <summary>
        /// Balanced mode, balances power and performance.
        /// </summary>
        Balanced,

        /// <summary>
        /// Performance mode, optimizes for performance.
        /// </summary>
        Performance
    }

    /// <summary>
    /// Represents the energy preference mode.
    /// </summary>
    public enum EnergyPreference
    {
        /// <summary>
        /// Maximum power saving.
        /// </summary>
        MaximumPowerSavings,

        /// <summary>
        /// Power saving.
        /// </summary>
        PowerSaver,

        /// <summary>
        /// Balanced.
        /// </summary>
        Balanced,

        /// <summary>
        /// Better performance.
        /// </summary>
        BetterPerformance,

        /// <summary>
        /// Best performance.
        /// </summary>
        BestPerformance
    }

    /// <summary>
    /// Represents the CPU boost mode.
    /// </summary>
    public enum CpuBoostMode
    {
        /// <summary>
        /// Disabled, CPU will not boost.
        /// </summary>
        Disabled,

        /// <summary>
        /// Enabled, CPU will boost when needed.
        /// </summary>
        Enabled,

        /// <summary>
        /// Aggressive, CPU will boost more aggressively.
        /// </summary>
        Aggressive,

        /// <summary>
        /// Efficient, CPU will boost efficiently.
        /// </summary>
        Efficient
    }
}