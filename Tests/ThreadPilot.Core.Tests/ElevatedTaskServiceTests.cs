/*
 * ThreadPilot - scheduled task service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Text;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    /// <summary>
    /// Unit tests for scheduled task orchestration in <see cref="ElevatedTaskService"/>.
    /// </summary>
    public sealed class ElevatedTaskServiceTests
    {
        [Fact]
        public async Task EnsureAutostartTaskAsync_ReturnsFalse_WhenExecutablePathIsInvalid()
        {
            var runner = new RecordingProcessRunner();
            var service = CreateService(runner);

            var result = await service.EnsureAutostartTaskAsync(@"C:\temp\ThreadPilot.txt", "--autostart");

            Assert.False(result);
            Assert.Empty(runner.Invocations);
        }

        [Fact]
        public async Task TryRunLaunchTaskAsync_ReturnsFalse_WhenSchTasksTimesOut()
        {
            var runner = new RecordingProcessRunner
            {
                ResultFactory = _ => new ProcessRunResult(-1, string.Empty, "schtasks timeout after 20 seconds"),
            };
            var service = CreateService(runner);

            var result = await service.TryRunLaunchTaskAsync();

            Assert.False(result);

            var invocation = Assert.Single(runner.Invocations);
            Assert.Equal(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), invocation.FileName);
            Assert.Equal(TimeSpan.FromSeconds(20), invocation.Timeout);
            Assert.Equal(new[] { "/Run", "/TN", service.LaunchTaskName }, invocation.Arguments);
        }

        [Fact]
        public async Task EnsureLaunchTaskAsync_WritesExpectedLaunchTaskDefinition()
        {
            var executablePath = CreateTemporaryExecutablePath();
            string? xmlPath = null;
            string? xmlContent = null;
            var runner = new RecordingProcessRunner
            {
                ResultFactory = invocation =>
                {
                    var xmlIndex = invocation.Arguments.IndexOf("/XML");
                    Assert.True(xmlIndex >= 0);
                    xmlPath = invocation.Arguments[xmlIndex + 1];
                    xmlContent = File.ReadAllText(xmlPath, Encoding.Unicode);
                    return new ProcessRunResult(0, string.Empty, string.Empty);
                },
            };

            try
            {
                var service = CreateService(
                    runner,
                    executablePathProvider: () => executablePath,
                    currentUserProvider: () => @"TEST\User");

                var result = await service.EnsureLaunchTaskAsync();

                Assert.True(result);
                Assert.NotNull(xmlPath);
                Assert.NotNull(xmlContent);
                Assert.Contains("<UserId>TEST\\User</UserId>", xmlContent, StringComparison.Ordinal);
                Assert.Contains($"<Command>{executablePath}</Command>", xmlContent, StringComparison.Ordinal);
                Assert.Contains("<Arguments>--launched-via-task</Arguments>", xmlContent, StringComparison.Ordinal);
                Assert.Contains(
                    $"<WorkingDirectory>{Path.GetDirectoryName(executablePath)}</WorkingDirectory>",
                    xmlContent,
                    StringComparison.Ordinal);
                Assert.False(File.Exists(xmlPath));
            }
            finally
            {
                if (File.Exists(executablePath))
                {
                    File.Delete(executablePath);
                }
            }
        }

        private static ElevatedTaskService CreateService(
            IProcessRunner runner,
            Func<string?>? executablePathProvider = null,
            Func<string>? currentUserProvider = null)
        {
            return new ElevatedTaskService(
                NullLogger<ElevatedTaskService>.Instance,
                runner,
                executablePathProvider,
                currentUserProvider);
        }

        private static string CreateTemporaryExecutablePath()
        {
            var executablePath = Path.Combine(Path.GetTempPath(), $"threadpilot-test-{Guid.NewGuid():N}.exe");
            File.WriteAllText(executablePath, "stub");
            return executablePath;
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
