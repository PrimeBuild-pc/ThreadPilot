namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class EnhancedLoggingServiceTests : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("threadpilot-log-");

        [Fact]
        public async Task TemporaryWriteFailure_RetriesEarlierEventsInOrder()
        {
            using var service = this.CreateService();
            await service.InitializeAsync();
            using (File.Open(service.CurrentLogFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await service.LogSystemEventAsync("Test", "first event", LogLevel.Error);
            }

            await service.LogSystemEventAsync("Test", "second event", LogLevel.Error);
            var events = await service.GetLogEntriesAsync(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
            Assert.Contains(events, entry => entry.Message == "first event");
            Assert.Contains(events, entry => entry.Message == "second event");
            var raw = await File.ReadAllTextAsync(service.CurrentLogFilePath);
            Assert.True(raw.IndexOf("first event", StringComparison.Ordinal) < raw.IndexOf("second event", StringComparison.Ordinal));
            Assert.Single(events, entry => entry.Message == "first event");
        }

        [Fact]
        public async Task OversizeLog_RotatesAndKeepsNewEventsAndExport()
        {
            using var service = this.CreateService();
            await service.InitializeAsync();
            await File.AppendAllTextAsync(service.CurrentLogFilePath, new string(' ', (1024 * 1024) + 1) + Environment.NewLine);
            await service.LogSystemEventAsync("Test", "after rotation", LogLevel.Error);

            var paths = this.directory.GetFiles("ThreadPilot*.log");
            Assert.Equal(2, paths.Length);
            Assert.Contains(paths, file => file.Name.StartsWith("ThreadPilot_", StringComparison.Ordinal));
            Assert.Contains("after rotation", await File.ReadAllTextAsync(service.CurrentLogFilePath), StringComparison.Ordinal);
            var exported = await service.ExportLogsAsync(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1), Path.Combine(this.directory.FullName, "export.txt"));
            Assert.Contains("after rotation", await File.ReadAllTextAsync(exported), StringComparison.Ordinal);
            Assert.Equal(2, (await service.GetLogStatisticsAsync()).TotalLogFiles);
        }

        [Fact]
        public async Task Cleanup_RemovesExpiredLogsAndKeepsCurrentAndRecentFiles()
        {
            using var service = this.CreateService();
            await service.InitializeAsync();
            var expired = Path.Combine(this.directory.FullName, "ThreadPilot_expired.log");
            var recent = Path.Combine(this.directory.FullName, "ThreadPilot_recent.log");
            await File.WriteAllTextAsync(expired, "expired");
            await File.WriteAllTextAsync(recent, "recent");
            File.SetCreationTimeUtc(expired, DateTime.UtcNow.AddDays(-10));

            await service.CleanupOldLogsAsync();

            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(service.CurrentLogFilePath));
        }

        public void Dispose() => this.directory.Delete(recursive: true);

        private EnhancedLoggingService CreateService()
        {
            var settings = new Mock<IApplicationSettingsService>();
            settings.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel
            {
                MaxLogFileSizeMb = 1,
                LogRetentionDays = 2,
            });
            return new EnhancedLoggingService(NullLogger<EnhancedLoggingService>.Instance, settings.Object, this.directory.FullName);
        }
    }
}
