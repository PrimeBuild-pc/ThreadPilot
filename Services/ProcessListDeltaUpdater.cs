namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using ThreadPilot.Models;

    public sealed record ProcessListDeltaResult(ProcessModel? SelectedProcess, bool SelectedProcessTerminated);

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

        /// <summary>
        /// Makes <paramref name="target"/> hold exactly <paramref name="desired"/>, in order, by removing, moving
        /// and inserting items instead of replacing the collection: the grid keeps its rows, selection and the
        /// sort the user picked from a column header.
        /// </summary>
        public static void SyncInPlace(ObservableCollection<ProcessModel> target, IReadOnlyList<ProcessModel> desired)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(desired);

            var wanted = new HashSet<ProcessModel>(desired, ReferenceEqualityComparer.Instance);
            for (var i = target.Count - 1; i >= 0; i--)
            {
                if (!wanted.Contains(target[i]))
                {
                    target.RemoveAt(i);
                }
            }

            for (var i = 0; i < desired.Count; i++)
            {
                if (i < target.Count && ReferenceEquals(target[i], desired[i]))
                {
                    continue;
                }

                var current = IndexOf(target, desired[i], i + 1);
                if (current >= 0)
                {
                    target.Move(current, i);
                }
                else
                {
                    target.Insert(i, desired[i]);
                }
            }
        }

        private static int IndexOf(ObservableCollection<ProcessModel> items, ProcessModel item, int start)
        {
            for (var i = start; i < items.Count; i++)
            {
                if (ReferenceEquals(items[i], item))
                {
                    return i;
                }
            }

            return -1;
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
