/*
 * ThreadPilot - process service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Collections.Concurrent;
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

        private static ProcessService CreateService(string profilesDirectory) =>
            new(null, null, () => profilesDirectory);

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
    }
}
