namespace ThreadPilot.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
            Assert.True(service.Settings.EnableSelfLowImpactMode);
            Assert.False(service.Settings.EnableSelfAffinityLimit);
            Assert.True(service.Settings.AutostartWithWindows);
            Assert.False(service.Settings.StartMinimized);
            Assert.Equal("en-US", service.Settings.Language);
        }

        [Fact]
        public async Task LoadSettingsAsync_DefaultsAutomationMonitoringEnabled_ForOlderSettingsJson()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = "{}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.EnableAutomationMonitoring);
            Assert.Equal(CpuAssignmentMode.AffinityMask, service.Settings.DefaultCpuAssignmentMode);
        }

        [Fact]
        public async Task LoadSettingsAsync_PersistsCompletionForANonAutomaticProfile()
        {
            // Without this the policy re-runs on every launch, and a later deliberate switch back
            // to Automatic would be silently undone by a second migration.
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] =
                "{\"defaultCpuAssignmentMode\": 3, \"language\": \"en-US\"}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.CpuSets, service.Settings.DefaultCpuAssignmentMode);
            Assert.True(service.Settings.HasMigratedCpuAssignmentModeDefault);
            Assert.True(service.Settings.HasSeenCpuAssignmentModeChangeNotice);
            Assert.True(storage.Writes.ContainsKey(TestPaths.SettingsFilePath));

            var reloaded = CreateService(storage);
            await reloaded.LoadSettingsAsync();
            Assert.Equal(CpuAssignmentMode.CpuSets, reloaded.Settings.DefaultCpuAssignmentMode);
            Assert.True(reloaded.Settings.HasMigratedCpuAssignmentModeDefault);
        }

        [Fact]
        public async Task LoadSettingsAsync_RepairsACorruptModeWithoutAnnouncingAChange()
        {
            // A value out of the enum range is not a choice the user made, so telling them their
            // Automatic setting was changed would be a lie.
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] =
                "{\"defaultCpuAssignmentMode\": 99, \"language\": \"en-US\"}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.AffinityMask, service.Settings.DefaultCpuAssignmentMode);
            Assert.True(service.Settings.HasSeenCpuAssignmentModeChangeNotice);
        }

        [Fact]
        public async Task LoadSettingsAsync_KeepsTheMigratedProfileWhenPersistingItFails()
        {
            // Falling into the outer catch would replace everything the user configured with
            // defaults and mark the notice seen, hiding the change entirely.
            var storage = new FakeSettingsStorage { FailWrites = true };
            storage.Files[TestPaths.SettingsFilePath] =
                "{\"defaultCpuAssignmentMode\": 0, \"maxNotificationHistoryItems\": 42, \"language\": \"en-US\"}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.AffinityMask, service.Settings.DefaultCpuAssignmentMode);
            Assert.Equal(42, service.Settings.MaxNotificationHistoryItems);
            Assert.False(service.Settings.HasSeenCpuAssignmentModeChangeNotice);
        }

        [Fact]
        public async Task LoadSettingsAsync_MigratesAnUpgradedProfileOffAutomaticAndPersistsIt()
        {
            // A profile written by 1.7.0 or earlier: Automatic was the shipped default then, and the
            // persisted value kept winning over the new one after the 1.7.1 upgrade.
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] =
                "{\"defaultCpuAssignmentMode\": 0, \"language\": \"en-US\"}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.AffinityMask, service.Settings.DefaultCpuAssignmentMode);
            Assert.True(service.Settings.HasMigratedCpuAssignmentModeDefault);
            Assert.False(service.Settings.HasSeenCpuAssignmentModeChangeNotice);

            // Must be written back, or the migration would run again on every launch.
            Assert.True(storage.Writes.ContainsKey(TestPaths.SettingsFilePath));
            var reloaded = CreateService(storage);
            await reloaded.LoadSettingsAsync();
            Assert.Equal(CpuAssignmentMode.AffinityMask, reloaded.Settings.DefaultCpuAssignmentMode);
            Assert.True(reloaded.Settings.HasMigratedCpuAssignmentModeDefault);
            Assert.False(reloaded.Settings.HasSeenCpuAssignmentModeChangeNotice);
        }

        [Fact]
        public async Task LoadSettingsAsync_DoesNotAnnounceAnythingToAFreshProfile()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.AffinityMask, service.Settings.DefaultCpuAssignmentMode);
            Assert.True(service.Settings.HasMigratedCpuAssignmentModeDefault);
            Assert.True(service.Settings.HasSeenCpuAssignmentModeChangeNotice);
        }

        [Fact]
        public async Task LoadSettingsAsync_KeepsAutomaticChosenAfterTheMigration()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] =
                "{\"defaultCpuAssignmentMode\": 0, \"hasMigratedCpuAssignmentModeDefault\": true, " +
                "\"hasSeenCpuAssignmentModeChangeNotice\": true, \"language\": \"en-US\"}";
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.Equal(CpuAssignmentMode.Automatic, service.Settings.DefaultCpuAssignmentMode);
        }

        [Fact]
        public async Task UpdateSettingsAsync_RoundTripsDefaultCpuAssignmentMode()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);
            await service.LoadSettingsAsync();
            var settings = service.Settings;
            settings.DefaultCpuAssignmentMode = CpuAssignmentMode.CpuSets;

            await service.UpdateSettingsAsync(settings);

            var reloaded = CreateService(storage);
            await reloaded.LoadSettingsAsync();
            Assert.Equal(CpuAssignmentMode.CpuSets, reloaded.Settings.DefaultCpuAssignmentMode);
        }

        [Fact]
        public async Task UpdateSettingsAsync_PersistsAutomationMonitoringDisabled()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage);
            await service.LoadSettingsAsync();
            var settings = service.Settings;
            settings.EnableAutomationMonitoring = false;

            await service.UpdateSettingsAsync(settings);

            var reloaded = CreateService(storage);
            await reloaded.LoadSettingsAsync();
            Assert.False(reloaded.Settings.EnableAutomationMonitoring);
        }

        [Fact]
        public async Task LoadSettingsAsync_UsesAndPersistsWindowsLanguage_WhenFileIsMissing()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage, "it-CH");

            await service.LoadSettingsAsync();

            Assert.Equal("it-IT", service.Settings.Language);
            Assert.Contains("\"language\": \"it-IT\"", storage.Writes[TestPaths.SettingsFilePath], StringComparison.Ordinal);
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
            Assert.True(service.Settings.EnableSelfLowImpactMode);
            Assert.False(service.Settings.EnableSelfAffinityLimit);
        }

        [Fact]
        public async Task LoadSettingsAsync_EnablesSafeSelfLowImpactDefault_ForOlderSettingsJson()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "notificationDisplayDurationMs": 3000,
                  "balloonNotificationTimeoutMs": 5000
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.EnableSelfLowImpactMode);
            Assert.False(service.Settings.EnableSelfAffinityLimit);
        }

        [Fact]
        public async Task LoadSettingsAsync_PreservesExplicitSelfLowImpactOptOut()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "enableSelfLowImpactMode": false,
                  "enableSelfAffinityLimit": true
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.False(service.Settings.EnableSelfLowImpactMode);
            Assert.True(service.Settings.EnableSelfAffinityLimit);
        }

        [Fact]
        public async Task LoadSettingsAsync_DefaultsStartMinimizedFalse_ForOlderAutostartSettingsJson()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "autostartWithWindows": true
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.AutostartWithWindows);
            Assert.False(service.Settings.StartMinimized);
        }

        [Fact]
        public async Task LoadSettingsAsync_PreservesExplicitStartMinimizedOptOut()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "autostartWithWindows": true,
                  "startMinimized": false
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.AutostartWithWindows);
            Assert.False(service.Settings.StartMinimized);
        }

        [Fact]
        public async Task LoadSettingsAsync_PreservesExplicitStartMinimizedOptIn()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "autostartWithWindows": true,
                  "startMinimized": true
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.AutostartWithWindows);
            Assert.True(service.Settings.StartMinimized);
        }

        [Fact]
        public async Task LoadSettingsAsync_PreservesStartupMinimizedSuggestionDismissal()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "hasSeenStartupMinimizedSuggestion": true
                }
                """;
            var service = CreateService(storage);

            await service.LoadSettingsAsync();

            Assert.True(service.Settings.HasSeenStartupMinimizedSuggestion);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("zh-CN")]
        [InlineData("it-IT")]
        [InlineData("fr-FR")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("ru-RU")]
        public async Task LoadSettingsAsync_PreservesSupportedLanguage(string language)
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = $$"""
                {
                  "language": "{{language}}"
                }
                """;
            var service = CreateService(storage, "it-IT");

            await service.LoadSettingsAsync();

            Assert.Equal(language, service.Settings.Language);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("pt-BR")]
        [InlineData("zh")]
        public async Task LoadSettingsAsync_UsesAndPersistsWindowsLanguage_WhenLanguageIsInvalid(string language)
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = $$"""
                {
                  "language": "{{language}}"
                }
                """;
            var service = CreateService(storage, "ru-KZ");

            await service.LoadSettingsAsync();

            Assert.Equal("ru-RU", service.Settings.Language);
            Assert.Contains("\"language\": \"ru-RU\"", storage.Writes[TestPaths.SettingsFilePath], StringComparison.Ordinal);
        }

        [Fact]
        public async Task LoadSettingsAsync_UsesWindowsLanguage_WhenLanguagePropertyIsMissing()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "startMinimized": true
                }
                """;
            var service = CreateService(storage, "fr-CA");

            await service.LoadSettingsAsync();

            Assert.Equal("fr-FR", service.Settings.Language);
            Assert.Contains("\"language\": \"fr-FR\"", storage.Writes[TestPaths.SettingsFilePath], StringComparison.Ordinal);
        }

        [Fact]
        public async Task LoadSettingsAsync_NormalizesAndPersistsSupportedLanguageCasing()
        {
            var storage = new FakeSettingsStorage();
            storage.Files[TestPaths.SettingsFilePath] = """
                {
                  "language": "IT-it"
                }
                """;
            var service = CreateService(storage, "ru-RU");

            await service.LoadSettingsAsync();

            Assert.Equal("it-IT", service.Settings.Language);
            Assert.Contains("\"language\": \"it-IT\"", storage.Writes[TestPaths.SettingsFilePath], StringComparison.Ordinal);
        }

        [Fact]
        public async Task ResetToDefaultsAsync_UsesWindowsLanguage()
        {
            var storage = new FakeSettingsStorage();
            var service = CreateService(storage, "de-AT");

            await service.ResetToDefaultsAsync();

            Assert.Equal("de-DE", service.Settings.Language);
        }

        [Fact]
        public async Task ImportSettingsAsync_UsesWindowsLanguage_WhenLanguageIsInvalid()
        {
            const string ImportPath = "import-settings.json";
            var storage = new FakeSettingsStorage();
            storage.Files[ImportPath] = """
                {
                  "language": "unsupported"
                }
                """;
            var service = CreateService(storage, "es-MX");

            await service.ImportSettingsAsync(ImportPath);

            Assert.Equal("es-ES", service.Settings.Language);
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

        private static ApplicationSettingsService CreateService(
            FakeSettingsStorage storage,
            string systemUiCultureName = "en-US")
        {
            return new ApplicationSettingsService(
                NullLogger<ApplicationSettingsService>.Instance,
                storage,
                TestPaths.SettingsFilePath,
                legacySettingsPath: null,
                () => new CultureInfo(systemUiCultureName));
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

            public bool FailWrites { get; init; }

            public Task WriteAsync(string path, string content)
            {
                if (this.FailWrites)
                {
                    throw new IOException("Simulated write failure.");
                }

                this.Files[path] = content;
                this.Writes[path] = content;
                return Task.CompletedTask;
            }
        }
    }
}
