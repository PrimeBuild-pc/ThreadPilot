namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class CpuTopologyServiceTopologyFixTests
    {
        [Fact]
        public void EfficiencyClass_HigherClassIsThePerformanceCore()
        {
            var topology = new CpuTopologyModel();
            for (var i = 0; i < 4; i++)
            {
                topology.LogicalCores.Add(new CpuCoreModel { LogicalCoreId = i, PhysicalCoreId = i });
            }

            CpuTopologyService.ApplyCoreTypeFromEfficiencyClass(
                topology,
                [(0, 0, (byte)1), (1, 1, (byte)1), (2, 2, (byte)0), (3, 3, (byte)0)]);

            Assert.Equal(
                [CpuCoreType.PerformanceCore, CpuCoreType.PerformanceCore, CpuCoreType.EfficiencyCore, CpuCoreType.EfficiencyCore],
                topology.LogicalCores.Select(core => core.CoreType));
        }

        [Fact]
        public async Task AmdCcds_ComeFromRealL3Groups_SixPlusSix()
        {
            var snapshot = TopologyFixtures.Amd([6, 6], cacheSizes: null);
            var service = new CpuTopologyService(NullLogger<CpuTopologyService>.Instance, new FixedTopologyProvider(snapshot));
            var topology = CreateLegacyTopology(24);

            Assert.True(await service.TryAssignCcdsFromL3GroupsAsync(topology));

            Assert.Equal(12, topology.LogicalCores.Count(core => core.CcdId == 0));
            Assert.Equal(12, topology.LogicalCores.Count(core => core.CcdId == 1));
        }

        [Fact]
        public async Task AmdCcds_SingleL3_HasNoCcdIds()
        {
            var snapshot = TopologyFixtures.Amd([8], cacheSizes: null);
            var service = new CpuTopologyService(NullLogger<CpuTopologyService>.Instance, new FixedTopologyProvider(snapshot));
            var topology = CreateLegacyTopology(16);

            Assert.True(await service.TryAssignCcdsFromL3GroupsAsync(topology));

            Assert.All(topology.LogicalCores, core => Assert.Null(core.CcdId));
        }

        [Fact]
        public async Task AmdCcds_WithoutL3Data_FallsBackToEstimate()
        {
            var snapshot = TopologyFixtures.Amd([8, 8], cacheSizes: null, withL3Indexes: false);
            var service = new CpuTopologyService(NullLogger<CpuTopologyService>.Instance, new FixedTopologyProvider(snapshot));

            Assert.False(await service.TryAssignCcdsFromL3GroupsAsync(CreateLegacyTopology(32)));
        }

        private static CpuTopologyModel CreateLegacyTopology(int logicalCount)
        {
            var topology = new CpuTopologyModel { CpuBrand = "AMD Ryzen" };
            for (var i = 0; i < logicalCount; i++)
            {
                topology.LogicalCores.Add(new CpuCoreModel { LogicalCoreId = i, PhysicalCoreId = i / 2 });
            }

            return topology;
        }

        private sealed class FixedTopologyProvider(CpuTopologySnapshot snapshot) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(snapshot);
        }
    }
}
