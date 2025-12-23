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
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public interface IPowerPlanService
    {
        /// <summary>
        /// Event fired when the active power plan changes
        /// </summary>
        event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;

        Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync();
        Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync();
        Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan);
        Task<PowerPlanModel?> GetActivePowerPlan();
        Task<bool> ImportCustomPowerPlan(string filePath);

        /// <summary>
        /// Sets the active power plan by GUID with duplicate change prevention
        /// </summary>
        Task<bool> SetActivePowerPlanByGuidAsync(string powerPlanGuid, bool preventDuplicateChanges = true);

        /// <summary>
        /// Gets the currently active power plan GUID
        /// </summary>
        Task<string?> GetActivePowerPlanGuidAsync();

        /// <summary>
        /// Checks if a power plan with the given GUID exists
        /// </summary>
        Task<bool> PowerPlanExistsAsync(string powerPlanGuid);

        /// <summary>
        /// Gets a power plan by GUID
        /// </summary>
        Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string powerPlanGuid);

        /// <summary>
        /// Validates that a power plan change is necessary
        /// </summary>
        Task<bool> IsPowerPlanChangeNeededAsync(string targetPowerPlanGuid);
    }

    /// <summary>
    /// Event arguments for power plan changes
    /// </summary>
    public class PowerPlanChangedEventArgs : EventArgs
    {
        public PowerPlanModel? PreviousPowerPlan { get; }
        public PowerPlanModel? NewPowerPlan { get; }
        public DateTime Timestamp { get; }
        public string? Reason { get; }

        public PowerPlanChangedEventArgs(PowerPlanModel? previousPowerPlan, PowerPlanModel? newPowerPlan, string? reason = null)
        {
            PreviousPowerPlan = previousPowerPlan;
            NewPowerPlan = newPowerPlan;
            Timestamp = DateTime.Now;
            Reason = reason;
        }
    }
}
