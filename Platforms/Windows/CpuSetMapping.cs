namespace ThreadPilot.Platforms.Windows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreadPilot.Models;

    internal sealed class CpuSetMapping
    {
        private readonly IReadOnlyDictionary<ProcessorRef, uint> cpuSetIdsByProcessor;
        private readonly IReadOnlyDictionary<uint, ProcessorRef> processorsByCpuSetId;

        private CpuSetMapping(
            IReadOnlyDictionary<ProcessorRef, uint> cpuSetIdsByProcessor,
            IReadOnlyDictionary<uint, ProcessorRef> processorsByCpuSetId)
        {
            this.cpuSetIdsByProcessor = cpuSetIdsByProcessor;
            this.processorsByCpuSetId = processorsByCpuSetId;
        }

        public static CpuSetMapping Empty { get; } = new(
            new Dictionary<ProcessorRef, uint>(),
            new Dictionary<uint, ProcessorRef>());

        public bool IsEmpty => this.cpuSetIdsByProcessor.Count == 0;

        public static CpuSetMapping Create(IReadOnlyDictionary<ProcessorRef, uint> cpuSetIdsByProcessor)
        {
            ArgumentNullException.ThrowIfNull(cpuSetIdsByProcessor);

            var forwardMap = cpuSetIdsByProcessor
                .OrderBy(kvp => kvp.Key.GlobalIndex)
                .ThenBy(kvp => kvp.Key.Group)
                .ThenBy(kvp => kvp.Key.LogicalProcessorNumber)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var inverseMap = forwardMap
                .GroupBy(kvp => kvp.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(kvp => kvp.Key)
                        .OrderBy(processor => processor.GlobalIndex)
                        .ThenBy(processor => processor.Group)
                        .ThenBy(processor => processor.LogicalProcessorNumber)
                        .First());

            return new CpuSetMapping(forwardMap, inverseMap);
        }

        public static ProcessorRef CreateProcessorRef(ushort group, byte logicalProcessorNumber)
        {
            return new ProcessorRef(group, logicalProcessorNumber, (group * 64) + logicalProcessorNumber);
        }

        public bool TryGetCpuSetId(ProcessorRef processor, out uint cpuSetId)
        {
            return this.cpuSetIdsByProcessor.TryGetValue(processor, out cpuSetId);
        }

        public bool TryGetProcessorRef(uint cpuSetId, out ProcessorRef processor)
        {
            return this.processorsByCpuSetId.TryGetValue(cpuSetId, out processor);
        }

        public IReadOnlyList<uint> ResolveCpuSetIds(CpuSelection selection)
        {
            ArgumentNullException.ThrowIfNull(selection);

            if (selection.CpuSetIds.Count > 0)
            {
                return selection.CpuSetIds
                    .Distinct()
                    .OrderBy(cpuSetId => cpuSetId)
                    .ToList();
            }

            return selection.LogicalProcessors
                .Select(processor => this.TryGetCpuSetId(processor, out var cpuSetId) ? (uint?)cpuSetId : null)
                .Where(cpuSetId => cpuSetId.HasValue)
                .Select(cpuSetId => cpuSetId!.Value)
                .Distinct()
                .OrderBy(cpuSetId => cpuSetId)
                .ToList();
        }

        public IReadOnlyList<uint> ResolveLegacyAffinityMask(long affinityMask, int logicalProcessorCount)
        {
            var unsignedMask = unchecked((ulong)affinityMask);
            var maxLegacyBits = Math.Min(Math.Max(logicalProcessorCount, 0), 64);
            var cpuSetIds = new List<uint>();

            for (var bit = 0; bit < maxLegacyBits; bit++)
            {
                if ((unsignedMask & (1UL << bit)) == 0)
                {
                    continue;
                }

                var processor = CreateProcessorRef(0, (byte)bit);
                if (this.TryGetCpuSetId(processor, out var cpuSetId))
                {
                    cpuSetIds.Add(cpuSetId);
                }
            }

            return cpuSetIds
                .Distinct()
                .OrderBy(cpuSetId => cpuSetId)
                .ToList();
        }
    }
}
