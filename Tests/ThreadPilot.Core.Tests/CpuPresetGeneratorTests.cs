namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class CpuPresetGeneratorTests
    {
        [Fact]
        public void Generate_WithFourCoreEightThreadSmt_GeneratesSafeBasePresets()
        {
            var topology = CreateSmtTopology(physicalCoreCount: 4, threadsPerCore: 2);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            AssertPresetIdsContain(
                presets,
                "all-cores",
                "all-physical-cores",
                "all-except-cpu0",
                "best-gaming",
                "safe-compatibility");
            Assert.Equal(4, GetPreset(presets, "all-physical-cores").Selection.LogicalProcessors.Count);
            AssertBestGamingSource(GetPreset(presets, "best-gaming"), "all-physical-cores");
            AssertValidPresets(presets, topology);
            AssertStableIdsAndOrder(generator, topology, presets);
        }

        [Fact]
        public void Generate_WithEightCoreEightThreadSmtOff_KeepsBestGamingValid()
        {
            var topology = CreateSmtTopology(physicalCoreCount: 8, threadsPerCore: 1);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            var allCores = GetPreset(presets, "all-cores");
            var physical = presets.SingleOrDefault(preset => preset.PresetId == "all-physical-cores");
            if (physical != null)
            {
                AssertSameSelection(allCores, physical);
                Assert.NotEqual(allCores.Reason, physical.Reason);
            }

            var bestGaming = GetPreset(presets, "best-gaming");
            Assert.NotEmpty(bestGaming.Selection.LogicalProcessors);
            AssertBestGamingSource(bestGaming, "all-physical-cores");
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WithHybridPAndECoresWithHt_GeneratesHybridPresets()
        {
            var topology = CreateHybridTopology(pCoreCount: 4, eCoreCount: 4, pCoreThreads: 2);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            AssertPresetIdsContain(presets, "p-cores-only", "p-cores-no-smt", "e-cores-only");
            Assert.Equal(8, GetPreset(presets, "p-cores-only").Selection.LogicalProcessors.Count);
            Assert.Equal(4, GetPreset(presets, "p-cores-no-smt").Selection.LogicalProcessors.Count);
            Assert.Equal(4, GetPreset(presets, "e-cores-only").Selection.LogicalProcessors.Count);
            AssertBestGamingSource(GetPreset(presets, "best-gaming"), "p-cores-no-smt");
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WithHybridPAndECoresWithoutHt_HandlesNoSmtDuplicate()
        {
            var topology = CreateHybridTopology(pCoreCount: 4, eCoreCount: 4, pCoreThreads: 1);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            var pCoresOnly = GetPreset(presets, "p-cores-only");
            var pCoresNoSmt = presets.SingleOrDefault(preset => preset.PresetId == "p-cores-no-smt");
            if (pCoresNoSmt != null)
            {
                AssertSameSelection(pCoresOnly, pCoresNoSmt);
                Assert.NotEqual(pCoresOnly.Reason, pCoresNoSmt.Reason);
            }

            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WithRyzenDualCcdSixPlusSix_GeneratesL3PhysicalPresets()
        {
            var topology = CreateDualCcdTopology(physicalCoresPerCcd: 6);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            AssertPresetIdsContain(presets, "l3-group-0-physical", "l3-group-1-physical");
            Assert.Equal(6, GetPreset(presets, "l3-group-0-physical").Selection.LogicalProcessors.Count);
            Assert.Equal(6, GetPreset(presets, "l3-group-1-physical").Selection.LogicalProcessors.Count);
            AssertBestGamingSource(GetPreset(presets, "best-gaming"), "l3-group-0-physical");
            Assert.Contains("L3", GetPreset(presets, "l3-group-0-physical").Reason, StringComparison.OrdinalIgnoreCase);
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WithRyzenDualCcdEightPlusEight_GeneratesEightPhysicalPerL3Preset()
        {
            var topology = CreateDualCcdTopology(physicalCoresPerCcd: 8);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            Assert.Equal(8, GetPreset(presets, "l3-group-0-physical").Selection.LogicalProcessors.Count);
            Assert.Equal(8, GetPreset(presets, "l3-group-1-physical").Selection.LogicalProcessors.Count);
            AssertBestGamingSource(GetPreset(presets, "best-gaming"), "l3-group-0-physical");
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WithMoreThan64LogicalProcessors_UsesCpuSelectionWithoutCpu64Alias()
        {
            var topology = CreateSmtTopology(physicalCoreCount: 40, threadsPerCore: 2);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            var allCores = GetPreset(presets, "all-cores");
            Assert.Contains(allCores.Selection.LogicalProcessors, processor => processor.GlobalIndex == 64);
            Assert.NotEqual(
                allCores.Selection.LogicalProcessors.Single(processor => processor.GlobalIndex == 64),
                allCores.Selection.LogicalProcessors.Single(processor => processor.GlobalIndex == 0));
            Assert.Null(CpuSelection.ToLegacyAffinityMaskOrNull(allCores.Selection));
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_WhenGeneratedPresetWasDeleted_DoesNotRegenerateIt()
        {
            var topology = CreateSmtTopology(physicalCoreCount: 4, threadsPerCore: 2);
            var generator = new CpuPresetGenerator();
            var options = new CpuPresetGenerationOptions
            {
                DeletedGeneratedPresetIds = new HashSet<string>(StringComparer.Ordinal)
                {
                    "best-gaming",
                },
            };

            var presets = generator.Generate(topology, options);

            Assert.DoesNotContain(presets, preset => preset.PresetId == "best-gaming");
            AssertPresetIdsContain(presets, "all-cores", "safe-compatibility");
        }

        [Fact]
        public void Generate_WithoutCoreIndex_SkipsPhysicalPresets()
        {
            var topology = CpuTopologySnapshot.Create(
                CreateProcessorRefs(8),
                signature: CreateSignature(logicalProcessorCount: 8, physicalCoreCount: 0));
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            Assert.DoesNotContain(presets, preset => preset.PresetId == "all-physical-cores");
            Assert.DoesNotContain(presets, preset => preset.PresetId == "p-cores-no-smt");
            Assert.DoesNotContain(presets, preset => preset.PresetId.StartsWith("l3-group-", StringComparison.Ordinal));
            AssertPresetIdsContain(presets, "all-cores", "all-except-cpu0", "best-gaming", "safe-compatibility");
            AssertBestGamingSource(GetPreset(presets, "best-gaming"), "all-except-cpu0");
            AssertValidPresets(presets, topology);
        }

        [Fact]
        public void Generate_DoesNotReturnEmptySelections()
        {
            var topology = CreateHybridTopology(pCoreCount: 2, eCoreCount: 2, pCoreThreads: 2);
            var generator = new CpuPresetGenerator();

            var presets = generator.Generate(topology);

            Assert.All(presets, preset => Assert.NotEmpty(preset.Selection.LogicalProcessors));
        }

        private static CpuPreset GetPreset(IReadOnlyList<CpuPreset> presets, string presetId) =>
            presets.Single(preset => preset.PresetId == presetId);

        private static void AssertPresetIdsContain(IReadOnlyList<CpuPreset> presets, params string[] presetIds)
        {
            foreach (var presetId in presetIds)
            {
                Assert.Contains(presets, preset => preset.PresetId == presetId);
            }
        }

        private static void AssertSameSelection(CpuPreset expected, CpuPreset actual) =>
            Assert.Equal(
                expected.Selection.GlobalLogicalProcessorIndexes,
                actual.Selection.GlobalLogicalProcessorIndexes);

        private static void AssertBestGamingSource(CpuPreset bestGaming, string expectedSourcePresetId)
        {
            Assert.Equal("best-gaming", bestGaming.PresetId);
            Assert.Equal(expectedSourcePresetId, bestGaming.SourcePresetId);
            Assert.NotEqual(bestGaming.SourcePresetId, bestGaming.Reason);
            Assert.False(string.IsNullOrWhiteSpace(bestGaming.Reason));
        }

        private static void AssertStableIdsAndOrder(
            CpuPresetGenerator generator,
            CpuTopologySnapshot topology,
            IReadOnlyList<CpuPreset> firstRun)
        {
            var secondRun = generator.Generate(topology);
            Assert.Equal(
                firstRun.Select(preset => preset.PresetId),
                secondRun.Select(preset => preset.PresetId));
        }

        private static void AssertValidPresets(IReadOnlyList<CpuPreset> presets, CpuTopologySnapshot topology)
        {
            Assert.NotEmpty(presets);
            Assert.Equal(presets.Count, presets.Select(preset => preset.PresetId).Distinct(StringComparer.Ordinal).Count());

            var topologyProcessors = topology.LogicalProcessors.ToHashSet();
            foreach (var preset in presets)
            {
                Assert.False(string.IsNullOrWhiteSpace(preset.PresetId));
                Assert.False(string.IsNullOrWhiteSpace(preset.Name));
                Assert.False(string.IsNullOrWhiteSpace(preset.Description));
                Assert.False(string.IsNullOrWhiteSpace(preset.Reason));
                Assert.True(preset.IsGenerated);
                Assert.True(preset.IsUserEditable);
                Assert.NotEmpty(preset.Selection.LogicalProcessors);
                Assert.Equal(topology.Signature, preset.GeneratedByTopologySignature);
                Assert.Equal(topology.Signature, preset.Selection.Metadata.TopologySignature);
                Assert.All(preset.Selection.LogicalProcessors, processor => Assert.Contains(processor, topologyProcessors));
            }
        }

        private static CpuTopologySnapshot CreateSmtTopology(int physicalCoreCount, int threadsPerCore)
        {
            var processors = new List<ProcessorRef>();
            var coreIndexes = new Dictionary<ProcessorRef, int>();
            var siblings = new Dictionary<ProcessorRef, IReadOnlyList<int>>();

            for (var core = 0; core < physicalCoreCount; core++)
            {
                var coreProcessors = new List<ProcessorRef>();
                for (var thread = 0; thread < threadsPerCore; thread++)
                {
                    var globalIndex = (core * threadsPerCore) + thread;
                    var processor = CreateProcessorRef(globalIndex);
                    processors.Add(processor);
                    coreIndexes[processor] = core;
                    coreProcessors.Add(processor);
                }

                foreach (var processor in coreProcessors)
                {
                    siblings[processor] = coreProcessors
                        .Where(sibling => sibling != processor)
                        .Select(sibling => sibling.GlobalIndex)
                        .ToList();
                }
            }

            return CpuTopologySnapshot.Create(
                processors,
                signature: CreateSignature(processors.Count, physicalCoreCount),
                coreIndexes: coreIndexes,
                smtSiblingGlobalIndexes: siblings);
        }

        private static CpuTopologySnapshot CreateHybridTopology(int pCoreCount, int eCoreCount, int pCoreThreads)
        {
            var processors = new List<ProcessorRef>();
            var coreIndexes = new Dictionary<ProcessorRef, int>();
            var siblings = new Dictionary<ProcessorRef, IReadOnlyList<int>>();
            var efficiency = new Dictionary<ProcessorRef, byte>();

            for (var core = 0; core < pCoreCount; core++)
            {
                var coreProcessors = new List<ProcessorRef>();
                for (var thread = 0; thread < pCoreThreads; thread++)
                {
                    var processor = CreateProcessorRef(processors.Count);
                    processors.Add(processor);
                    coreIndexes[processor] = core;
                    efficiency[processor] = 2;
                    coreProcessors.Add(processor);
                }

                foreach (var processor in coreProcessors)
                {
                    siblings[processor] = coreProcessors
                        .Where(sibling => sibling != processor)
                        .Select(sibling => sibling.GlobalIndex)
                        .ToList();
                }
            }

            for (var core = 0; core < eCoreCount; core++)
            {
                var processor = CreateProcessorRef(processors.Count);
                processors.Add(processor);
                coreIndexes[processor] = pCoreCount + core;
                efficiency[processor] = 0;
                siblings[processor] = [];
            }

            return CpuTopologySnapshot.Create(
                processors,
                efficiencyClasses: efficiency,
                signature: CreateSignature(processors.Count, pCoreCount + eCoreCount),
                coreIndexes: coreIndexes,
                smtSiblingGlobalIndexes: siblings);
        }

        private static CpuTopologySnapshot CreateDualCcdTopology(int physicalCoresPerCcd)
        {
            var processorCount = physicalCoresPerCcd * 2;
            var processors = CreateProcessorRefs(processorCount).ToList();
            var coreIndexes = processors.ToDictionary(processor => processor, processor => processor.GlobalIndex);
            var siblings = processors.ToDictionary(
                processor => processor,
                _ => (IReadOnlyList<int>)[]);
            var l3Indexes = processors.ToDictionary(
                processor => processor,
                processor => processor.GlobalIndex < physicalCoresPerCcd ? 0 : 1);

            return CpuTopologySnapshot.Create(
                processors,
                signature: CreateSignature(
                    logicalProcessorCount: processorCount,
                    physicalCoreCount: processorCount,
                    lastLevelCacheGroupCount: 2),
                coreIndexes: coreIndexes,
                lastLevelCacheIndexes: l3Indexes,
                smtSiblingGlobalIndexes: siblings);
        }

        private static IEnumerable<ProcessorRef> CreateProcessorRefs(int count) =>
            Enumerable.Range(0, count).Select(CreateProcessorRef);

        private static ProcessorRef CreateProcessorRef(int globalIndex) =>
            new((ushort)(globalIndex / 64), (byte)(globalIndex % 64), globalIndex);

        private static CpuTopologySignature CreateSignature(
            int logicalProcessorCount,
            int physicalCoreCount,
            int lastLevelCacheGroupCount = 0) =>
            new()
            {
                CpuBrand = "Synthetic CPU",
                LogicalProcessorCount = logicalProcessorCount,
                PhysicalCoreCount = physicalCoreCount,
                ProcessorGroupCount = Math.Max(1, (logicalProcessorCount + 63) / 64),
                LastLevelCacheGroupCount = lastLevelCacheGroupCount,
                Source = "Test",
            };
    }
}
