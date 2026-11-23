namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ConfigurationBackupTests
    {
        [Fact]
        public async Task ExportRoundTrip_PreservesPersistentRules()
        {
            var harness = new Harness();
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            var imported = ConfigurationBackupService.Parse(ConfigurationBackupService.Serialize(backup));
            Assert.Equal("old", Assert.Single(imported!.PersistentProcessRules!).Id);
        }

        [Fact]
        public async Task LegacyBundle_KeepsPersistentRules()
        {
            var harness = new Harness();
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.SchemaVersion = "2.0";
            backup.PersistentProcessRules = null;
            await harness.Service.ApplyAsync(ConfigurationBackupService.Parse(ConfigurationBackupService.Serialize(backup))!);
            Assert.Empty(harness.Rules.Writes);
        }

        [Fact]
        public async Task FailedImport_KeepsConcurrentCommittedSettingsEdit()
        {
            var file = Path.Combine(Path.GetTempPath(), $"tp-review-{Guid.NewGuid():N}.json");
            try
            {
                var settings = new ApplicationSettingsService(
                    NullLogger<ApplicationSettingsService>.Instance,
                    new FileSettingsStorage(), file, null);
                await settings.EditSettingsAsync(draft => draft.StartMinimized = false);
                var harness = new Harness();
                var service = new ConfigurationBackupService(settings, harness.Associations.Object, harness.Rules);
                var backup = await service.CaptureAsync(settings.Settings);
                var replacing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var writes = 0;
                harness.Associations.Setup(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()))
                    .Returns(() =>
                    {
                        if (Interlocked.Increment(ref writes) == 1)
                        {
                            replacing.SetResult();
                            return release.Task;
                        }

                        return Task.FromResult(true);
                    });

                var importing = service.ApplyAsync(backup);
                try
                {
                    await replacing.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    await settings.EditSettingsAsync(draft => draft.StartMinimized = true);
                }
                finally
                {
                    release.TrySetResult(false);
                }

                await Assert.ThrowsAsync<IOException>(() => importing);
                var restarted = new ApplicationSettingsService(
                    NullLogger<ApplicationSettingsService>.Instance,
                    new FileSettingsStorage(), file, null);
                await restarted.LoadSettingsAsync();
                Assert.True(restarted.Settings.StartMinimized);
                Assert.True(settings.Settings.StartMinimized);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task FailedAssociationImport_RestoresRulesAndLeavesOtherSettingsAlone()
        {
            var harness = new Harness();
            harness.Associations.Setup(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()))
                .ReturnsAsync(false);
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.PersistentProcessRules = [new() { Id = "new", ProcessName = "new.exe" }];
            await Assert.ThrowsAsync<IOException>(() => harness.Service.ApplyAsync(backup));
            Assert.Equal("old", Assert.Single(await harness.Rules.LoadAsync()).Id);
            Assert.Equal(2, harness.Rules.Writes.Count);
            harness.Settings.Verify(x => x.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()), Times.Never);
            harness.Associations.Verify(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()), Times.Once);
        }

        [Fact]
        public async Task FailedSettingsImport_RestoresRulesAndAssociationsAlreadyChanged()
        {
            var harness = new Harness();
            var previous = new ProcessMonitorConfiguration { DefaultPowerPlanGuid = "original" };
            var current = previous;
            harness.Associations.SetupGet(x => x.Configuration).Returns(() => current);
            harness.Associations.Setup(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()))
                .ReturnsAsync((ProcessMonitorConfiguration replacement) =>
                {
                    current = replacement;
                    return true;
                });
            harness.Settings.Setup(x => x.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()))
                .ThrowsAsync(new IOException("Settings unavailable"));
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.ProcessMonitorConfiguration!.DefaultPowerPlanGuid = "imported";
            backup.PersistentProcessRules = [new() { Id = "new", ProcessName = "new.exe" }];

            await Assert.ThrowsAsync<IOException>(() => harness.Service.ApplyAsync(backup));

            Assert.Equal("original", current.DefaultPowerPlanGuid);
            Assert.Equal("old", Assert.Single(await harness.Rules.LoadAsync()).Id);
            harness.Settings.Verify(x => x.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()), Times.Once);
        }

        [Fact]
        public async Task InvalidRule_IsRejectedBeforeAnyWrite()
        {
            var harness = new Harness();
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.PersistentProcessRules = [new() { ProcessName = "game.exe", Priority = ProcessPriorityClass.RealTime }];
            await Assert.ThrowsAsync<InvalidDataException>(() => harness.Service.ApplyAsync(backup));
            Assert.Empty(harness.Rules.Writes);
            harness.Associations.Verify(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()), Times.Never);
        }

        [Fact]
        public void IncompleteBundle_DoesNotFallBackToLegacySettingsImport()
        {
            Assert.Throws<InvalidDataException>(() => ConfigurationBackupService.Parse("{\"settings\":{}}"));
        }

        private sealed class Harness
        {
            public Mock<IApplicationSettingsService> Settings { get; } = new();

            public Mock<IProcessPowerPlanAssociationService> Associations { get; } = new();

            public RuleStore Rules { get; } = new();

            public ConfigurationBackupService Service { get; }

            public Harness()
            {
                this.Settings.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel());
                this.Associations.SetupGet(x => x.Configuration).Returns(new ProcessMonitorConfiguration());
                this.Associations.Setup(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>())).ReturnsAsync(true);
                this.Service = new(this.Settings.Object, this.Associations.Object, this.Rules);
            }
        }

        private sealed class RuleStore : IPersistentProcessRuleStore
        {
            private IReadOnlyList<PersistentProcessRule> rules = [new() { Id = "old", ProcessName = "old.exe" }];

            public List<IReadOnlyList<PersistentProcessRule>> Writes { get; } = new();
            public Task<IReadOnlyList<PersistentProcessRule>> LoadAsync() => Task.FromResult(this.rules);

            public Task SaveAsync(IReadOnlyList<PersistentProcessRule> replacement)
            {
                this.rules = replacement.ToArray();
                this.Writes.Add(this.rules);
                return Task.CompletedTask;
            }
        }
    }
}
