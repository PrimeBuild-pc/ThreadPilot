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
    using System.Collections.ObjectModel;
    using System.Linq;
    using ThreadPilot.Models;

    public sealed record ProcessListDeltaResult(ProcessModel? SelectedProcess, bool SelectedProcessTerminated);

    /// <summary>
    /// Applies process snapshots to the UI collection while preserving existing models by PID.
    /// </summary>
    public static class ProcessListDeltaUpdater
    {
        public static ProcessListDeltaResult ApplyDelta(
            ObservableCollection<ProcessModel> target,
            IEnumerable<ProcessModel> snapshot,
            int? selectedProcessId)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(snapshot);

            var currentByPid = target
                .GroupBy(process => process.ProcessId)
                .ToDictionary(group => group.Key, group => group.First());
            var snapshotByPid = new Dictionary<int, ProcessModel>();
            foreach (var process in snapshot)
            {
                snapshotByPid[process.ProcessId] = process;
            }

            var seenPids = new HashSet<int>();
            ProcessModel? selectedProcess = null;

            foreach (var incoming in snapshotByPid.Values)
            {
                seenPids.Add(incoming.ProcessId);

                if (currentByPid.TryGetValue(incoming.ProcessId, out var existing))
                {
                    CopyProcessState(incoming, existing);
                    if (selectedProcessId == incoming.ProcessId)
                    {
                        selectedProcess = existing;
                    }

                    continue;
                }

                target.Add(incoming);
                if (selectedProcessId == incoming.ProcessId)
                {
                    selectedProcess = incoming;
                }
            }

            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!seenPids.Contains(target[i].ProcessId))
                {
                    target.RemoveAt(i);
                }
            }

            var selectedProcessTerminated = selectedProcessId.HasValue && selectedProcess == null;
            return new ProcessListDeltaResult(selectedProcess, selectedProcessTerminated);
        }

        private static void CopyProcessState(ProcessModel source, ProcessModel target)
        {
            target.Name = source.Name;
            target.ExecutablePath = source.ExecutablePath;
            target.CpuUsage = source.CpuUsage;
            target.MemoryUsage = source.MemoryUsage;
            target.Priority = source.Priority;
            target.ProcessorAffinity = source.ProcessorAffinity;
            target.MainWindowHandle = source.MainWindowHandle;
            target.MainWindowTitle = source.MainWindowTitle;
            target.HasVisibleWindow = source.HasVisibleWindow;
            target.IsForeground = source.IsForeground;
            target.Classification = source.Classification;
            target.IsIdleServerDisabled = source.IsIdleServerDisabled;
            target.IsRegistryPriorityEnabled = source.IsRegistryPriorityEnabled;
        }
    }
}
