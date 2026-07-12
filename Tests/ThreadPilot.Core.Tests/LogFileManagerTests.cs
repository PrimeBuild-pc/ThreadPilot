namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    public sealed class LogFileManagerTests
    {
        [Fact]
        public async Task WriteReadStatisticsAndExport_RoundTripStructuredEntries()
        {
            var directory = CreateTemporaryDirectory();
            var exportPath = Path.Combine(directory, "export.txt");
            using var manager = new LogFileManager(NullLogger<LogFileManager>.Instance, directory);
            var now = DateTime.UtcNow;
            var information = CreateEntry(now.AddMinutes(-2), "Information", "started");
            var warning = CreateEntry(now.AddMinutes(-1), "Warning", "warning");
            var error = CreateEntry(now, "Error", "failed");

            try
            {
                await manager.InitializeAsync();
                await manager.WriteLogEntriesAsync([information, "not-json", warning]);
                await manager.WriteLogEntryAsync(error);

                var entries = await manager.ReadLogEntriesAsync(now.AddMinutes(-3), now.AddMinutes(1), maxEntries: 2);
                var statistics = await manager.GetStatisticsAsync();
                var resultPath = await manager.ExportLogsAsync(now.AddMinutes(-3), now.AddMinutes(1), exportPath);

                Assert.Equal([error, warning], entries);
                Assert.Equal(1, statistics.TotalLogFiles);
                Assert.Equal(1, statistics.InfoCount);
                Assert.Equal(1, statistics.WarningCount);
                Assert.Equal(1, statistics.ErrorCount);
                Assert.True(statistics.CurrentFileSizeBytes > 0);
                Assert.Equal(exportPath, resultPath);
                var export = await File.ReadAllTextAsync(exportPath);
                Assert.Contains("# Total Entries: 3", export, StringComparison.Ordinal);
                Assert.Contains(information, export, StringComparison.Ordinal);
                Assert.DoesNotContain("not-json", export, StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Fact]
        public async Task WriteLogEntry_WhenSizeLimitExceeded_RotatesAndKeepsCurrentLog()
        {
            var directory = CreateTemporaryDirectory();
            using var manager = new LogFileManager(NullLogger<LogFileManager>.Instance, directory);

            try
            {
                await manager.InitializeAsync();
                manager.UpdateConfiguration(maxFileSizeMb: 0, retentionDays: 7);

                await manager.WriteLogEntryAsync(CreateEntry(DateTime.UtcNow, "Information", "after rotation"));

                var files = Directory.GetFiles(directory, "*.log");
                Assert.Equal(2, files.Length);
                Assert.Contains(files, path => Path.GetFileName(path) == "ThreadPilot.log");
                Assert.Contains(files, path => Path.GetFileName(path).StartsWith("ThreadPilot_", StringComparison.Ordinal));
                Assert.Contains("after rotation", await File.ReadAllTextAsync(manager.CurrentLogFilePath), StringComparison.Ordinal);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Fact]
        public async Task CleanupOldLogs_RemovesExpiredAndExcessFilesButKeepsCurrentLog()
        {
            var directory = CreateTemporaryDirectory();
            using var manager = new LogFileManager(NullLogger<LogFileManager>.Instance, directory);

            try
            {
                await manager.InitializeAsync();
                var expired = Path.Combine(directory, "ThreadPilot_expired.log");
                var older = Path.Combine(directory, "ThreadPilot_older.log");
                var newer = Path.Combine(directory, "ThreadPilot_newer.log");
                await File.WriteAllTextAsync(expired, "expired");
                await File.WriteAllTextAsync(older, "older");
                await File.WriteAllTextAsync(newer, "newer");
                File.SetCreationTimeUtc(expired, DateTime.UtcNow.AddDays(-10));
                File.SetCreationTimeUtc(older, DateTime.UtcNow.AddHours(-2));
                File.SetCreationTimeUtc(newer, DateTime.UtcNow.AddHours(-1));
                manager.UpdateConfiguration(maxFileSizeMb: 10, retentionDays: 2, maxLogFiles: 1);

                await manager.CleanupOldLogsAsync();

                Assert.True(File.Exists(manager.CurrentLogFilePath));
                Assert.False(File.Exists(expired));
                Assert.False(File.Exists(older));
                Assert.True(File.Exists(newer));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static string CreateEntry(DateTime timestamp, string level, string message) =>
            $"{{\"timestamp\":\"{timestamp:yyyy-MM-dd HH:mm:ss.fff}\",\"level\":\"{level}\",\"message\":\"{message}\"}}";

        private static string CreateTemporaryDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"threadpilot-log-manager-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
