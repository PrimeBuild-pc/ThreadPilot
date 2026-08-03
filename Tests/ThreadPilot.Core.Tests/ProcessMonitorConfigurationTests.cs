namespace ThreadPilot.Core.Tests
{
    using System.Text.Json;
    using ThreadPilot.Models;

    public sealed class ProcessMonitorConfigurationTests
    {
        [Fact]
        public void LegacyMonitoringFields_AreIgnoredWhenConfigurationIsDeserialized()
        {
            const string json = """
                {
                  "isEventBasedMonitoringEnabled": false,
                  "isFallbackPollingEnabled": false,
                  "pollingIntervalSeconds": 1,
                  "preventDuplicatePowerPlanChanges": false
                }
                """;

            var configuration = JsonSerializer.Deserialize<ProcessMonitorConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            Assert.NotNull(configuration);
            Assert.False(configuration.PreventDuplicatePowerPlanChanges);
        }

        [Theory]
        [InlineData("IsEventBasedMonitoringEnabled")]
        [InlineData("IsFallbackPollingEnabled")]
        [InlineData("PollingIntervalSeconds")]
        public void DoesNotExposeDuplicateMonitoringProperties(string propertyName)
        {
            Assert.Null(typeof(ProcessMonitorConfiguration).GetProperty(propertyName));
        }
    }
}
