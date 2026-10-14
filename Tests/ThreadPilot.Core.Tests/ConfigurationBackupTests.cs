namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
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
            harness.Rules.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyList<PersistentProcessRule>>()), Times.Never);
        }

        [Fact]
        public async Task FailedImport_RestoresAllPreviousConfiguration()
        {
            var harness = new Harness();
            harness.Associations.SetupSequence(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>()))
                .ReturnsAsync(false).ReturnsAsync(true);
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.PersistentProcessRules = [new() { Id = "new", ProcessName = "new.exe" }];
            await Assert.ThrowsAsync<IOException>(() => harness.Service.ApplyAsync(backup));
            harness.Rules.Verify(x => x.SaveAsync(It.Is<IReadOnlyList<PersistentProcessRule>>(r => r.Single().Id == "old")), Times.Once);
            harness.Settings.Verify(x => x.UpdateSettingsAsync(It.IsAny<ApplicationSettingsModel>()), Times.Once);
        }

        [Fact]
        public async Task InvalidRule_IsRejectedBeforeAnyWrite()
        {
            var harness = new Harness();
            var backup = await harness.Service.CaptureAsync(harness.Settings.Object.Settings);
            backup.PersistentProcessRules = [new() { ProcessName = "game.exe", Priority = ProcessPriorityClass.RealTime }];
            await Assert.ThrowsAsync<InvalidDataException>(() => harness.Service.ApplyAsync(backup));
            harness.Rules.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyList<PersistentProcessRule>>()), Times.Never);
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

            public Mock<IPersistentProcessRuleStore> Rules { get; } = new();

            public ConfigurationBackupService Service { get; }

            public Harness()
            {
                this.Settings.SetupGet(x => x.Settings).Returns(new ApplicationSettingsModel());
                this.Associations.SetupGet(x => x.Configuration).Returns(new ProcessMonitorConfiguration());
                this.Associations.Setup(x => x.ReplaceConfigurationAsync(It.IsAny<ProcessMonitorConfiguration>())).ReturnsAsync(true);
                this.Rules.Setup(x => x.LoadAsync()).ReturnsAsync([new() { Id = "old", ProcessName = "old.exe" }]);
                this.Service = new(this.Settings.Object, this.Associations.Object, this.Rules.Object);
            }
        }
    }
}
