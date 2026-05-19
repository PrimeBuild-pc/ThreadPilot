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
    using ThreadPilot.Models;

    public sealed class CpuPresetGenerator : ICpuPresetGenerator
    {
        private const string GamingWarning =
            "Suggested default. Results may vary by game and system. You can edit or delete this preset.";

        public IReadOnlyList<CpuPreset> Generate(
            CpuTopologySnapshot topology,
            CpuPresetGenerationOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(topology);

            var resolvedOptions = options ?? new CpuPresetGenerationOptions();
            var presets = new List<CpuPreset>();

            AddPreset(
                presets,
                resolvedOptions,
                CreatePreset(
                    "all-cores",
                    "All cores",
                    "Use every logical processor reported by the current CPU topology.",
                    topology.LogicalProcessors,
                    topology,
                    "Uses all logical processors from the topology snapshot."));

            var allPhysicalProcessors = HasCoreIndexForAllProcessors(topology)
                ? SelectOneLogicalProcessorPerCore(topology.LogicalProcessors, topology)
                : [];
            if (allPhysicalProcessors.Count > 0)
            {
                AddPreset(
                    presets,
                    resolvedOptions,
                    CreatePreset(
                        "all-physical-cores",
                        "All physical cores / no SMT",
                        "Use one logical processor per physical core.",
                        allPhysicalProcessors,
                        topology,
                        "Uses CoreIndex and SMT sibling metadata to select one logical processor per core."));
            }

            var allExceptCpu0 = topology.LogicalProcessors
                .Where(processor => processor.GlobalIndex != 0)
                .ToList();
            if (topology.LogicalProcessors.Count >= 2 && allExceptCpu0.Count > 0)
            {
                AddPreset(
                    presets,
                    resolvedOptions,
                    CreatePreset(
                        "all-except-cpu0",
                        "All except CPU0",
                        "Use every logical processor except global CPU index 0.",
                        allExceptCpu0,
                        topology,
                        "Excludes GlobalIndex 0 while keeping the remaining topology-aware processor refs."));
            }

            var efficiencyClasses = topology.LogicalProcessors
                .Select(processor => topology.TryGetEfficiencyClass(processor, out var efficiencyClass)
                    ? (byte?)efficiencyClass
                    : null)
                .Where(efficiencyClass => efficiencyClass.HasValue)
                .Select(efficiencyClass => efficiencyClass!.Value)
                .Distinct()
                .OrderBy(efficiencyClass => efficiencyClass)
                .ToList();

            var hasDistinctEfficiencyClasses = efficiencyClasses.Count >= 2;
            List<ProcessorRef> pCoreProcessors = [];
            if (hasDistinctEfficiencyClasses)
            {
                var performanceClass = efficiencyClasses.Max();
                pCoreProcessors = topology.LogicalProcessors
                    .Where(processor =>
                        topology.TryGetEfficiencyClass(processor, out var efficiencyClass) &&
                        efficiencyClass == performanceClass)
                    .ToList();
                var eCoreProcessors = topology.LogicalProcessors
                    .Where(processor =>
                        topology.TryGetEfficiencyClass(processor, out var efficiencyClass) &&
                        efficiencyClass < performanceClass)
                    .ToList();

                AddPreset(
                    presets,
                    resolvedOptions,
                    CreatePreset(
                        "p-cores-only",
                        "P-cores only",
                        "Use logical processors in the highest EfficiencyClass.",
                        pCoreProcessors,
                        topology,
                        "Uses the highest EfficiencyClass in the topology snapshot as performance cores."));

                if (HasCoreIndexForProcessors(pCoreProcessors, topology))
                {
                    AddPreset(
                        presets,
                        resolvedOptions,
                        CreatePreset(
                            "p-cores-no-smt",
                            "P-cores only / no SMT",
                            "Use one logical processor per performance core.",
                            SelectOneLogicalProcessorPerCore(pCoreProcessors, topology),
                            topology,
                            "Uses EfficiencyClass plus CoreIndex and SMT sibling metadata to choose one logical processor per P-core."));
                }

                AddPreset(
                    presets,
                    resolvedOptions,
                    CreatePreset(
                        "e-cores-only",
                        "E-cores only",
                        "Use logical processors below the highest EfficiencyClass.",
                        eCoreProcessors,
                        topology,
                        "Uses EfficiencyClass values below the performance class as efficiency cores.",
                        "Usually not recommended for games. Useful for background tasks."));
            }

            if (topology.Signature.LastLevelCacheGroupCount > 1 && HasCoreIndexForAllProcessors(topology))
            {
                var l3Groups = topology.LogicalProcessors
                    .Select(processor => topology.TryGetLastLevelCacheIndex(processor, out var cacheIndex)
                        ? new { Processor = processor, CacheIndex = (int?)cacheIndex }
                        : null)
                    .Where(item => item?.CacheIndex != null)
                    .GroupBy(item => item!.CacheIndex!.Value)
                    .OrderBy(group => group.Key);

                foreach (var group in l3Groups)
                {
                    AddPreset(
                        presets,
                        resolvedOptions,
                        CreatePreset(
                            $"l3-group-{group.Key}-physical",
                            $"L3 group {group.Key} / physical cores",
                            $"Use one logical processor per core in L3/cache group {group.Key}.",
                            SelectOneLogicalProcessorPerCore(group.Select(item => item!.Processor), topology),
                            topology,
                            $"Based on LastLevelCacheIndex/L3 cache group {group.Key}, not on CPU SKU naming."));
                }
            }

            var bestGamingSourceId = SelectBestGamingSourcePresetId(presets, resolvedOptions);
            if (bestGamingSourceId != null)
            {
                var sourcePreset = presets.Single(preset => preset.PresetId == bestGamingSourceId);
                AddPreset(
                    presets,
                    resolvedOptions,
                    CreatePreset(
                        "best-gaming",
                        "Best gaming suggestion",
                        "Suggested topology-aware starting point for games.",
                        sourcePreset.Selection.LogicalProcessors,
                        topology,
                        bestGamingSourceId,
                        GamingWarning));
            }

            AddPreset(
                presets,
                resolvedOptions,
                CreatePreset(
                    "safe-compatibility",
                    "Safe compatibility",
                    "Use every logical processor for maximum compatibility.",
                    topology.LogicalProcessors,
                    topology,
                    "Maximum compatibility."));

            // TODO: X3D CCD-only presets require reliable cache/topology detection.
            // Do not generate X3D CCD-only until it can be detected with confidence.
            return presets;
        }

        private static string? SelectBestGamingSourcePresetId(
            IReadOnlyList<CpuPreset> presets,
            CpuPresetGenerationOptions options)
        {
            var orderedCandidates = options.ExcludeCpu0ForGaming
                ? new[]
                {
                    "p-cores-no-smt",
                    "l3-group-0-physical",
                    "all-physical-cores",
                    "all-except-cpu0",
                    "all-cores",
                }
                : new[]
                {
                    "p-cores-no-smt",
                    "l3-group-0-physical",
                    "all-physical-cores",
                    "all-cores",
                };

            return orderedCandidates.FirstOrDefault(candidate =>
                presets.Any(preset => preset.PresetId == candidate));
        }

        private static bool HasCoreIndexForAllProcessors(CpuTopologySnapshot topology) =>
            HasCoreIndexForProcessors(topology.LogicalProcessors, topology);

        private static bool HasCoreIndexForProcessors(
            IEnumerable<ProcessorRef> processors,
            CpuTopologySnapshot topology)
        {
            var processorList = processors.ToList();
            return processorList.Count > 0 &&
                processorList.All(processor => topology.TryGetCoreIndex(processor, out _));
        }

        private static List<ProcessorRef> SelectOneLogicalProcessorPerCore(
            IEnumerable<ProcessorRef> processors,
            CpuTopologySnapshot topology)
        {
            return processors
                .Select(processor =>
                {
                    topology.TryGetCoreIndex(processor, out var coreIndex);
                    return new
                    {
                        Processor = processor,
                        CoreIndex = coreIndex,
                        SmtSiblingCount = topology.GetSmtSiblingGlobalIndexes(processor).Count,
                    };
                })
                .GroupBy(item => item.CoreIndex)
                .OrderBy(group => group.Key)
                .Select(group => group
                    .OrderBy(item => item.Processor.GlobalIndex)
                    .ThenBy(item => item.SmtSiblingCount)
                    .First()
                    .Processor)
                .ToList();
        }

        private static CpuPreset CreatePreset(
            string presetId,
            string name,
            string description,
            IEnumerable<ProcessorRef> processors,
            CpuTopologySnapshot topology,
            string reason,
            string? warning = null,
            bool reviewRequired = false)
        {
            var selectedProcessors = processors
                .Distinct()
                .OrderBy(processor => processor.GlobalIndex)
                .ThenBy(processor => processor.Group)
                .ThenBy(processor => processor.LogicalProcessorNumber)
                .ToList();

            return new CpuPreset
            {
                PresetId = presetId,
                Name = name,
                Description = description,
                Selection = CpuSelection.FromProcessors(selectedProcessors, topology, reason),
                Reason = reason,
                Warning = warning,
                GeneratedByTopologySignature = topology.Signature,
                IsUserEditable = true,
                IsGenerated = true,
                ReviewRequired = reviewRequired,
            };
        }

        private static void AddPreset(
            List<CpuPreset> presets,
            CpuPresetGenerationOptions options,
            CpuPreset preset)
        {
            if (options.DeletedGeneratedPresetIds.Contains(preset.PresetId) ||
                preset.Selection.LogicalProcessors.Count == 0 ||
                presets.Any(existing => existing.PresetId == preset.PresetId))
            {
                return;
            }

            var duplicateSamePurpose = presets.Any(existing =>
                existing.Reason == preset.Reason &&
                existing.Selection.GlobalLogicalProcessorIndexes.SequenceEqual(
                    preset.Selection.GlobalLogicalProcessorIndexes));
            if (duplicateSamePurpose)
            {
                return;
            }

            presets.Add(preset);
        }
    }
}
