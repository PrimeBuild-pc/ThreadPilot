namespace ThreadPilot.Core.Tests
{
    using System.Text.Json;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class CoreMaskServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        [Fact]
        public async Task InitializeAsync_WhenNoMaskFile_CreatesAllCoresAndNoCoreZero()
        {
            var masksFilePath = CreateTempMasksPath();
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();

            Assert.Contains(service.AvailableMasks, mask => mask.Name == "All Cores");
            var noCoreZero = Assert.Single(service.AvailableMasks, mask => mask.Name == "No Core 0");
            Assert.Equal(new[] { false, true, true, true }, noCoreZero.BoolMask);
        }

        [Fact]
        public async Task InitializeAsync_WithSmtTopology_CreatesAllNoSmt()
        {
            var masksFilePath = CreateTempMasksPath();
            var service = CreateService(CreateAmdSmtTopology(physicalCoreCount: 8, threadsPerCore: 2), masksFilePath);

            await service.InitializeAsync();

            var allNoSmt = Assert.Single(service.AvailableMasks, mask => mask.Name == "All no SMT");
            Assert.Equal(16, allNoSmt.BoolMask.Count);
            Assert.Equal(8, allNoSmt.SelectedCoreCount);
            Assert.Equal(
                Enumerable.Range(0, 16).Select(index => index % 2 == 0),
                allNoSmt.BoolMask);
        }

        [Fact]
        public async Task InitializeAsync_WhenExistingFileHasOnlyAllCores_BackfillsMissingBuiltIns()
        {
            var masksFilePath = CreateTempMasksPath();
            var existingId = "existing-all-cores";
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask(existingId, "All Cores", [true, true, true, true], isDefault: true));
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();

            Assert.Equal(existingId, Assert.Single(service.AvailableMasks, mask => mask.Name == "All Cores").Id);
            Assert.Contains(service.AvailableMasks, mask => mask.Name == "No Core 0");
        }

        [Fact]
        public async Task InitializeAsync_BackfillDoesNotDuplicateBuiltIns()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true),
                CreateStoredMask("no-core-zero", "No Core 0", [false, true, true, true]));
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();
            await service.InitializeAsync();

            Assert.Equal(1, service.AvailableMasks.Count(mask => mask.Name == "All Cores"));
            Assert.Equal(1, service.AvailableMasks.Count(mask => mask.Name == "No Core 0"));
        }

        [Fact]
        public async Task InitializeAsync_BackfillPreservesUserMasks()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true),
                CreateStoredMask("custom-mask", "My Game Mask", [false, true, true, false]));
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();

            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");
            Assert.Equal("My Game Mask", customMask.Name);
            Assert.Equal(new[] { false, true, true, false }, customMask.BoolMask);
            Assert.Contains(service.AvailableMasks, mask => mask.Name == "No Core 0");
        }

        [Fact]
        public async Task TopologyDetected_AfterInitialLoad_BackfillsSmtDefaults()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true));
            CpuTopologyModel? currentTopology = null;
            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Strict);
            topologyService.SetupGet(service => service.CurrentTopology).Returns(() => currentTopology);
            var service = new CoreMaskService(
                NullLogger<CoreMaskService>.Instance,
                topologyService.Object,
                Mock.Of<IServiceProvider>(),
                masksFilePath: masksFilePath);

            await service.InitializeAsync();
            Assert.DoesNotContain(service.AvailableMasks, mask => mask.Name == "All no SMT");

            currentTopology = CreateAmdSmtTopology(physicalCoreCount: 8, threadsPerCore: 2);
            topologyService.Raise(
                mock => mock.TopologyDetected += null,
                new CpuTopologyDetectedEventArgs(currentTopology, successful: true));

            Assert.True(SpinWait.SpinUntil(
                () => service.AvailableMasks.Any(mask => mask.Name == "All no SMT"),
                TimeSpan.FromSeconds(3)));
            Assert.Equal(1, service.AvailableMasks.Count(mask => mask.Name == "All no SMT"));
        }

        private static CoreMaskService CreateService(CpuTopologyModel topology, string masksFilePath)
        {
            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Strict);
            topologyService.SetupGet(service => service.CurrentTopology).Returns(topology);

            return new CoreMaskService(
                NullLogger<CoreMaskService>.Instance,
                topologyService.Object,
                Mock.Of<IServiceProvider>(),
                masksFilePath: masksFilePath);
        }

        private static CpuTopologyModel CreateTopology(int logicalCoreCount)
        {
            var topology = new CpuTopologyModel
            {
                CpuBrand = "Generic CPU",
                TopologyDetectionSuccessful = true,
            };

            for (var index = 0; index < logicalCoreCount; index++)
            {
                topology.LogicalCores.Add(new CpuCoreModel
                {
                    LogicalCoreId = index,
                    PhysicalCoreId = index,
                    SocketId = 0,
                    LogicalProcessorName = $"CPU{index}",
                });
            }

            return topology;
        }

        private static CpuTopologyModel CreateAmdSmtTopology(int physicalCoreCount, int threadsPerCore)
        {
            var topology = new CpuTopologyModel
            {
                CpuBrand = "AMD Ryzen",
                TopologyDetectionSuccessful = true,
            };

            for (var physicalCore = 0; physicalCore < physicalCoreCount; physicalCore++)
            {
                var firstLogicalCore = physicalCore * threadsPerCore;
                for (var thread = 0; thread < threadsPerCore; thread++)
                {
                    var logicalCore = firstLogicalCore + thread;
                    topology.LogicalCores.Add(new CpuCoreModel
                    {
                        LogicalCoreId = logicalCore,
                        PhysicalCoreId = physicalCore,
                        SocketId = 0,
                        CoreType = CpuCoreType.Zen4,
                        IsHyperThreaded = threadsPerCore > 1,
                        HyperThreadSibling = threadsPerCore > 1
                            ? firstLogicalCore + ((thread + 1) % threadsPerCore)
                            : null,
                        LogicalProcessorName = $"CPU{physicalCore}_T{thread}",
                    });
                }
            }

            return topology;
        }

        private static string CreateTempMasksPath()
        {
            var directory = Path.Combine(Path.GetTempPath(), "ThreadPilot-CoreMaskServiceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "core_masks.json");
        }

        private static object CreateStoredMask(
            string id,
            string name,
            IEnumerable<bool> boolMask,
            bool isDefault = false) =>
            new
            {
                id,
                name,
                description = $"{name} description",
                boolMask = boolMask.ToList(),
                profileSchemaVersion = CpuAffinityProfileSchemaVersions.Legacy,
                cpuSelection = (CpuSelection?)null,
                cpuSelectionMigration = (CpuSelectionMigrationMetadata?)null,
                isDefault,
                isEnabled = true,
                createdAt = DateTime.UtcNow.AddDays(-1),
                updatedAt = DateTime.UtcNow.AddDays(-1),
            };

        private static Task WriteMasksAsync(string masksFilePath, params object[] masks)
        {
            var json = JsonSerializer.Serialize(masks, JsonOptions);
            return File.WriteAllTextAsync(masksFilePath, json);
        }
    }
}
