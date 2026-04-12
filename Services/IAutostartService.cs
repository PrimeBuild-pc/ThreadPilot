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

    /// <summary>
    /// Interface for managing Windows autostart functionality.
    /// </summary>
    public interface IAutostartService
    {
        /// <summary>
        /// Occurs when autostart state changes.
        /// </summary>
        event EventHandler<AutostartStatusChangedEventArgs>? AutostartStatusChanged;

        /// <summary>
        /// Gets a value indicating whether gets whether the application is currently set to autostart with Windows.
        /// </summary>
        bool IsAutostartEnabled { get; }

        /// <summary>
        /// Gets the current autostart registry entry path.
        /// </summary>
        string? AutostartPath { get; }

        /// <summary>
        /// Enables autostart with Windows.
        /// </summary>
        /// <param name="startMinimized">Whether to start the application minimized.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> EnableAutostartAsync(bool startMinimized = true);

        /// <summary>
        /// Disables autostart with Windows.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> DisableAutostartAsync();

        /// <summary>
        /// Checks if autostart is currently enabled.
        /// </summary>
        /// <returns>True if autostart is enabled, false otherwise.</returns>
        Task<bool> CheckAutostartStatusAsync();

        /// <summary>
        /// Updates the autostart entry with new parameters.
        /// </summary>
        /// <param name="startMinimized">Whether to start minimized.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> UpdateAutostartAsync(bool startMinimized = true);

        /// <summary>
        /// Gets the command line arguments for autostart.
        /// </summary>
        /// <param name="startMinimized">Whether to include start minimized flag.</param>
        /// <returns>Command line arguments string.</returns>
        string GetAutostartArguments(bool startMinimized = true);
    }

    /// <summary>
    /// Event arguments for autostart status changes.
    /// </summary>
    public class AutostartStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets a value indicating whether autostart is currently enabled.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether startup should launch minimized.
        /// </summary>
        public bool StartMinimized { get; }

        /// <summary>
        /// Gets the registry command value currently used for startup.
        /// </summary>
        public string? RegistryPath { get; }

        /// <summary>
        /// Gets the error that caused the status update when the operation failed.
        /// </summary>
        public Exception? Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutostartStatusChangedEventArgs"/> class.
        /// </summary>
        /// <param name="isEnabled">Whether autostart is enabled.</param>
        /// <param name="startMinimized">Whether startup should launch minimized.</param>
        /// <param name="registryPath">The autostart registry value when available.</param>
        /// <param name="error">The failure that occurred, if any.</param>
        public AutostartStatusChangedEventArgs(bool isEnabled, bool startMinimized = false, string? registryPath = null, Exception? error = null)
        {
            this.IsEnabled = isEnabled;
            this.StartMinimized = startMinimized;
            this.RegistryPath = registryPath;
            this.Error = error;
        }
    }
}

