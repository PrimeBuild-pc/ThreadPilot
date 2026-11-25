namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// Intel hybrid generations. The i9-12900K case is the CPU Set table read from a real 12900K
    /// (docs/validation/intel-hybrid-12900k.md); the others are synthetic and only show what the policy decides.
    /// </summary>
    public sealed class GameBoostIntelHybridTests
    {
        // (CPU Set ID, logical processor, CoreIndex, L3 index, EfficiencyClass) as GetSystemCpuSetInformation reported them.
        private static readonly (uint Id, int Lp, int Core, int L3, byte Class)[] CoreI9With12900KCpuSets =
        [
            (256, 0, 0, 0, 1), (257, 1, 0, 0, 1), (258, 2, 2, 0, 1), (259, 3, 2, 0, 1),
            (260, 4, 4, 0, 1), (261, 5, 4, 0, 1), (262, 6, 6, 0, 1), (263, 7, 6, 0, 1),
            (264, 8, 8, 0, 1), (265, 9, 8, 0, 1), (266, 10, 10, 0, 1), (267, 11, 10, 0, 1),
            (268, 12, 12, 0, 1), (269, 13, 12, 0, 1), (270, 14, 14, 0, 1), (271, 15, 14, 0, 1),
            (272, 16, 16, 0, 0), (273, 17, 17, 0, 0), (274, 18, 18, 0, 0), (275, 19, 19, 0, 0),
            (276, 20, 20, 0, 0), (277, 21, 21, 0, 0), (278, 22, 22, 0, 0), (279, 23, 23, 0, 0),
        ];

        [Theory]
        [InlineData(GameBoostMode.Automatic)]
        [InlineData(GameBoostMode.Isolation)]
        public void RealCoreI9With12900KCpuSets_GameOnTheSixteenPCoreThreads_BackgroundOnTheEightECores(GameBoostMode mode)
        {
            var decision = GameBoostPolicy.Decide(FromCpuSets(CoreI9With12900KCpuSets), "12th Gen Intel(R) Core(TM) i9-12900K", mode);

            Assert.Equal(GameBoostTopologyKind.IntelHybrid, decision.Kind);
            Assert.Equal(Enumerable.Range(0, 16), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(256, 16).Select(id => (uint)id), decision.GameSelection.CpuSetIds);
            Assert.Equal(Enumerable.Range(16, 8), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(272, 8).Select(id => (uint)id), decision.BackgroundSelection.CpuSetIds);
        }

        [Theory]
        [InlineData("13th Gen Intel(R) Core(TM) i9-13900K", 8, 16)]
        [InlineData("14th Gen Intel(R) Core(TM) i9-14900K", 8, 16)]
        [InlineData("14th Gen Intel(R) Core(TM) i7-14700K", 8, 12)]
        [InlineData("13th Gen Intel(R) Core(TM) i7-13700K", 8, 8)]
        [InlineData("12th Gen Intel(R) Core(TM) i5-12600K", 6, 4)]
        [InlineData("13th Gen Intel(R) Core(TM) i5-13600K", 6, 8)]
        public void DesktopHybrid_GameOnPThreads_BackgroundOnECores(string brand, int performanceCores, int efficiencyCores)
        {
            var decision = GameBoostPolicy.Decide(TopologyFixtures.IntelHybrid(performanceCores, efficiencyCores), brand, GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.IntelHybrid, decision.Kind);
            Assert.Equal(Enumerable.Range(0, performanceCores * 2), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(performanceCores * 2, efficiencyCores), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void I5_12400_SixPCoresNoECores_DoesNothing()
        {
            var topology = TopologyFixtures.IntelHybrid(performanceCores: 6, efficiencyCores: 0);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "12th Gen Intel(R) Core(TM) i5-12400", GameBoostMode.Isolation));
        }

        [Fact]
        public void CoreUltraWithoutSmt_ThreeEfficiencyClasses_LowPowerECoresAreTreatedAsECores()
        {
            // Synthetic Core Ultra layout: 6 P-cores (class 2), 8 E-cores (class 1), 2 LP E-cores (class 0), no SMT.
            var cpuSets = Enumerable.Range(0, 16)
                .Select(lp => ((uint)(256 + lp), lp, lp, lp < 14 ? 0 : 1, (byte)(lp < 6 ? 2 : lp < 14 ? 1 : 0)))
                .ToArray();

            var decision = GameBoostPolicy.Decide(FromCpuSets(cpuSets), "Intel(R) Core(TM) Ultra 7 155H", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.IntelHybrid, decision.Kind);
            Assert.Equal(Enumerable.Range(0, 6), decision.GameSelection!.GlobalLogicalProcessorIndexes);

            // Only the top class is "P": E-cores and the slower LP E-cores end up in the same background set.
            Assert.Equal(Enumerable.Range(6, 10), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void CoreUltra200S_EightPCoresWithoutSmt_GameGetsEightCpus()
        {
            // Arrow Lake desktop: 8 P-cores and 16 E-cores, no Hyper-Threading.
            var cpuSets = Enumerable.Range(0, 24)
                .Select(lp => ((uint)(256 + lp), lp, lp, 0, (byte)(lp < 8 ? 1 : 0)))
                .ToArray();

            var decision = GameBoostPolicy.Decide(FromCpuSets(cpuSets), "Intel(R) Core(TM) Ultra 9 285K", GameBoostMode.Automatic);

            Assert.Equal(Enumerable.Range(0, 8), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(8, 16), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void LunarLakeFourPCores_TooSmallForTheGame_DoesNothing()
        {
            var cpuSets = Enumerable.Range(0, 8)
                .Select(lp => ((uint)(256 + lp), lp, lp, lp < 4 ? 0 : 1, (byte)(lp < 4 ? 1 : 0)))
                .ToArray();

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(FromCpuSets(cpuSets), "Intel(R) Core(TM) Ultra 7 258V", GameBoostMode.Automatic));
        }

        private static CpuTopologySnapshot FromCpuSets((uint Id, int Lp, int Core, int L3, byte Class)[] cpuSets)
        {
            var processors = cpuSets.Select(set => new ProcessorRef(0, (byte)set.Lp, set.Lp)).ToList();
            return CpuTopologySnapshot.Create(
                processors,
                cpuSetIds: cpuSets.ToDictionary(set => processors[set.Lp], set => set.Id),
                efficiencyClasses: cpuSets.ToDictionary(set => processors[set.Lp], set => set.Class),
                coreIndexes: cpuSets.ToDictionary(set => processors[set.Lp], set => set.Core),
                lastLevelCacheIndexes: cpuSets.ToDictionary(set => processors[set.Lp], set => set.L3));
        }
    }
}
