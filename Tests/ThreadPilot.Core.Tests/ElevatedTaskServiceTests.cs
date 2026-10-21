/*
 * ThreadPilot - scheduled task service unit tests.
 */
namespace ThreadPilot.Core.Tests
{
    using System.Text;
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

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
                    if (invocation.Arguments[0] != "/Create")
                    {
                        return new ProcessRunResult(1, string.Empty, "not found");
                    }

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
                    currentUserProvider: () => @"TEST\User",
                    isProtectedInstallPath: _ => true);

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

        [Theory]
        [InlineData(@"{PF}\ThreadPilot\ThreadPilot.exe", true)]
        [InlineData(@"{PF}\..\Users\Public\ThreadPilot.exe", false)]
        [InlineData(@"{PF} Evil\ThreadPilot.exe", false)]
        [InlineData(@"C:\Users\Someone\Downloads\ThreadPilot\ThreadPilot.exe", false)]
        [InlineData(@"ThreadPilot.exe", false)]
        [InlineData(@"\ThreadPilot.exe", false)]
        [InlineData("", false)]
        public void IsProtectedInstallPath_OnlyAcceptsProgramFiles(string path, bool expected)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            Assert.Equal(expected, ElevatedTaskService.IsProtectedInstallPath(path.Replace("{PF}", programFiles)));
        }

        [Fact]
        public async Task EnsureLaunchTaskAsync_FromAWritableFolder_DeletesStaleTasksAndRegistersNothing()
        {
            var executablePath = CreateTemporaryExecutablePath();
            var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ThreadPilot", "ThreadPilot.exe");
            var runner = new RecordingProcessRunner
            {
                ResultFactory = invocation => invocation.Arguments switch
                {
                    ["/Query", "/TN", "ThreadPilot_Launch", "/XML"] => TaskXml(@"C:\Users\Someone\Documents\ThreadPilot.exe"),
                    ["/Query", "/TN", "ThreadPilot_Startup", "/XML"] => TaskXml($"\"{installed}\""),
                    _ => new ProcessRunResult(0, string.Empty, string.Empty),
                },
            };

            try
            {
                var service = CreateService(runner, executablePathProvider: () => executablePath);

                var result = await service.EnsureLaunchTaskAsync();

                Assert.False(result);
                Assert.Contains(runner.Invocations, invocation => invocation.Arguments.SequenceEqual(new[] { "/Delete", "/TN", "ThreadPilot_Launch", "/F" }));
                Assert.DoesNotContain(runner.Invocations, invocation => invocation.Arguments.Contains("ThreadPilot_Startup") && invocation.Arguments[0] == "/Delete");
                Assert.DoesNotContain(runner.Invocations, invocation => invocation.Arguments[0] == "/Create");
            }
            finally
            {
                File.Delete(executablePath);
            }
        }

        [Fact]
        public async Task EnsureLaunchTaskAsync_KeepsATaskInProgramFiles_AndParsesQuotedEscapedCommands()
        {
            var executablePath = CreateTemporaryExecutablePath();
            var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tools & Co", "ThreadPilot.exe");
            var runner = new RecordingProcessRunner
            {
                ResultFactory = invocation => invocation.Arguments[0] == "/Query"
                    ? TaskXml($"\"{installed.Replace("&", "&amp;")}\"")
                    : new ProcessRunResult(0, string.Empty, string.Empty),
            };

            try
            {
                var service = CreateService(runner, executablePathProvider: () => executablePath);

                await service.EnsureLaunchTaskAsync();

                Assert.DoesNotContain(runner.Invocations, invocation => invocation.Arguments[0] == "/Delete");
            }
            finally
            {
                File.Delete(executablePath);
            }
        }

        [Fact]
        public async Task EnsureAutostartTaskAsync_FromAWritableFolder_RegistersNothing()
        {
            var executablePath = CreateTemporaryExecutablePath();
            var runner = new RecordingProcessRunner();

            try
            {
                var service = CreateService(runner);

                Assert.False(await service.EnsureAutostartTaskAsync(executablePath, "--autostart"));
                Assert.Empty(runner.Invocations);
            }
            finally
            {
                File.Delete(executablePath);
            }
        }

        private static ProcessRunResult TaskXml(string command) => new(
            0,
            $"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\r\n<Task><Actions><Exec><Command>{command}</Command></Exec></Actions></Task>",
            string.Empty);

        private static ElevatedTaskService CreateService(
            IProcessRunner runner,
            Func<string?>? executablePathProvider = null,
            Func<string>? currentUserProvider = null,
            Func<string?, bool>? isProtectedInstallPath = null)
        {
            return new ElevatedTaskService(
                NullLogger<ElevatedTaskService>.Instance,
                runner,
                executablePathProvider,
                currentUserProvider,
                isProtectedInstallPath);
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
