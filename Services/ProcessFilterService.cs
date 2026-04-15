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
    using System.Collections.Generic;
    using System.Linq;
    using ThreadPilot.Models;

    /// <summary>
    /// Filter options used for process-list querying from the ViewModel.
    /// </summary>
    public sealed class ProcessFilterCriteria
    {
        public string SearchText { get; init; } = string.Empty;

        public bool HideSystemProcesses { get; init; }

        public bool HideIdleProcesses { get; init; }

        public string SortMode { get; init; } = "CpuUsage";
    }

    /// <summary>
    /// Service for filtering and sorting process collections.
    /// </summary>
    public class ProcessFilterService
    {
        private static readonly string[] SystemProcessNames =
        {
            "System", "Registry", "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe",
            "services.exe", "lsass.exe", "svchost.exe", "spoolsv.exe", "explorer.exe",
            "dwm.exe", "audiodg.exe", "conhost.exe", "dllhost.exe", "rundll32.exe",
            "taskhostw.exe", "SearchIndexer.exe", "WmiPrvSE.exe", "MsMpEng.exe",
            "SecurityHealthService.exe", "SecurityHealthSystray.exe",
        };

        /// <summary>
        /// Applies filter criteria and returns sorted process results.
        /// </summary>
        public IReadOnlyList<ProcessModel> FilterAndSort(IEnumerable<ProcessModel> source, ProcessFilterCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(criteria);

            var filtered = source;

            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                filtered = filtered.Where(p => p.Name.Contains(criteria.SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.HideSystemProcesses)
            {
                filtered = filtered.Where(p => !IsSystemProcess(p));
            }

            if (criteria.HideIdleProcesses)
            {
                filtered = filtered.Where(p => p.CpuUsage > 0.1);
            }

            var sorted = criteria.SortMode switch
            {
                "CpuUsage" => filtered.OrderByDescending(p => p.CpuUsage),
                "MemoryUsage" => filtered.OrderByDescending(p => p.MemoryUsage),
                "Name" => filtered.OrderBy(p => p.Name),
                "ProcessId" => filtered.OrderBy(p => p.ProcessId),
                _ => filtered.OrderByDescending(p => p.CpuUsage),
            };

            return sorted.ToList();
        }

        private static bool IsSystemProcess(ProcessModel process)
        {
            if (process == null)
            {
                return false;
            }

            return SystemProcessNames.Any(sp => process.Name.Equals(sp, StringComparison.OrdinalIgnoreCase)) ||
                   process.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase);
        }
    }
}
