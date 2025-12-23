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
    /// Service for detecting and managing CPU topology information
    /// </summary>
    public interface ICpuTopologyService
    {
        /// <summary>
        /// Event fired when CPU topology is detected or updated
        /// </summary>
        event EventHandler<CpuTopologyDetectedEventArgs>? TopologyDetected;

        /// <summary>
        /// Gets the current CPU topology information
        /// </summary>
        CpuTopologyModel? CurrentTopology { get; }

        /// <summary>
        /// Detects CPU topology information
        /// </summary>
        Task<CpuTopologyModel> DetectTopologyAsync();

        /// <summary>
        /// Gets available affinity presets based on current topology
        /// </summary>
        IEnumerable<CpuAffinityPreset> GetAffinityPresets();

        /// <summary>
        /// Validates if an affinity mask is valid for the current system
        /// </summary>
        bool IsAffinityMaskValid(long affinityMask);

        /// <summary>
        /// Gets the maximum number of logical cores supported
        /// </summary>
        int GetMaxLogicalCores();

        /// <summary>
        /// Refreshes topology information (useful for hot-plug scenarios)
        /// </summary>
        Task RefreshTopologyAsync();
    }

    /// <summary>
    /// Event arguments for CPU topology detection
    /// </summary>
    public class CpuTopologyDetectedEventArgs : EventArgs
    {
        public CpuTopologyModel Topology { get; }
        public bool DetectionSuccessful { get; }
        public string? ErrorMessage { get; }
        public DateTime DetectionTime { get; }

        public CpuTopologyDetectedEventArgs(CpuTopologyModel topology, bool successful, string? errorMessage = null)
        {
            Topology = topology;
            DetectionSuccessful = successful;
            ErrorMessage = errorMessage;
            DetectionTime = DateTime.Now;
        }
    }
}

