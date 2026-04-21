/*
 * ThreadPilot - power plan service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    /// <summary>
    /// Unit tests for deterministic behavior in <see cref="PowerPlanService"/>.
    /// </summary>
    public sealed class PowerPlanServiceTests
    {
        [Fact]
        public async Task GetActivePowerPlan_ParsesPowerCfgOutput()
        {
            const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";
            var runner = new RecordingProcessRunner
            {
                ResultFactory = _ => new ProcessRunResult(
                    0,
                    $"Power Scheme GUID: {guid}  (Balanced)",
                    string.Empty),
            };
            var service = CreateService(runner);

            var result = await service.GetActivePowerPlan();

            Assert.NotNull(result);
            Assert.Equal(guid, result.Guid);
            Assert.Equal("Balanced", result.Name);
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task SetActivePowerPlanByGuidAsync_SkipsChange_WhenAlreadyActive()
        {
            const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";
            var runner = new RecordingProcessRunner
            {
                ResultFactory = _ => new ProcessRunResult(
                    0,
                    $"Power Scheme GUID: {guid}  (Balanced)",
                    string.Empty),
            };
            var service = CreateService(runner);

            var result = await service.SetActivePowerPlanByGuidAsync(guid, preventDuplicateChanges: true);

            Assert.True(result);

            var invocation = Assert.Single(runner.Invocations);
            Assert.Equal(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), invocation.FileName);
            Assert.Equal(new[] { "/getactivescheme" }, invocation.Arguments);
        }

        [Fact]
        public async Task AddCustomPowerPlanFileAsync_RenamesOnCollision()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), $"threadpilot-powerplans-{Guid.NewGuid():N}");
            var managedDirectory = Path.Combine(tempRoot, "managed");
            var sourceDirectory = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(managedDirectory);
            Directory.CreateDirectory(sourceDirectory);

            var sourcePath = Path.Combine(sourceDirectory, "gaming.pow");
            var existingPath = Path.Combine(managedDirectory, "gaming.pow");
            await File.WriteAllTextAsync(sourcePath, "source");
            await File.WriteAllTextAsync(existingPath, "existing");

            try
            {
                var service = CreateService(
                    new RecordingProcessRunner(),
                    powerPlansPathProvider: () => managedDirectory);

                var result = await service.AddCustomPowerPlanFileAsync(sourcePath);

                Assert.True(result);
                var renamedPath = Path.Combine(managedDirectory, "gaming_1.pow");
                Assert.True(File.Exists(renamedPath));
                Assert.Equal("source", await File.ReadAllTextAsync(renamedPath));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        private static PowerPlanService CreateService(
            IProcessRunner runner,
            Func<string>? powerPlansPathProvider = null)
        {
            var enhancedLogger = new Mock<IEnhancedLoggingService>(MockBehavior.Loose);
            return new PowerPlanService(
                NullLogger<PowerPlanService>.Instance,
                enhancedLogger.Object,
                runner,
                powerPlansPathProvider);
        }

        private sealed class RecordingProcessRunner : IProcessRunner
        {
            public List<ProcessInvocation> Invocations { get; } = new();

            public Func<ProcessInvocation, ProcessRunResult>? ResultFactory { get; init; }

            public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
            {
                var invocation = new ProcessInvocation(fileName, arguments.ToList(), timeout);
                this.Invocations.Add(invocation);
                return Task.FromResult(this.ResultFactory?.Invoke(invocation) ?? new ProcessRunResult(0, string.Empty, string.Empty));
            }
        }

        private sealed record ProcessInvocation(string FileName, List<string> Arguments, TimeSpan Timeout);
    }
}
