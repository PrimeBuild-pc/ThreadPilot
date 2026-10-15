namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;

    internal static class TopologyFixtures
    {
        /// <summary>AMD with SMT: each entry of coresPerL3 is one L3 group (CCD), two logical CPUs per core.</summary>
        public static CpuTopologySnapshot Amd(
            int[] coresPerL3,
            ulong[]? cacheSizes,
            bool withL3Indexes = true,
            bool withCpuSetIds = true,
            int[]? reserved = null)
        {
            var processors = new List<ProcessorRef>();
            var l3Indexes = new Dictionary<ProcessorRef, int>();
            var coreIndexes = new Dictionary<ProcessorRef, int>();
            var core = 0;
            for (var l3 = 0; l3 < coresPerL3.Length; l3++)
            {
                for (var c = 0; c < coresPerL3[l3]; c++, core++)
                {
                    for (var thread = 0; thread < 2; thread++)
                    {
                        var index = (core * 2) + thread;
                        var processor = new ProcessorRef(0, (byte)index, index);
                        processors.Add(processor);
                        coreIndexes[processor] = core;
                        l3Indexes[processor] = l3;
                    }
                }
            }

            return CpuTopologySnapshot.Create(
                processors,
                cpuSetIds: withCpuSetIds ? processors.ToDictionary(p => p, p => (uint)(256 + p.GlobalIndex)) : null,
                coreIndexes: coreIndexes,
                lastLevelCacheIndexes: withL3Indexes ? l3Indexes : null,
                reservedLogicalProcessorIndexes: reserved,
                lastLevelCacheSizes: cacheSizes?.Select((size, index) => (size, index)).ToDictionary(x => x.index, x => x.size));
        }

        /// <summary>Intel hybrid: P-cores with HT first (EfficiencyClass 1), then single-thread E-cores (class 0).</summary>
        public static CpuTopologySnapshot IntelHybrid(int performanceCores, int efficiencyCores, bool withL3Indexes = true)
        {
            var processors = new List<ProcessorRef>();
            var classes = new Dictionary<ProcessorRef, byte>();
            var coreIndexes = new Dictionary<ProcessorRef, int>();
            var index = 0;
            for (var core = 0; core < performanceCores + efficiencyCores; core++)
            {
                var isPerformance = core < performanceCores;
                for (var thread = 0; thread < (isPerformance ? 2 : 1); thread++, index++)
                {
                    var processor = new ProcessorRef(0, (byte)index, index);
                    processors.Add(processor);
                    classes[processor] = isPerformance ? (byte)1 : (byte)0;
                    coreIndexes[processor] = core;
                }
            }

            return CpuTopologySnapshot.Create(
                processors,
                cpuSetIds: processors.ToDictionary(p => p, p => (uint)(256 + p.GlobalIndex)),
                efficiencyClasses: classes,
                coreIndexes: coreIndexes,
                lastLevelCacheIndexes: withL3Indexes ? processors.ToDictionary(p => p, _ => 0) : null);
        }
    }
}
