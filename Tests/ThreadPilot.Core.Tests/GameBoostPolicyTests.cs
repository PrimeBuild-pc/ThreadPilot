namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class GameBoostPolicyTests
    {
        private const ulong MB = 1024UL * 1024UL;

        [Theory]
        [InlineData(GameBoostMode.Automatic)]
        [InlineData(GameBoostMode.Isolation)]
        public void Ryzen7950X3D_GameOnVCacheCcd_BackgroundOnOther(GameBoostMode mode)
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7950X3D 16-Core Processor", mode);

            Assert.Equal(GameBoostTopologyKind.AmdAsymmetricCache, decision.Kind);
            Assert.Equal(Enumerable.Range(0, 16), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(16, 16), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(16, decision.GameSelection.CpuSetIds.Count);
        }

        [Fact]
        public void Ryzen7900X3D_SixPlusSix_UsesRealL3Groups()
        {
            var topology = TopologyFixtures.Amd([6, 6], [96 * MB, 32 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7900X3D", GameBoostMode.Automatic);

            Assert.Equal(Enumerable.Range(0, 12), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(12, 12), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void VCacheOnSecondCcd_FollowsTheCacheNotTheIndex()
        {
            var topology = TopologyFixtures.Amd([8, 8], [32 * MB, 96 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 9950X3D", GameBoostMode.Automatic);

            Assert.Equal(Enumerable.Range(16, 16), decision.GameSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void Ryzen9950X_Automatic_MovesOnlyBackground()
        {
            var topology = TopologyFixtures.Amd([8, 8], [32 * MB, 32 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 9950X", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.AmdSymmetricMultiL3, decision.Kind);
            Assert.Null(decision.GameSelection);
            Assert.Equal(Enumerable.Range(16, 16), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void Ryzen9950X_Isolation_GameOnCcd0()
        {
            var topology = TopologyFixtures.Amd([8, 8], [32 * MB, 32 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 9950X", GameBoostMode.Isolation);

            Assert.Equal(Enumerable.Range(0, 16), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(16, 16), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void Ryzen9950X3D2_EqualVCache_IsSymmetric()
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 96 * MB]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 9950X3D2", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.AmdSymmetricMultiL3, decision.Kind);
            Assert.Null(decision.GameSelection);
        }

        [Fact]
        public void Ryzen5800X3D_SingleL3_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([8], [96 * MB]);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "AMD Ryzen 7 5800X3D", GameBoostMode.Isolation));
        }

        [Fact]
        public void Zen2FourCoreCcx_TooSmallForTheGame_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([4, 4, 4, 4], [16 * MB, 16 * MB, 16 * MB, 16 * MB]);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "AMD Ryzen 9 3950X", GameBoostMode.Isolation));
        }

        [Fact]
        public void Amd_MissingCacheSizes_IsNeverTreatedAsVCache()
        {
            var topology = TopologyFixtures.Amd([8, 8], cacheSizes: null);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7950X3D", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.AmdSymmetricMultiL3, decision.Kind);
            Assert.Null(decision.GameSelection);
        }

        [Fact]
        public void Amd_MissingL3Index_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB], withL3Indexes: false);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7950X3D", GameBoostMode.Automatic));
        }

        [Fact]
        public void Intel14700K_GameOnPCores_BackgroundOnECores()
        {
            var topology = TopologyFixtures.IntelHybrid(performanceCores: 8, efficiencyCores: 12);

            var decision = GameBoostPolicy.Decide(topology, "Intel(R) Core(TM) i7-14700K", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.IntelHybrid, decision.Kind);
            Assert.Equal(Enumerable.Range(0, 16), decision.GameSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(Enumerable.Range(16, 12), decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void Intel_DoesNotNeedL3Indexes()
        {
            var topology = TopologyFixtures.IntelHybrid(performanceCores: 8, efficiencyCores: 16, withL3Indexes: false);

            var decision = GameBoostPolicy.Decide(topology, "Intel(R) Core(TM) i9-14900K", GameBoostMode.Automatic);

            Assert.Equal(GameBoostTopologyKind.IntelHybrid, decision.Kind);
        }

        [Fact]
        public void IntelLaptopTwoPCores_TooSmallForTheGame_DoesNothing()
        {
            var topology = TopologyFixtures.IntelHybrid(performanceCores: 2, efficiencyCores: 8);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "Intel(R) Core(TM) i5-1235U", GameBoostMode.Automatic));
        }

        [Fact]
        public void IntelWithoutEfficiencyClasses_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([8], [36 * MB]);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "Intel(R) Core(TM) i7-9700K", GameBoostMode.Isolation));
        }

        [Fact]
        public void FallbackTopologyWithoutCpuSetIds_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB], withCpuSetIds: false);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7950X3D", GameBoostMode.Automatic));
        }

        [Fact]
        public void UnknownVendor_DoesNothing()
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB]);

            Assert.Equal(GameBoostDecision.None, GameBoostPolicy.Decide(topology, null, GameBoostMode.Automatic));
        }

        [Fact]
        public void ReservedProcessors_AreNeverSelected()
        {
            var topology = TopologyFixtures.Amd([8, 8], [96 * MB, 32 * MB], reserved: [31]);

            var decision = GameBoostPolicy.Decide(topology, "AMD Ryzen 9 7950X3D", GameBoostMode.Automatic);

            Assert.DoesNotContain(31, decision.BackgroundSelection!.GlobalLogicalProcessorIndexes);
        }
    }
}
