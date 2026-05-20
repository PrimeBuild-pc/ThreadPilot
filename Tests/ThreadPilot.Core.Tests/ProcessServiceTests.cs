/*
 * ThreadPilot - process service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text.Json;
    using Microsoft.Extensions.Logging;
    using Moq;
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
            var profileApplier = new FakeLoadProcessProfileApplier(
                cpuSelectionResult: AffinityApplyResult.Failed(
                    AffinityApplyErrorCodes.NativeApplyFailed,
                    "Affinity was not applied.",
                    "simulated apply failure"));
            var service = CreateService(
                profilesDirectory,
                new FakeCpuTopologyProvider(topology),
                profileApplier);

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.False(result);
                Assert.Equal(1, profileApplier.CpuSelectionApplyCalls);
                Assert.Equal(0, profileApplier.LegacyAffinityApplyCalls);
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
            var profileApplier = new FakeLoadProcessProfileApplier(
                cpuSelectionResult: AffinityApplyResult.SucceededWithCpuSets("simulated apply success"));
            var service = CreateService(
                profilesDirectory,
                new FakeCpuTopologyProvider(topology),
                profileApplier);

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.True(result);
                Assert.Equal(1, profileApplier.CpuSelectionApplyCalls);
                Assert.Equal(0, profileApplier.LegacyAffinityApplyCalls);
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
            var profileApplier = new FakeLoadProcessProfileApplier();
            var service = CreateService(profilesDirectory, topologyProvider: null, profileApplier);

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.True(result);
                Assert.Equal(1, profileApplier.LegacyAffinityApplyCalls);
                Assert.Equal(0b11, profileApplier.LastLegacyAffinityMask);
                Assert.Equal(0, profileApplier.CpuSelectionApplyCalls);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public async Task LoadProcessProfile_WithRealtimePriority_ReturnsFalseWithoutApplyingPriorityOrAffinity()
        {
            var profilesDirectory = CreateTemporaryDirectory();
            var profileName = $"profile-{Guid.NewGuid():N}";
            var profile = new ProcessProfileSnapshot
            {
                ProcessName = "game.exe",
                Priority = ProcessPriorityClass.RealTime,
                ProcessorAffinity = 0b11,
            };
            var profileApplier = new FakeLoadProcessProfileApplier();
            var service = CreateService(profilesDirectory, topologyProvider: null, profileApplier);

            try
            {
                await WriteProfileAsync(profilesDirectory, profileName, profile);

                var result = await service.LoadProcessProfile(profileName, CreateProcess());

                Assert.False(result);
                Assert.Equal(0, profileApplier.PriorityApplyCalls);
                Assert.Equal(0, profileApplier.LegacyAffinityApplyCalls);
                Assert.Equal(0, profileApplier.CpuSelectionApplyCalls);
            }
            finally
            {
                DeleteDirectory(profilesDirectory);
            }
        }

        [Fact]
        public void PriorityGuardrails_HighPriorityReturnsUserFacingWarning()
        {
            var warning = ProcessPriorityGuardrails.GetWarning(ProcessPriorityClass.High);

            Assert.Equal(ProcessOperationUserMessages.HighPriorityWarning, warning);
        }

        [Fact]
        public async Task SetProcessPriority_WithRealtime_AuditsFailureAndThrowsBlockedMessage()
        {
            var logger = new Mock<ILogger<ProcessService>>();
            var security = new Mock<ISecurityService>(MockBehavior.Strict);
            security
                .Setup(s => s.AuditElevatedAction("SetProcessPriority", "game.exe", false))
                .Returns(Task.CompletedTask);

            var service = CreateService(CreateTemporaryDirectory(), logger: logger.Object, securityService: security.Object);
            var process = CreateProcess();

            try
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.SetProcessPriority(process, ProcessPriorityClass.RealTime));

                Assert.Equal(ProcessOperationUserMessages.RealtimePriorityBlocked, ex.Message);
                Assert.Equal(ProcessPriorityClass.Normal, process.Priority);
                security.Verify(
                    s => s.AuditElevatedAction("SetProcessPriority", "game.exe", false),
                    Times.Once);
                VerifyWarningLogged(logger, ProcessOperationUserMessages.RealtimePriorityBlocked);
            }
            finally
            {
                DeleteDirectory(GetProfilesDirectory(service));
            }
        }

        [Fact]
        public async Task SetRegistryPriorityAsync_WithRealtime_ReturnsFalse()
        {
            var service = CreateService(CreateTemporaryDirectory());

            try
            {
                var result = await service.SetRegistryPriorityAsync(CreateProcess(), enable: true, ProcessPriorityClass.RealTime);

                Assert.False(result);
                Assert.Contains("does not bypass", ProcessOperationUserMessages.PersistentLaunchTimePriorityNotice, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                DeleteDirectory(GetProfilesDirectory(service));
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
            ICpuTopologyProvider? topologyProvider = null,
            FakeLoadProcessProfileApplier? profileApplier = null,
            ILogger<ProcessService>? logger = null,
            ISecurityService? securityService = null)
        {
            if (profileApplier == null)
            {
                return new(logger, securityService, () => profilesDirectory, cpuTopologyProvider: topologyProvider);
            }

            return new ProcessService(
                logger,
                securityService,
                () => profilesDirectory,
                foregroundProcessService: null,
                processClassifier: null,
                passiveProcessErrorThrottle: null,
                cpuTopologyProvider: topologyProvider,
                cpuSelectionMigrationService: null,
                loadProcessProfilePrioritySetter: profileApplier.SetPriorityAsync,
                loadProcessProfileCpuSelectionSetter: profileApplier.SetCpuSelectionAsync,
                loadProcessProfileLegacyAffinitySetter: profileApplier.SetLegacyAffinityAsync);
        }

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

        private static void VerifyWarningLogged(Mock<ILogger<ProcessService>> logger, string message)
        {
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString() != null && state.ToString()!.Contains(message, StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        private sealed class FakeCpuTopologyProvider(CpuTopologySnapshot snapshot) : ICpuTopologyProvider
        {
            public Task<CpuTopologySnapshot> GetTopologySnapshotAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult(snapshot);
        }

        private sealed class FakeLoadProcessProfileApplier
        {
            private readonly AffinityApplyResult cpuSelectionResult;

            public FakeLoadProcessProfileApplier(AffinityApplyResult? cpuSelectionResult = null)
            {
                this.cpuSelectionResult = cpuSelectionResult ?? AffinityApplyResult.Succeeded(0, 0);
            }

            public int PriorityApplyCalls { get; private set; }

            public int CpuSelectionApplyCalls { get; private set; }

            public int LegacyAffinityApplyCalls { get; private set; }

            public long LastLegacyAffinityMask { get; private set; }

            public Task SetPriorityAsync(ProcessModel process, ProcessPriorityClass priority)
            {
                this.PriorityApplyCalls++;
                process.Priority = priority;
                return Task.CompletedTask;
            }

            public Task<AffinityApplyResult> SetCpuSelectionAsync(ProcessModel process, CpuSelection selection)
            {
                this.CpuSelectionApplyCalls++;
                return Task.FromResult(this.cpuSelectionResult);
            }

            public Task SetLegacyAffinityAsync(ProcessModel process, long affinityMask)
            {
                this.LegacyAffinityApplyCalls++;
                this.LastLegacyAffinityMask = affinityMask;
                process.ProcessorAffinity = affinityMask;
                return Task.CompletedTask;
            }
        }
    }
}
