namespace ThreadPilot.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    public sealed class ApplicationSettingsServiceTests
    {
        [Fact]
        public async Task LoadSettingsAsync_CreatesDefaults_WhenFileIsMissing()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(storage.Writes.ContainsKey(TestPaths.SettingsFilePath));
            Assert.Equal(3000, service.Settings.NotificationDisplayDurationMs);
            Assert.Equal(5000, service.Settings.BalloonNotificationTimeoutMs);
        }

        [Fact]
        public async Task LoadSettingsAsync_FallsBackToDefaults_WhenJsonIsMalformed()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = "{ invalid json";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(3000, service.Settings.NotificationDisplayDurationMs);
            Assert.Equal(string.Empty, service.Settings.CustomTrayIconPath);
        }

        [Fact]
        public async Task ImportSettingsAsync_Throws_WhenFileIsMissing()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);

            await Assert.ThrowsAsync<FileNotFoundException>(() => service.ImportSettingsAsync("missing-settings.json"));
        }

        [Fact]
        public async Task ValidateAndFixSettings_DisablesMissingCustomTrayIcon()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);
            var updatedSettings = new ApplicationSettingsModel
            {
                UseCustomTrayIcon = true,
                CustomTrayIconPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ico"),
            };

            await service.UpdateSettingsAsync(updatedSettings);

            Assert.False(service.Settings.UseCustomTrayIcon);
        }

        private static ApplicationSettingsService CreateService(FakeSettingsStorage storage)
        {
            return new ApplicationSettingsService(
                NullLogger<ApplicationSettingsService>.Instance,
                storage,
                TestPaths.SettingsFilePath,
                legacySettingsPath: null);
        }

        private static class TestPaths
        {
            public const string SettingsFilePath = "settings-under-test.json";
        }

        private sealed class FakeSettingsStorage : ISettingsStorage
        {
            public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> Writes { get; } = new(StringComparer.OrdinalIgnoreCase);

            public void Copy(string sourcePath, string destinationPath, bool overwrite)
            {
                if (!this.Files.TryGetValue(sourcePath, out var content))
                {
                    throw new FileNotFoundException("Source file not found.", sourcePath);
                }

                if (!overwrite && this.Files.ContainsKey(destinationPath))
                {
                    throw new IOException("Destination already exists.");
                }

                this.Files[destinationPath] = content;
            }

            public void EnsureDirectoryForFile(string path)
            {
            }

            public bool Exists(string path)
            {
                return this.Files.ContainsKey(path);
            }

            public Task<string?> ReadAsync(string path)
            {
                this.Files.TryGetValue(path, out var content);
                return Task.FromResult<string?>(content);
            }

            public Task WriteAsync(string path, string content)
            {
                this.Files[path] = content;
                this.Writes[path] = content;
                return Task.CompletedTask;
            }
        }
    }
}
