/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Main orchestration service for process monitoring and power plan management
    /// </summary>
    public interface IProcessMonitorManagerService : IDisposable
    {
        /// <summary>
        /// Event fired when a process-triggered power plan change occurs
        /// </summary>
        event EventHandler<ProcessPowerPlanChangeEventArgs>? ProcessPowerPlanChanged;

        /// <summary>
        /// Event fired when the service status changes
        /// </summary>
        event EventHandler<ServiceStatusEventArgs>? ServiceStatusChanged;

        /// <summary>
        /// Gets whether the service is currently running
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets the current service status
        /// </summary>
        string Status { get; }

        /// <summary>
        /// Gets currently running associated processes
        /// </summary>
        IEnumerable<ProcessModel> RunningAssociatedProcesses { get; }

        /// <summary>
        /// Starts the process monitoring and power plan management service
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Stops the service
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Manually triggers a power plan evaluation for all running processes
        /// </summary>
        Task EvaluateCurrentProcessesAsync();

        /// <summary>
        /// Forces a return to the default power plan
        /// </summary>
        Task ForceDefaultPowerPlanAsync();

        /// <summary>
        /// Gets the current active power plan information
        /// </summary>
        Task<PowerPlanModel?> GetCurrentActivePowerPlanAsync();

        /// <summary>
        /// Refreshes the configuration from the association service
        /// </summary>
        Task RefreshConfigurationAsync();

        /// <summary>
        /// Updates the service settings (polling intervals, etc.)
        /// </summary>
        void UpdateSettings();
    }

    /// <summary>
    /// Event arguments for process-triggered power plan changes
    /// </summary>
    public class ProcessPowerPlanChangeEventArgs : EventArgs
    {
        public ProcessModel Process { get; }
        public ProcessPowerPlanAssociation Association { get; }
        public PowerPlanModel? PreviousPowerPlan { get; }
        public PowerPlanModel? NewPowerPlan { get; }
        public string Action { get; } // "ProcessStarted", "ProcessStopped", "DefaultRestored"
        public DateTime Timestamp { get; }

        public ProcessPowerPlanChangeEventArgs(
            ProcessModel process, 
            ProcessPowerPlanAssociation association, 
            PowerPlanModel? previousPowerPlan, 
            PowerPlanModel? newPowerPlan, 
            string action)
        {
            Process = process;
            Association = association;
            PreviousPowerPlan = previousPowerPlan;
            NewPowerPlan = newPowerPlan;
            Action = action;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for service status changes
    /// </summary>
    public class ServiceStatusEventArgs : EventArgs
    {
        public bool IsRunning { get; }
        public string Status { get; }
        public string? Details { get; }
        public Exception? Error { get; }
        public DateTime Timestamp { get; }

        public ServiceStatusEventArgs(bool isRunning, string status, string? details = null, Exception? error = null)
        {
            IsRunning = isRunning;
            Status = status;
            Details = details;
            Error = error;
            Timestamp = DateTime.Now;
        }
    }
}

