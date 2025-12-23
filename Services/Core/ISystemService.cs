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

namespace ThreadPilot.Services.Core
{
    /// <summary>
    /// Base interface for core system services that interact directly with the operating system
    /// </summary>
    public interface ISystemService
    {
        /// <summary>
        /// Gets whether the service is currently available and functional
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Event fired when the service availability changes
        /// </summary>
        event EventHandler<ServiceAvailabilityChangedEventArgs>? AvailabilityChanged;

        /// <summary>
        /// Initialize the service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Cleanup and dispose of service resources
        /// </summary>
        Task DisposeAsync();
    }

    /// <summary>
    /// Event args for service availability changes
    /// </summary>
    public class ServiceAvailabilityChangedEventArgs : EventArgs
    {
        public bool IsAvailable { get; }
        public string? Reason { get; }

        public ServiceAvailabilityChangedEventArgs(bool isAvailable, string? reason = null)
        {
            IsAvailable = isAvailable;
            Reason = reason;
        }
    }
}

