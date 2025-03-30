using System;
using System.Collections.Generic;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Interface for the system information service.
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>
        /// Gets information about the system.
        /// </summary>
        /// <returns>A SystemInfo object containing the system information.</returns>
        SystemInfo GetSystemInfo();

        /// <summary>
        /// Gets the list of CPU cores.
        /// </summary>
        /// <returns>A list of CpuCore objects.</returns>
        List<CpuCore> GetCpuCores();

        /// <summary>
        /// Gets the CPU utilization percentage.
        /// </summary>
        /// <returns>The CPU utilization percentage.</returns>
        float GetCpuUtilization();

        /// <summary>
        /// Gets the CPU temperature.
        /// </summary>
        /// <returns>The CPU temperature in degrees Celsius.</returns>
        float GetCpuTemperature();

        /// <summary>
        /// Gets the RAM utilization percentage.
        /// </summary>
        /// <returns>The RAM utilization percentage.</returns>
        float GetRamUtilization();

        /// <summary>
        /// Gets the total RAM in GB.
        /// </summary>
        /// <returns>The total RAM in GB.</returns>
        float GetTotalRam();

        /// <summary>
        /// Gets the battery percentage (if applicable).
        /// </summary>
        /// <returns>The battery percentage, or null if not applicable.</returns>
        float? GetBatteryPercentage();

        /// <summary>
        /// Checks if the system has a battery.
        /// </summary>
        /// <returns>True if the system has a battery, false otherwise.</returns>
        bool HasBattery();

        /// <summary>
        /// Checks if the system is on AC power.
        /// </summary>
        /// <returns>True if the system is on AC power, false otherwise.</returns>
        bool IsOnAcPower();

        /// <summary>
        /// Gets the current power scheme.
        /// </summary>
        /// <returns>The current power scheme GUID.</returns>
        Guid GetCurrentPowerScheme();

        /// <summary>
        /// Sets the current power scheme.
        /// </summary>
        /// <param name="powerSchemeGuid">The power scheme GUID to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetCurrentPowerScheme(Guid powerSchemeGuid);

        /// <summary>
        /// Event that is raised when system information is updated.
        /// </summary>
        event EventHandler? SystemInfoUpdated;

        /// <summary>
        /// Starts the monitoring of system information.
        /// </summary>
        /// <param name="intervalInSeconds">The interval in seconds between updates.</param>
        void StartMonitoring(int intervalInSeconds = 1);

        /// <summary>
        /// Stops the monitoring of system information.
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Checks if monitoring is active.
        /// </summary>
        /// <returns>True if monitoring is active, false otherwise.</returns>
        bool IsMonitoringActive();
    }
}