/*
 * ThreadPilot - Core security unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;

    /// <summary>
    /// Unit tests for security-focused validation behavior in <see cref="PowerPlanService"/>.
    /// </summary>
    public sealed class PowerPlanServiceSecurityTests
    {
        /// <summary>
        /// Ensures relative import paths are rejected.
        /// </summary>
        [Fact]
        public async Task ImportCustomPowerPlan_ReturnsFalse_ForRelativePath()
        {
            var service = CreateService();

            var result = await service.ImportCustomPowerPlan("..\\evil.pow");

            Assert.False(result);
        }

        /// <summary>
        /// Ensures non-.pow files are rejected.
        /// </summary>
        [Fact]
        public async Task ImportCustomPowerPlan_ReturnsFalse_ForInvalidExtension()
        {
            var service = CreateService();
            var filePath = Path.Combine(Path.GetTempPath(), "threadpilot-test.txt");
            await File.WriteAllTextAsync(filePath, "content");

            try
            {
                var result = await service.ImportCustomPowerPlan(filePath);
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        /// <summary>
        /// Ensures invalid GUID values are rejected before invoking powercfg.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("invalid-guid")]
        [InlineData("1234")]
        public async Task SetActivePowerPlanByGuidAsync_ReturnsFalse_ForInvalidGuid(string guid)
        {
            var service = CreateService();

            var result = await service.SetActivePowerPlanByGuidAsync(guid);

            Assert.False(result);
        }

        private static PowerPlanService CreateService()
        {
            var enhancedLogger = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            return new PowerPlanService(NullLogger<PowerPlanService>.Instance, enhancedLogger.Object);
        }
    }
}
