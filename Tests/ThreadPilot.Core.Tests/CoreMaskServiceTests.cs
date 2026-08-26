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
        public async Task InitializeAsync_AfterCpuChange_RebuildsBuiltInsAndGrowsUserMask()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true),
                CreateStoredMask("custom-mask", "My Game Mask", [true, false, true, false]));
            var service = CreateService(CreateTopology(logicalCoreCount: 8), masksFilePath);

            await service.InitializeAsync();

            var allCores = Assert.Single(service.AvailableMasks, mask => mask.Name == "All Cores");
            Assert.Equal("all-cores", allCores.Id);
            Assert.Equal(8, allCores.BoolMask.Count);
            Assert.All(allCores.BoolMask, Assert.True);
            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");

            // Grown, not redefined: the CPUs that appeared were not part of a selection made before
            // they existed, so they stay unselected and the mask keeps meaning what it meant.
            Assert.Equal(new[] { true, false, true, false, false, false, false, false }, customMask.BoolMask);
            Assert.Empty(service.MasksNeedingTopologyReview);
        }

        [Fact]
        public async Task InitializeAsync_OnASmallerCpu_ShrinksAUserMaskThatStillSelectsSomething()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true, true, true, true, true], isDefault: true),
                CreateStoredMask("custom-mask", "My Game Mask", [true, true, false, false, false, false, true, true]));
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();

            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");
            Assert.Equal(new[] { true, true, false, false }, customMask.BoolMask);
            Assert.Empty(service.MasksNeedingTopologyReview);
        }

        [Fact]
        public async Task InitializeAsync_LeavesAUserMaskAloneWhenShrinkingWouldEmptyIt()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true, true, true, true, true], isDefault: true),
                CreateStoredMask("custom-mask", "Big Cores Only", [false, false, false, false, true, true, true, true]));
            var service = CreateService(CreateTopology(logicalCoreCount: 4), masksFilePath);

            await service.InitializeAsync();

            // Every CPU it selected is gone. Truncating would leave a mask that cannot be applied,
            // and guessing which of the remaining CPUs the user meant is not ThreadPilot's call.
            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");
            Assert.Equal(new[] { false, false, false, false, true, true, true, true }, customMask.BoolMask);
            Assert.Equal(new[] { "Big Cores Only" }, service.MasksNeedingTopologyReview);
        }

        [Fact]
        public async Task InitializeAsync_LeavesAUserMaskAloneWhenAnAutomationRuleUsesIt()
        {
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true),
                CreateStoredMask("custom-mask", "My Game Mask", [true, false, true, false]));

            var associations = new Mock<IProcessPowerPlanAssociationService>(MockBehavior.Loose);
            associations
                .Setup(service => service.GetAssociationsAsync())
                .ReturnsAsync(new List<ProcessPowerPlanAssociation>
                {
                    new() { ExecutableName = "game.exe", CoreMaskId = "custom-mask" },
                });
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(provider => provider.GetService(typeof(IProcessPowerPlanAssociationService)))
                .Returns(associations.Object);

            var topologyService = new Mock<ICpuTopologyService>(MockBehavior.Strict);
            topologyService.SetupGet(service => service.CurrentTopology).Returns(CreateTopology(logicalCoreCount: 8));
            var service = new CoreMaskService(
                NullLogger<CoreMaskService>.Instance,
                topologyService.Object,
                serviceProvider.Object,
                masksFilePath: masksFilePath);

            await service.InitializeAsync();

            // Resizing it would silently change what that automation does.
            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");
            Assert.Equal(new[] { true, false, true, false }, customMask.BoolMask);
            Assert.Equal(new[] { "My Game Mask" }, service.MasksNeedingTopologyReview);
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

        [Fact]
        public async Task InitializeAsync_FlagsAUserMaskBuiltOnADifferentCpuWithTheSameCoreCount()
        {
            // Same number of threads, different chip: the bit count matches, so nothing is resized,
            // but "cores 2 and 3" no longer mean what they meant when the mask was drawn.
            var masksFilePath = CreateTempMasksPath();
            await WriteMasksAsync(
                masksFilePath,
                CreateStoredMask("all-cores", "All Cores", [true, true, true, true], isDefault: true),
                CreateStoredMask(
                    "custom-mask",
                    "My Game Mask",
                    [false, false, true, true],
                    cpuSelection: new CpuSelection
                    {
                        LogicalProcessors = [new ProcessorRef(0, 2, 2), new ProcessorRef(0, 3, 3)],
                        GlobalLogicalProcessorIndexes = [2, 3],
                        Metadata = new CpuSelectionMetadata
                        {
                            TopologySignature = new CpuTopologySignature { CpuBrand = "AMD Ryzen 7 5800X" },
                        },
                    }));
            var topology = CreateTopology(logicalCoreCount: 4);
            topology.CpuBrand = "AMD Ryzen 7 5800X3D";
            var service = CreateService(topology, masksFilePath);

            await service.InitializeAsync();

            var customMask = Assert.Single(service.AvailableMasks, mask => mask.Id == "custom-mask");
            Assert.Equal(new[] { false, false, true, true }, customMask.BoolMask);
            Assert.Equal(new[] { "My Game Mask" }, service.MasksNeedingTopologyReview);
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
            bool isDefault = false,
            CpuSelection? cpuSelection = null) =>
            new
            {
                id,
                name,
                description = $"{name} description",
                boolMask = boolMask.ToList(),
                profileSchemaVersion = CpuAffinityProfileSchemaVersions.Legacy,
                cpuSelection,
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
