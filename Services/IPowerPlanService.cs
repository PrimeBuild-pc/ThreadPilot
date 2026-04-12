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
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface IPowerPlanService
    {
        /// <summary>
        /// Occurs when the active power plan changes.
        /// </summary>
        event EventHandler<PowerPlanChangedEventArgs>? PowerPlanChanged;

        /// <summary>
        /// Retrieves all power plans currently available on the system.
        /// </summary>
        /// <returns>Collection of power plans with active-state metadata.</returns>
        Task<ObservableCollection<PowerPlanModel>> GetPowerPlansAsync();

        /// <summary>
        /// Retrieves custom power plans discovered in the managed plans directory.
        /// </summary>
        /// <returns>Collection of importable custom power plans.</returns>
        Task<ObservableCollection<PowerPlanModel>> GetCustomPowerPlansAsync();

        /// <summary>
        /// Sets the active plan using a <see cref="PowerPlanModel"/> instance.
        /// </summary>
        /// <param name="powerPlan">Power plan to activate.</param>
        /// <returns><see langword="true"/> when activation succeeds; otherwise <see langword="false"/>.</returns>
        Task<bool> SetActivePowerPlan(PowerPlanModel powerPlan);

        /// <summary>
        /// Gets the current active power plan.
        /// </summary>
        /// <returns>The active power plan, or <see langword="null"/> when unavailable.</returns>
        Task<PowerPlanModel?> GetActivePowerPlan();

        /// <summary>
        /// Imports a custom power plan from a .pow file.
        /// </summary>
        /// <param name="filePath">Absolute path to the source .pow file.</param>
        /// <returns><see langword="true"/> when import succeeds; otherwise <see langword="false"/>.</returns>
        Task<bool> ImportCustomPowerPlan(string filePath);

        /// <summary>
        /// Sets the active power plan by GUID with duplicate change prevention.
        /// </summary>
        /// <param name="powerPlanGuid">Target power plan GUID.</param>
        /// <param name="preventDuplicateChanges">Whether to skip redundant changes when already active.</param>
        /// <returns><see langword="true"/> when the operation succeeds; otherwise <see langword="false"/>.</returns>
        Task<bool> SetActivePowerPlanByGuidAsync(string powerPlanGuid, bool preventDuplicateChanges = true);

        /// <summary>
        /// Gets the currently active power plan GUID.
        /// </summary>
        /// <returns>Active plan GUID, or <see langword="null"/> when unavailable.</returns>
        Task<string?> GetActivePowerPlanGuidAsync();

        /// <summary>
        /// Checks if a power plan with the given GUID exists.
        /// </summary>
        /// <param name="powerPlanGuid">Power plan GUID to check.</param>
        /// <returns><see langword="true"/> when the plan exists; otherwise <see langword="false"/>.</returns>
        Task<bool> PowerPlanExistsAsync(string powerPlanGuid);

        /// <summary>
        /// Gets a power plan by GUID.
        /// </summary>
        /// <param name="powerPlanGuid">Power plan GUID.</param>
        /// <returns>Matching plan when found; otherwise <see langword="null"/>.</returns>
        Task<PowerPlanModel?> GetPowerPlanByGuidAsync(string powerPlanGuid);

        /// <summary>
        /// Validates that a power plan change is necessary.
        /// </summary>
        /// <param name="targetPowerPlanGuid">Target plan GUID.</param>
        /// <returns><see langword="true"/> when a change should be applied; otherwise <see langword="false"/>.</returns>
        Task<bool> IsPowerPlanChangeNeededAsync(string targetPowerPlanGuid);
    }

    /// <summary>
    /// Event arguments for power plan changes.
    /// </summary>
    public class PowerPlanChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previously active power plan.
        /// </summary>
        public PowerPlanModel? PreviousPowerPlan { get; }

        /// <summary>
        /// Gets the newly active power plan.
        /// </summary>
        public PowerPlanModel? NewPowerPlan { get; }

        /// <summary>
        /// Gets the local timestamp when the change was recorded.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the optional reason for the power plan transition.
        /// </summary>
        public string? Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerPlanChangedEventArgs"/> class.
        /// </summary>
        /// <param name="previousPowerPlan">Power plan active before the change.</param>
        /// <param name="newPowerPlan">Power plan active after the change.</param>
        /// <param name="reason">Optional reason describing why the change occurred.</param>
        public PowerPlanChangedEventArgs(PowerPlanModel? previousPowerPlan, PowerPlanModel? newPowerPlan, string? reason = null)
        {
            this.PreviousPowerPlan = previousPowerPlan;
            this.NewPowerPlan = newPowerPlan;
            this.Timestamp = DateTime.Now;
            this.Reason = reason;
        }
    }
}
