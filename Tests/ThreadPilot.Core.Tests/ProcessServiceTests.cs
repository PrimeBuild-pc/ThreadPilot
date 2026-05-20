/*
 * ThreadPilot - process service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text.Json;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for deterministic behavior in <see cref="ProcessService"/>.
    /// </summary>
    public sealed class ProcessServiceTests
    {
        [Fact]
        public async Task SaveProcessProfile_WritesExpectedJson()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var process = new ProcessModel
            {
                Name = "game.exe",
                Priority = ProcessPriorityClass.High,
                ProcessorAffinity = 3,
            };

            try
            {
                var service = CreateService(profilesDirectory);

                var result = await service.SaveProcessProfile(profileName, process);

                Assert.True(result);

                var filePath = Path.Combine(profilesDirectory, $"{profileName}.json");
                Assert.True(File.Exists(filePath));

                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(filePath));
                Assert.Equal("game.exe", document.RootElement.GetProperty("ProcessName").GetString());
                Assert.Equal((int)ProcessPriorityClass.High, document.RootElement.GetProperty("Priority").GetInt32());
                Assert.Equal(3, document.RootElement.GetProperty("ProcessorAffinity").GetInt64());
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task SaveProcessProfile_WithTopologyProvider_WritesCpuSelectionSchema()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var topology = CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 1, 1),
                new ProcessorRef(0, 2, 2),
            ]);
            var process = new ProcessModel
            {
                Name = "game.exe",
                Priority = ProcessPriorityClass.High,
                ProcessorAffinity = 0b101,
            };

            try
            {
                var service = CreateService(profilesDirectory, new FakeCpuTopologyProvider(topology));

                var result = await service.SaveProcessProfile(profileName, process);

                Assert.True(result);

                var filePath = Path.Combine(profilesDirectory, $"{profileName}.json");
                var profile = JsonSerializer.Deserialize<ProcessProfileSnapshot>(
                    await File.ReadAllTextAsync(filePath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                Assert.NotNull(profile);
                Assert.Equal(CpuAffinityProfileSchemaVersions.CpuSelection, profile.ProfileSchemaVersion);
                Assert.Equal(0b101, profile.ProcessorAffinity);
                Assert.NotNull(profile.CpuSelection);
                Assert.Equal([0, 2], profile.CpuSelection!.GlobalLogicalProcessorIndexes);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task LoadProcessProfile_ReturnsFalse_WhenFileIsMissing()
        {
            var profilesDirectory = CreateTemporaryDirectory();

            try
            {
                var service = CreateService(profilesDirectory);

                var result = await service.LoadProcessProfile("missing-profile", new ProcessModel());

                Assert.False(result);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task LoadProcessProfile_WithCpuSelectionApplyFailure_ReturnsFalse()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var topology = CreateTopology();
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 1,
                ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection,
                CpuSelection = CpuSelection.FromProcessors([topology.LogicalProcessors[0]], topology),
            };
            var service = CreateTestableService(
                profilesDirectory,
                new FakeCpuTopologyProvider(topology),
                cpuSelectionResult: AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.NativeApplyFailed,
                    "Affinity was not applied.",
                    "simulated apply failure"));

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.False(result);
                Assert.Equal(1, service.CpuSelectionApplyCalls);
                Assert.Equal(0, service.LegacyAffinityApplyCalls);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task LoadProcessProfile_WithCpuSelectionApplySuccess_ReturnsTrue()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var topology = CreateTopology();
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 1,
                ProfileSchemaVersion = CpuAffinityProfileSchemaVersions.CpuSelection,
                CpuSelection = CpuSelection.FromProcessors([topology.LogicalProcessors[0]], topology),
            };
            var service = CreateTestableService(
                profilesDirectory,
                new FakeCpuTopologyProvider(topology),
                cpuSelectionResult: AffinityApplyResult.SucceededWithCpuSets("simulated apply success"));

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.True(result);
                Assert.Equal(1, service.CpuSelectionApplyCalls);
                Assert.Equal(0, service.LegacyAffinityApplyCalls);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task LoadProcessProfile_WithoutTopologyProvider_UsesLegacyAffinityPath()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 0b11,
            };
            var service = CreateTestableService(profilesDirectory, topologyProvider: null);

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.True(result);
                Assert.Equal(1, service.LegacyAffinityApplyCalls);
                Assert.Equal(0b11, service.LastLegacyAffinityMask);
                Assert.Equal(0, service.CpuSelectionApplyCalls);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public void IsPassiveProcessAccessException_ReturnsTrue_ForModuleEnumerationFailure()
        {
            var exception = new Win32Exception(299, "Unable to enumerate the process modules.");

            var result = ProcessService.IsPassiveProcessAccessException(exception);

            Assert.True(result);
        }

        [Fact]
        public void IsPassiveProcessAccessException_ReturnsTrue_ForUnauthorizedAccess()
        {
            var exception = new UnauthorizedAccessException("Access denied.");

            var result = ProcessService.IsPassiveProcessAccessException(exception);

            Assert.True(result);
        }

        [Theory]
        [InlineData("Unable to access modules for this process.")]
        [InlineData("ReadProcessMemory failed for protected process.")]
        public void IsPassiveProcessAccessException_ReturnsTrue_ForKnownPassiveMessages(string message)
        {
            var exception = new InvalidOperationException(message);

            var result = ProcessService.IsPassiveProcessAccessException(exception);

            Assert.True(result);
        }

        [Fact]
        public void IsPassiveProcessAccessException_ReturnsFalse_ForUnrelatedException()
        {
            var exception = new InvalidOperationException("Unexpected parse failure.");

            var result = ProcessService.IsPassiveProcessAccessException(exception);

            Assert.False(result);
        }

        [Fact]
        public void TrackPriorityChange_PreservesOriginalPriority()
        {
            var service = CreateService(CreateTemporaryDirectory());

            try
            {
                service.TrackPriorityChange(42, ProcessPriorityClass.Normal);
                service.TrackPriorityChange(42, ProcessPriorityClass.High);

                var trackedPriorities = GetPrivateDictionary<int, ProcessPriorityClass>(service, "originalPriorities");
                Assert.True(trackedPriorities.TryGetValue(42, out var priority));
                Assert.Equal(ProcessPriorityClass.Normal, priority);
            }
            finally
            {
                DeleteDirectory(GetProfilesDirectory(service));
            }
        }

        [Fact]
        public void UntrackProcess_ClearsTrackedState()
        {
            var service = CreateService(CreateTemporaryDirectory());

            try
            {
                service.TrackAppliedMask(77, "mask-a");
                service.TrackPriorityChange(77, ProcessPriorityClass.BelowNormal);

                service.UntrackProcess(77);

                var trackedMasks = GetPrivateDictionary<int, string>(service, "appliedMasks");
                var trackedPriorities = GetPrivateDictionary<int, ProcessPriorityClass>(service, "originalPriorities");
                Assert.False(trackedMasks.ContainsKey(77));
                Assert.False(trackedPriorities.ContainsKey(77));
            }
            finally
            {
                DeleteDirectory(GetProfilesDirectory(service));
            }
        }

        private static ProcessService CreateService(
            string profilesDirectory,
            ICpuTopologyProvider? topologyProvider = null) =>
            new(null, null, () => profilesDirectory, cpuTopologyProvider: topologyProvider);

        private static TestableProcessService CreateTestableService(
            string profilesDirectory,
            ICpuTopologyProvider? topologyProvider,
            AffinityApplyResult? cpuSelectionResult = null) =>
            new(profilesDirectory, topologyProvider, cpuSelectionResult);

        private static ProcessModel CreateProcess() =>
            new()
            {
                ProcessId = 1234,
                Name = "game.exe",
                Priority = ProcessPriorityClass.Normal,
                ProcessorAffinity = 0,
            };

        private static CpuTopologySnapshot CreateTopology() =>
            CpuTopologySnapshot.Create(
            [
                new ProcessorRef(0, 0, 0),
                new ProcessorRef(0, 1, 1),
            ]);

        private static Task WriteProfileAsync(
            string profilesDirectory,
            string profileName,
            ProcessProfileSnapshot profile)
        {
            var filePath = Path.Combine(profilesDirectory, $"{profileName}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            return File.WriteAllTextAsync(filePath, json);
        }

        private static string CreateTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"threadpilot-process-service-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static string GetProfilesDirectory(ProcessService service)
        {
            var property = typeof(ProcessService).GetProperty("ProfilesDirectory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (string)(property?.GetValue(service) ?? throw new InvalidOperationException("ProfilesDirectory property not found."));
        }

        private static ConcurrentDictionary<TKey, TValue> GetPrivateDictionary<TKey, TValue>(ProcessService service, string fieldName)
            where TKey : notnull
        {
            var field = typeof(ProcessService).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (ConcurrentDictionary<TKey, TValue>)(field?.GetValue(service) ?? throw new InvalidOperationException($"Field '{fieldName}' not found."));
        }

        private sealed class FakeCpuTopologyProvider(CpuTopologySnapshot snapshot) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult(snapshot);
        }

        private sealed class TestableProcessService : ProcessService
        {
            private readonly AffinityApplyResult cpuSelectionResult;

            public TestableProcessService(
                string profilesDirectory,
                ICpuTopologyProvider? topologyProvider,
                AffinityApplyResult? cpuSelectionResult)
                : base(null, null, () => profilesDirectory, cpuTopologyProvider: topologyProvider)
            {
                this.cpuSelectionResult = cpuSelectionResult ?? AffinityApplyResult.Succeeded(0, 0);
            }

            public int CpuSelectionApplyCalls { get; private set; }

            public int LegacyAffinityApplyCalls { get; private set; }

            public long LastLegacyAffinityMask { get; private set; }

            public override Task SetProcessPriority(ProcessModel process, ProcessPriorityClass priority)
            {
                process.Priority = priority;
                return Task.CompletedTask;
            }

            public override Task<AffinityApplyResult> SetProcessorAffinity(ProcessModel process, CpuSelection selection)
            {
                this.CpuSelectionApplyCalls++;
                return Task.FromResult(this.cpuSelectionResult);
            }

            public override Task SetProcessorAffinity(ProcessModel process, long affinityMask)
            {
                this.LegacyAffinityApplyCalls++;
                this.LastLegacyAffinityMask = affinityMask;
                process.ProcessorAffinity = affinityMask;
                return Task.CompletedTask;
            }
        }
    }
}
