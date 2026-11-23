namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ProcessPowerPlanAssociationServiceTests
    {
        [Fact]
        public async Task FailedWrite_PreservesPublishedConfigurationAndReturnsFailure()
        {
            var directory = Directory.CreateTempSubdirectory("threadpilot-association-");
            var file = Path.Combine(directory.FullName, "ProcessPowerPlanAssociations.json");
            Directory.CreateDirectory(file);
            try
            {
                var service = new ProcessPowerPlanAssociationService(directory.FullName);
                var events = new List<string>();
                service.ConfigurationChanged += (_, args) => events.Add(args.ChangeType);
                var proposed = new ProcessMonitorConfiguration { DefaultPowerPlanGuid = "imported" };
                var association = new ProcessPowerPlanAssociation("game", "fast", "Fast");

                Assert.False(await service.SetDefaultPowerPlanAsync("balanced", "Balanced"));
                Assert.False(await service.AddAssociationAsync(association));
                Assert.False(await service.SaveMonitoringSettingsAsync(false, 321));
                Assert.False(await service.ReplaceConfigurationAsync(proposed));
                Assert.Empty(service.Configuration.Associations);
                Assert.Empty(service.Configuration.DefaultPowerPlanGuid);
                Assert.NotEqual(321, service.Configuration.PowerPlanChangeDelayMs);
                Assert.Equal(new[] { "SaveError", "SaveError", "SaveError", "SaveError" }, events);

                Directory.Delete(file);
                Assert.True(await service.SetDefaultPowerPlanAsync("balanced", "Balanced"));
                Assert.True(await service.AddAssociationAsync(association));
                var reloaded = new ProcessPowerPlanAssociationService(directory.FullName);
                Assert.True(await reloaded.LoadConfigurationAsync());
                Assert.Equal("balanced", reloaded.Configuration.DefaultPowerPlanGuid);
                Assert.Equal("game", Assert.Single(reloaded.Configuration.Associations).ExecutableName);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }
    }
}
