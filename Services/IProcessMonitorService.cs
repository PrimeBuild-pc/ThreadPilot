/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    /// <summary>
    /// Interface for process monitoring service that uses WMI events with fallback polling.
    /// </summary>
    public interface IProcessMonitorService : IDisposable
    {
        /// <summary>
        /// Event fired when a process starts
        /// </summary>
        event EventHandler<ProcessEventArgs>? ProcessStarted;

        /// <summary>
        /// Event fired when a process stops
        /// </summary>
        event EventHandler<ProcessEventArgs>? ProcessStopped;

        /// <summary>
        /// Event fired when monitoring status changes
        /// </summary>
        event EventHandler<MonitoringStatusEventArgs>? MonitoringStatusChanged;

        /// <summary>
        /// Gets a value indicating whether gets whether the service is currently monitoring.
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Gets a value indicating whether gets whether WMI monitoring is available and working.
        /// </summary>
        bool IsWmiAvailable { get; }

        /// <summary>
        /// Gets a value indicating whether gets whether fallback polling is currently active.
        /// </summary>
        bool IsFallbackPollingActive { get; }

        /// <summary>
        /// Starts monitoring processes.
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stops monitoring processes.
        /// </summary>
        Task StopMonitoringAsync();

        /// <summary>
        /// Gets all currently running processes.
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetRunningProcessesAsync();

        /// <summary>
        /// Checks if a specific process is currently running.
        /// </summary>
        Task<bool> IsProcessRunningAsync(string executableName);

        /// <summary>
        /// Updates the service settings (polling intervals, etc.)
        /// </summary>
        void UpdateSettings();
    }

    /// <summary>
    /// Event arguments for process events.
    /// </summary>
    public class ProcessEventArgs : EventArgs
    {
        public ProcessModel Process { get; }

        public DateTime Timestamp { get; }

        public ProcessEventArgs(ProcessModel process)
        {
            this.Process = process;
            this.Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for monitoring status changes.
    /// </summary>
    public class MonitoringStatusEventArgs : EventArgs
    {
        public bool IsMonitoring { get; }

        public bool IsWmiAvailable { get; }

        public bool IsFallbackPollingActive { get; }

        public string? StatusMessage { get; }

        public Exception? Error { get; }

        public MonitoringStatusEventArgs(bool isMonitoring, bool isWmiAvailable, bool isFallbackPollingActive, string? statusMessage = null, Exception? error = null)
        {
            this.IsMonitoring = isMonitoring;
            this.IsWmiAvailable = isWmiAvailable;
            this.IsFallbackPollingActive = isFallbackPollingActive;
            this.StatusMessage = statusMessage;
            this.Error = error;
        }
    }
}

