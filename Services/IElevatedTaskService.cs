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
    using System.Threading.Tasks;

    /// <summary>
    /// Service for managing Scheduled Task-based elevation entrypoints.
    /// </summary>
    public interface IElevatedTaskService
    {
        /// <summary>
        /// Gets the managed task name used for on-demand elevated launch.
        /// </summary>
        string LaunchTaskName { get; }

        /// <summary>
        /// Gets the managed task name used for elevated logon autostart.
        /// </summary>
        string AutostartTaskName { get; }

        /// <summary>
        /// Ensures the managed on-demand elevated launch task exists and targets the current executable.
        /// </summary>
        /// <returns>True when the task exists and is up to date; otherwise false.</returns>
        Task<bool> EnsureLaunchTaskAsync();

        /// <summary>
        /// Runs the managed on-demand elevated launch task.
        /// </summary>
        /// <returns>True if task execution was started successfully; otherwise false.</returns>
        Task<bool> TryRunLaunchTaskAsync();

        /// <summary>
        /// Ensures elevated autostart task exists and points to the provided executable command.
        /// </summary>
        /// <param name="executablePath">Absolute executable path.</param>
        /// <param name="arguments">Command-line arguments for autostart launches.</param>
        /// <returns>True if task was created/updated successfully; otherwise false.</returns>
        Task<bool> EnsureAutostartTaskAsync(string executablePath, string arguments);

        /// <summary>
        /// Removes the managed elevated autostart task.
        /// </summary>
        /// <returns>True when the task is removed (or already absent); otherwise false.</returns>
        Task<bool> RemoveAutostartTaskAsync();

        /// <summary>
        /// Checks whether the managed elevated autostart task is currently registered.
        /// </summary>
        /// <returns>True when the task exists; otherwise false.</returns>
        Task<bool> IsAutostartTaskRegisteredAsync();
    }
}
