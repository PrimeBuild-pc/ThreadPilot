namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using System.Text.Json;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class CpuSelectionMigrationServiceTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        [Fact]
        public void MigrateFromLegacyAffinityMask_WithSingleGroupBelow64_SelectsExpectedProcessors()
        {
            var topology = CreateTopology(8);
            var service = new CpuSelectionMigrationService();

            var result = service.MigrateFromLegacyAffinityMask(0b0101, topology);

            Assert.Equal([0, 2], result.Selection.GlobalLogicalProcessorIndexes);
            Assert.True(result.Metadata.CreatedFromLegacyAffinityMask);
            Assert.False(result.Metadata.ReviewRequired);
            Assert.Equal(0b0101, service.BuildLegacyAffinityMaskIfRepresentable(result.Selection));
        }

        [Fact]
        public void MigrateFromLegacyAffinityMask_OnTopologyAbove64_DoesNotAliasCpu64ToCpu0()
        {
            var topology = CreateTopology(65);
            var service = new CpuSelectionMigrationService();

            var result = service.MigrateFromLegacyAffinityMask(1, topology);
            var cpu64Selection = CpuSelection.FromProcessors(
                [topology.LogicalProcessors.Single(processor => processor.GlobalIndex == 64)],
                topology);

            Assert.Equal([0], result.Selection.GlobalLogicalProcessorIndexes);
            Assert.DoesNotContain(result.Selection.LogicalProcessors, processor => processor.GlobalIndex == 64);
            Assert.Null(service.BuildLegacyAffinityMaskIfRepresentable(cpu64Selection));
        }

        [Fact]
        public void BuildLegacyAffinityMaskIfRepresentable_WithGroupOneCpuZero_ReturnsNull()
        {
            var group1Cpu0 = new ProcessorRef(1, 0, 64);
            var topology = CpuTopologySnapshot.Create([new ProcessorRef(0, 0, 0), group1Cpu0]);
            var selection = CpuSelection.FromProcessors([group1Cpu0], topology);
            var service = new CpuSelectionMigrationService();

            var legacyMask = service.BuildLegacyAffinityMaskIfRepresentable(selection);

            Assert.Null(legacyMask);
        }

        [Fact]
        public void MigrateFromLegacyCoreMask_WhenShorterThanTopology_SelectsPresentIndexesAndRequiresReview()
        {
            var topology = CreateTopology(4);
            var service = new CpuSelectionMigrationService();

            var result = service.MigrateFromLegacyCoreMask([true, false], topology);

            Assert.Equal([0], result.Selection.GlobalLogicalProcessorIndexes);
            Assert.True(result.Metadata.CreatedFromLegacyCoreMask);
            Assert.True(result.Metadata.ReviewRequired);
        }

        [Fact]
        public void MigrateFromLegacyCoreMask_WhenLongerThanTopology_IgnoresExtrasAndRequiresReview()
        {
            var topology = CreateTopology(2);
            var service = new CpuSelectionMigrationService();

            var result = service.MigrateFromLegacyCoreMask([false, true, true, true], topology);

            Assert.Equal([1], result.Selection.GlobalLogicalProcessorIndexes);
            Assert.True(result.Metadata.ReviewRequired);
        }

        [Fact]
        public void MigrateProcessProfile_WithExistingCpuSelection_DoesNotOverwriteFromLegacyMask()
        {
            var topology = CreateTopology(4);
            var existingSelection = CpuSelection.FromProcessors([topology.LogicalProcessors[1]], topology);
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.High,
                ProcessorAffinity = 0b0101,
                CpuSelection = existingSelection,
            };
            var service = new CpuSelectionMigrationService();

            var migrated = service.MigrateProcessProfile(profile, topology);

            Assert.Equal([1], migrated.CpuSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal(0b0101, migrated.ProcessorAffinity);
        }

        [Fact]
        public void ShouldRequireReview_TracksTopologySignatureChanges()
        {
            var topology = CreateTopology(4);
            var changedTopology = CreateTopology(6);
            var selection = CpuSelection.FromProcessors([topology.LogicalProcessors[0]], topology);
            var service = new CpuSelectionMigrationService();

            Assert.False(service.ShouldRequireReview(selection, topology.Signature, topology));
            Assert.True(service.ShouldRequireReview(selection, topology.Signature, changedTopology));
        }

        [Fact]
        public void PrepareProcessProfileForSave_WithSingleGroupBelow64_SavesLegacyMask()
        {
            var topology = CreateTopology(4);
            var selection = CpuSelection.FromProcessors([topology.LogicalProcessors[0], topology.LogicalProcessors[2]], topology);
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 0,
                CpuSelection = selection,
            };
            var service = new CpuSelectionMigrationService();

            var prepared = service.PrepareProcessProfileForSave(profile, topology);

            Assert.Equal(CpuAffinityProfileSchemaVersions.CpuSelection, prepared.ProfileSchemaVersion);
            Assert.Equal(0b0101, prepared.ProcessorAffinity);
            Assert.NotNull(prepared.CpuSelection);
        }

        [Fact]
        public void PrepareProcessProfileForSave_WithCpu64_DoesNotProduceLegacyMask()
        {
            var topology = CreateTopology(65);
            var selection = CpuSelection.FromProcessors([topology.LogicalProcessors[64]], topology);
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 0b11,
                CpuSelection = selection,
            };
            var service = new CpuSelectionMigrationService();

            var prepared = service.PrepareProcessProfileForSave(profile, topology);

            Assert.Equal(0b11, prepared.ProcessorAffinity);
            Assert.Null(service.BuildLegacyAffinityMaskIfRepresentable(prepared.CpuSelection!));
        }

        [Fact]
        public void LegacyProcessProfileWithoutSchemaVersion_DeserializesAsVersionOne()
        {
            const string json = """
                {
                  "processName": "game.exe",
                  "priority": 2,
                  "processorAffinity": 5
                }
                """;

            var profile = JsonSerializer.Deserialize<ProcessProfileSnapshot>(json, JsonOptions);

            Assert.NotNull(profile);
            Assert.Equal(CpuAffinityProfileSchemaVersions.Legacy, profile.ProfileSchemaVersion);
            Assert.Equal(5, profile.ProcessorAffinity);
        }

        [Fact]
        public void ProcessProfileWithCpuSelection_DeserializesAsVersionTwo()
        {
            var topology = CreateTopology(2);
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.High,
                ProcessorAffinity = 1,
                ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection,
                CpuSelection = CpuSelection.FromProcessors([topology.LogicalProcessors[0]], topology),
            };

            var json = JsonSerializer.Serialize(profile);
            var deserialized = JsonSerializer.Deserialize<ProcessProfileSnapshot>(json, JsonOptions);

            Assert.NotNull(deserialized);
            Assert.Equal(CpuAffinityProfileSchemaVersions.CpuSelection, deserialized.ProfileSchemaVersion);
            Assert.NotNull(deserialized.CpuSelection);
            Assert.Equal([0], deserialized.CpuSelection!.GlobalLogicalProcessorIndexes);
        }

        [Fact]
        public void LegacyCoreMaskWithoutSchemaVersion_DeserializesAsVersionOne()
        {
            const string json = """
                {
                  "id": "mask-1",
                  "name": "Legacy mask",
                  "description": "legacy",
                  "boolMask": [true, false, true],
                  "isDefault": false,
                  "isEnabled": true
                }
                """;

            var mask = JsonSerializer.Deserialize<CoreMask>(json, JsonOptions);

            Assert.NotNull(mask);
            Assert.Equal(CpuAffinityProfileSchemaVersions.Legacy, mask.ProfileSchemaVersion);
            Assert.Equal([true, false, true], mask.BoolMask.ToArray());
        }

        [Fact]
        public void CoreMaskWithCpuSelection_DeserializesAsVersionTwo()
        {
            var topology = CreateTopology(2);
            var mask = new CoreMask
            {
                Name = "V2 mask",
                ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection,
                CpuSelection = CpuSelection.FromProcessors([topology.LogicalProcessors[1]], topology),
            };
            mask.BoolMask.Add(false);
            mask.BoolMask.Add(true);

            var json = JsonSerializer.Serialize(mask);
            var deserialized = JsonSerializer.Deserialize<CoreMask>(json, JsonOptions);

            Assert.NotNull(deserialized);
            Assert.Equal(CpuAffinityProfileSchemaVersions.CpuSelection, deserialized.ProfileSchemaVersion);
            Assert.NotNull(deserialized.CpuSelection);
            Assert.Equal([1], deserialized.CpuSelection!.GlobalLogicalProcessorIndexes);
            Assert.Equal([false, true], deserialized.BoolMask.ToArray());
        }

        private static CpuTopologySnapshot CreateTopology(int processorCount)
        {
            var processors = Enumerable
                .Range(0, processorCount)
                .Select(index => new ProcessorRef((ushort)(index / 64), (byte)(index % 64), index))
                .ToList();

            return CpuTopologySnapshot.Create(
                processors,
                signature: new CpuTopologySignature
                {
                    CpuBrand = "Synthetic CPU",
                    LogicalProcessorCount = processorCount,
                    PhysicalCoreCount = processorCount,
                    ProcessorGroupCount = Math.Max(1, (processorCount + 63) / 64),
                    Source = "Test",
                });
        }
    }
}
