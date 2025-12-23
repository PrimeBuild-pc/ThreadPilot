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
using System.Diagnostics;
using System.Threading.Tasks;
using ThreadPilot.Models;

namespace ThreadPilot.Services
{
    public interface IProcessService
    {
        Task<ObservableCollection<ProcessModel>> GetProcessesAsync();
        Task SetProcessorAffinity(ProcessModel process, long affinityMask);
        Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority);
        Task<bool> SaveProcessProfile(string profileName, ProcessModel process);
        Task<bool> LoadProcessProfile(string profileName, ProcessModel process);
        Task RefreshProcessInfo(ProcessModel process);

        /// <summary>
        /// Gets a process by its ID
        /// </summary>
        Task<ProcessModel?> GetProcessByIdAsync(int processId);

        /// <summary>
        /// Gets processes by executable name
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetProcessesByNameAsync(string executableName);

        /// <summary>
        /// Checks if a process with the given name is currently running
        /// </summary>
        Task<bool> IsProcessRunningAsync(string executableName);

        /// <summary>
        /// Gets all running processes with their executable paths
        /// </summary>
        Task<IEnumerable<ProcessModel>> GetProcessesWithPathsAsync();

        /// <summary>
        /// Gets only active applications with visible windows (user-facing applications)
        /// </summary>
        Task<ObservableCollection<ProcessModel>> GetActiveApplicationsAsync();

        /// <summary>
        /// Creates a ProcessModel from a System.Diagnostics.Process
        /// </summary>
        ProcessModel CreateProcessModel(Process process);

        /// <summary>
        /// Checks if a specific process is still running
        /// </summary>
        Task<bool> IsProcessStillRunning(ProcessModel process);

        /// <summary>
        /// Sets the idle server state for a process (disables/enables idle functionality)
        /// </summary>
        Task<bool> SetIdleServerStateAsync(ProcessModel process, bool enableIdleServer);

        /// <summary>
        /// Sets registry-based priority enforcement for a process
        /// </summary>
        Task<bool> SetRegistryPriorityAsync(ProcessModel process, bool enable, ProcessPriorityClass priority);

        /// <summary>
        /// Enables or disables the use of Windows CPU Sets for affinity management
        /// </summary>
        void SetUseCpuSets(bool useCpuSets);

        /// <summary>
        /// Gets whether CPU Sets are currently enabled for affinity management
        /// </summary>
        bool GetUseCpuSets();

        /// <summary>
        /// Clears the CPU Set for a process (allows it to run on all cores)
        /// </summary>
        Task<bool> ClearProcessCpuSetAsync(ProcessModel process);

        /// <summary>
        /// Clears all applied CPU masks/affinities from all tracked processes
        /// Processes return to using all cores (used on application exit)
        /// </summary>
        Task ClearAllAppliedMasksAsync();

        /// <summary>
        /// Resets all modified process priorities to Normal
        /// (used on application exit)
        /// </summary>
        Task ResetAllProcessPrioritiesAsync();

        /// <summary>
        /// Registers that a mask has been applied to a process (for tracking)
        /// </summary>
        void TrackAppliedMask(int processId, string maskId);

        /// <summary>
        /// Registers that a priority has been changed for a process (for tracking)
        /// </summary>
        void TrackPriorityChange(int processId, ProcessPriorityClass originalPriority);

        /// <summary>
        /// Unregisters tracking when a process exits
        /// </summary>
        void UntrackProcess(int processId);
    }
}
