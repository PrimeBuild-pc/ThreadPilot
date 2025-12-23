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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing CPU core affinity masks
    /// </summary>
    public interface ICoreMaskService
    {
        /// <summary>
        /// Gets all available core masks
        /// </summary>
        ObservableCollection<CoreMask> AvailableMasks { get; }

        /// <summary>
        /// Gets the default mask (all cores)
        /// </summary>
        CoreMask? DefaultMask { get; }

        /// <summary>
        /// Initializes the service and loads masks from storage
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Creates a new core mask
        /// </summary>
        Task<CoreMask> CreateMaskAsync(string name, string description, IEnumerable<bool> boolMask);

        /// <summary>
        /// Updates an existing mask
        /// </summary>
        Task UpdateMaskAsync(CoreMask mask);

        /// <summary>
        /// Deletes a mask by ID
        /// </summary>
        Task DeleteMaskAsync(string maskId);

        /// <summary>
        /// Gets a mask by ID
        /// </summary>
        CoreMask? GetMaskById(string maskId);

        /// <summary>
        /// Gets a mask by name
        /// </summary>
        CoreMask? GetMaskByName(string name);

        /// <summary>
        /// Saves all masks to persistent storage
        /// </summary>
        Task SaveMasksAsync();

        /// <summary>
        /// Loads masks from persistent storage
        /// </summary>
        Task LoadMasksAsync();

        /// <summary>
        /// Checks if a mask is referenced by any profile or rule (not necessarily active)
        /// </summary>
        Task<bool> IsMaskReferencedByProfilesAsync(string maskId);

        /// <summary>
        /// Checks if a mask is actively applied to any running process
        /// </summary>
        Task<bool> IsMaskActivelyAppliedAsync(string maskId);

        /// <summary>
        /// Gets the names of profiles/rules that reference a specific mask
        /// </summary>
        Task<IEnumerable<string>> GetProfilesReferencingMaskAsync(string maskId);

        /// <summary>
        /// Updates all profiles referencing a mask to use the default "All Cores" mask
        /// </summary>
        Task UpdateProfilesToDefaultMaskAsync(string maskId);

        /// <summary>
        /// Creates default masks for the system based on CPU topology
        /// </summary>
        Task CreateDefaultMasksAsync();

        /// <summary>
        /// Gets the "All Cores" baseline mask (cannot be deleted)
        /// </summary>
        CoreMask? GetAllCoresMask();
    }
}

