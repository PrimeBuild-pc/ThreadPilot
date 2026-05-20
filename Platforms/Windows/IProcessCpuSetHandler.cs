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
namespace ThreadPilot.Platforms.Windows
{
    using System;
    using ThreadPilot.Models;

    /// <summary>
    /// Interface for handling CPU Set operations on a specific process.
    /// </summary>
    public interface IProcessCpuSetHandler : IDisposable
    {
        /// <summary>
        /// Gets the process ID this handler manages.
        /// </summary>
        uint ProcessId { get; }

        /// <summary>
        /// Gets the executable name.
        /// </summary>
        string ExecutableName { get; }

        /// <summary>
        /// Applies a CPU affinity mask to the process using CPU Sets.
        /// This legacy path is valid only for single-processor-group systems with up to
        /// 64 logical processors. It will be superseded by <see cref="ApplyCpuSelection"/>
        /// for topology-aware CPU Set selection.
        /// </summary>
        /// <param name="affinityMask">The affinity mask where each bit represents a logical processor.</param>
        /// <param name="clearMask">If true, clears the CPU Set (allows all cores); if false, applies the mask.</param>
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        bool ApplyCpuSetMask(long affinityMask, bool clearMask = false);

        /// <summary>
        /// Applies a CPU affinity mask to the process using CPU Sets and returns detailed failure information.
        /// </summary>
        /// <param name="affinityMask">The affinity mask where each bit represents a logical processor.</param>
        /// <param name="clearMask">If true, clears the CPU Set (allows all cores); if false, applies the mask.</param>
        /// <returns>Detailed CPU Set apply result.</returns>
        CpuSetApplyResult ApplyCpuSetMaskDetailed(long affinityMask, bool clearMask = false);

        /// <summary>
        /// Applies a topology-aware CPU selection to the process using CPU Sets.
        /// </summary>
        /// <param name="selection">The CPU selection to apply. Ignored and allowed to be null when <paramref name="clearSelection"/> is true.</param>
        /// <param name="clearSelection">If true, clears the CPU Set selection and ignores <paramref name="selection"/>.</param>
        /// <returns>True if the operation succeeded, false otherwise.</returns>
        bool ApplyCpuSelection(CpuSelection? selection, bool clearSelection = false);

        /// <summary>
        /// Applies a topology-aware CPU selection to the process using CPU Sets and returns detailed failure information.
        /// </summary>
        /// <param name="selection">The CPU selection to apply. Ignored and allowed to be null when <paramref name="clearSelection"/> is true.</param>
        /// <param name="clearSelection">If true, clears the CPU Set selection and ignores <paramref name="selection"/>.</param>
        /// <returns>Detailed CPU Set apply result.</returns>
        CpuSetApplyResult ApplyCpuSelectionDetailed(CpuSelection? selection, bool clearSelection = false);

        /// <summary>
        /// Gets the average CPU usage for this process.
        /// </summary>
        /// <returns>CPU usage percentage (0-1 range), or -1 if unavailable.</returns>
        double GetAverageCpuUsage();

        /// <summary>
        /// Gets a value indicating whether checks if the handler has valid handles to the process.
        /// </summary>
        bool IsValid { get; }
    }
}
