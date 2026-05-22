/*
 * ThreadPilot - dependency injection registration tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class ServiceConfigurationTests
    {
        [Fact]
        public void ConfigureApplicationServices_RegistersPersistentRuleAutoApplyService()
        {
            using var provider = CreateProvider();

            var service = provider.GetRequiredService<IPersistentRuleAutoApplyService>();

            Assert.IsType<PersistentRuleAutoApplyService>(service);
        }

        [Fact]
        public void ConfigureApplicationServices_RegistersProcessRuleCreationService()
        {
            using var provider = CreateProvider();

            var service = provider.GetRequiredService<IProcessRuleCreationService>();

            Assert.IsType<ProcessRuleCreationService>(service);
        }

        [Fact]
        public void ApplicationSettings_DefaultsToPersistentRulesAutoApplyEnabled()
        {
            var settings = new ApplicationSettingsModel();

            Assert.True(settings.ApplyPersistentRulesOnProcessStart);
        }

        private static ServiceProvider CreateProvider() =>
            new ServiceCollection()
                .ConfigureApplicationServices()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
