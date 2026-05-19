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
namespace ThreadPilot.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Identifies a logical processor without relying on a legacy 64-bit affinity mask.
    /// </summary>
    public readonly record struct ProcessorRef(ushort Group, byte LogicalProcessorNumber, int GlobalIndex);

    /// <summary>
    /// Stable signature used to determine whether a persisted CPU selection was created for the current topology.
    /// </summary>
    public sealed record CpuTopologySignature
    {
        public string CpuBrand { get; init; } = "Unknown";

        public int LogicalProcessorCount { get; init; }

        public int PhysicalCoreCount { get; init; }

        public int ProcessorGroupCount { get; init; } = 1;

        public int NumaNodeCount { get; init; }

        public int LastLevelCacheGroupCount { get; init; }

        public string Source { get; init; } = "Unknown";
    }

    /// <summary>
    /// Metadata that explains how a CPU selection was built and whether it can be represented by legacy APIs.
    /// </summary>
    public sealed record CpuSelectionMetadata
    {
        public CpuTopologySignature? TopologySignature { get; init; }

        public bool CreatedFromLegacyAffinityMask { get; init; }

        public bool ContainsLogicalProcessorsBeyondLegacyMask { get; init; }

        public bool HasMultipleProcessorGroups { get; init; }

        public int ProcessorGroupCount { get; init; }

        public int MaxGlobalLogicalProcessorIndex { get; init; } = -1;

        public string SelectionReason { get; init; } = string.Empty;
    }

    /// <summary>
    /// Lightweight topology snapshot used by the CpuSelection migration layer.
    /// Runtime topology detection will populate this in a later phase.
    /// </summary>
    public sealed class CpuTopologySnapshot
    {
        private readonly IReadOnlyDictionary<ProcessorRef, uint> cpuSetIdsByProcessor;
        private readonly IReadOnlyDictionary<ProcessorRef, byte> efficiencyClassesByProcessor;

        private CpuTopologySnapshot(
            IReadOnlyList<ProcessorRef> logicalProcessors,
            IReadOnlyDictionary<ProcessorRef, uint> cpuSetIdsByProcessor,
            IReadOnlyDictionary<ProcessorRef, byte> efficiencyClassesByProcessor,
            CpuTopologySignature signature)
        {
            this.LogicalProcessors = logicalProcessors;
            this.cpuSetIdsByProcessor = cpuSetIdsByProcessor;
            this.efficiencyClassesByProcessor = efficiencyClassesByProcessor;
            this.Signature = signature;
        }

        public IReadOnlyList<ProcessorRef> LogicalProcessors { get; }

        public CpuTopologySignature Signature { get; }

        public static CpuTopologySnapshot Create(
            IEnumerable<ProcessorRef> logicalProcessors,
            IReadOnlyDictionary<ProcessorRef, uint>? cpuSetIds = null,
            IReadOnlyDictionary<ProcessorRef, byte>? efficiencyClasses = null,
            CpuTopologySignature? signature = null)
        {
            ArgumentNullException.ThrowIfNull(logicalProcessors);

            var processors = logicalProcessors
                .Distinct()
                .OrderBy(processor => processor.GlobalIndex)
                .ThenBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();

            var processorSet = processors.ToHashSet();
            var cpuSetMap = cpuSetIds?
                .Where(kvp => processorSet.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                ?? new Dictionary<ProcessorRef, uint>();

            var efficiencyClassMap = efficiencyClasses?
                .Where(kvp => processorSet.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                ?? new Dictionary<ProcessorRef, byte>();

            var resolvedSignature = signature ?? new CpuTopologySignature
            {
                LogicalProcessorCount = processors.Count,
                ProcessorGroupCount = processors.Select(processor => processor.Group).Distinct().Count(),
                Source = "Snapshot",
            };

            return new CpuTopologySnapshot(processors, cpuSetMap, efficiencyClassMap, resolvedSignature);
        }

        public bool TryGetCpuSetId(ProcessorRef processor, out uint cpuSetId) =>
            this.cpuSetIdsByProcessor.TryGetValue(processor, out cpuSetId);

        public bool TryGetEfficiencyClass(ProcessorRef processor, out byte efficiencyClass) =>
            this.efficiencyClassesByProcessor.TryGetValue(processor, out efficiencyClass);

        public byte? GetPerformanceEfficiencyClass()
        {
            if (this.efficiencyClassesByProcessor.Count == 0)
            {
                return null;
            }

            return this.efficiencyClassesByProcessor.Values.Max();
        }
    }

    /// <summary>
    /// Group-aware CPU selection model used by new persistence and migration code.
    /// </summary>
    public sealed record CpuSelection
    {
        public List<uint> CpuSetIds { get; init; } = new();

        public List<ProcessorRef> LogicalProcessors { get; init; } = new();

        public List<int> GlobalLogicalProcessorIndexes { get; init; } = new();

        public CpuSelectionMetadata Metadata { get; init; } = new();

        public static CpuSelection FromProcessors(
            IEnumerable<ProcessorRef> processors,
            CpuTopologySnapshot topology,
            string selectionReason = "")
        {
            ArgumentNullException.ThrowIfNull(processors);
            ArgumentNullException.ThrowIfNull(topology);

            var selectedProcessors = processors
                .Distinct()
                .OrderBy(processor => processor.GlobalIndex)
                .ThenBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();

            var cpuSetIds = selectedProcessors
                .Select(processor => topology.TryGetCpuSetId(processor, out var cpuSetId) ? (uint?)cpuSetId : null)
                .Where(cpuSetId => cpuSetId.HasValue)
                .Select(cpuSetId => cpuSetId!.Value)
                .Distinct()
                .OrderBy(cpuSetId => cpuSetId)
                .ToList();

            return new CpuSelection
            {
                CpuSetIds = cpuSetIds,
                LogicalProcessors = selectedProcessors,
                GlobalLogicalProcessorIndexes = selectedProcessors
                    .Select(processor => processor.GlobalIndex)
                    .Distinct()
                    .OrderBy(index => index)
                    .ToList(),
                Metadata = CreateMetadata(selectedProcessors, topology.Signature, createdFromLegacyAffinityMask: false, selectionReason),
            };
        }

        public static CpuSelection FromLegacyAffinityMask(long mask, CpuTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(topology);

            var unsignedMask = unchecked((ulong)mask);
            var selectedIndexes = new HashSet<int>();
            for (var bit = 0; bit < 64; bit++)
            {
                if ((unsignedMask & (1UL << bit)) != 0)
                {
                    selectedIndexes.Add(bit);
                }
            }

            var selectedProcessors = topology.LogicalProcessors
                .Where(processor => selectedIndexes.Contains(processor.GlobalIndex))
                .ToList();

            var selection = FromProcessors(selectedProcessors, topology, "Migrated from legacy affinity mask");
            return selection with
            {
                Metadata = CreateMetadata(
                    selection.LogicalProcessors,
                    topology.Signature,
                    createdFromLegacyAffinityMask: true,
                    "Migrated from legacy affinity mask"),
            };
        }

        public static long? ToLegacyAffinityMaskOrNull(CpuSelection selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            if (selection.LogicalProcessors.Any(processor => processor.GlobalIndex >= 64))
            {
                return null;
            }

            if (selection.LogicalProcessors.Select(processor => processor.Group).Distinct().Count() > 1)
            {
                return null;
            }

            long mask = 0;
            foreach (var processor in selection.LogicalProcessors)
            {
                if (processor.GlobalIndex < 0)
                {
                    return null;
                }

                mask |= 1L << processor.GlobalIndex;
            }

            return mask;
        }

        private static CpuSelectionMetadata CreateMetadata(
            IReadOnlyCollection<ProcessorRef> processors,
            CpuTopologySignature signature,
            bool createdFromLegacyAffinityMask,
            string selectionReason)
        {
            var groups = processors.Select(processor => processor.Group).Distinct().ToList();
            var maxGlobalIndex = processors.Count == 0
                ? -1
                : processors.Max(processor => processor.GlobalIndex);

            return new CpuSelectionMetadata
            {
                TopologySignature = signature,
                CreatedFromLegacyAffinityMask = createdFromLegacyAffinityMask,
                ContainsLogicalProcessorsBeyondLegacyMask = maxGlobalIndex >= 64,
                HasMultipleProcessorGroups = groups.Count > 1,
                ProcessorGroupCount = groups.Count,
                MaxGlobalLogicalProcessorIndex = maxGlobalIndex,
                SelectionReason = selectionReason,
            };
        }
    }
}
