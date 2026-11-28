namespace ThreadPilot.Core.Tests;

using System;
using System.Threading.Tasks;
using Moq;
using ThreadPilot.Services;

// Run explicitly with -p:StorePackage=true --filter FullyQualifiedName~StorePackageTests.
public sealed class StorePackageTests
{
    [Fact]
    public async Task StoreInstallerCannotLaunchEvenWithAnInvalidPath()
    {
        Assert.True(StoreDistribution.IsStoreBuild);
        var launcher = new Mock<IUpdateProcessLauncher>(MockBehavior.Strict);
        var service = new UpdateInstallerService(
            new Mock<IUpdateTempDirectoryProvider>(MockBehavior.Strict).Object, launcher.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LaunchInstallerElevatedAsync("missing.exe"));
        launcher.VerifyNoOtherCalls();
    }

    [Fact]
    public void StoreTasksCannotOverwriteTheExeChannelsTasks()
    {
        Assert.True(StoreDistribution.IsStoreBuild);
        var service = new ElevatedTaskService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ElevatedTaskService>.Instance,
            new Mock<ThreadPilot.Services.Abstractions.IProcessRunner>(MockBehavior.Strict).Object);
        Assert.Equal("ThreadPilot_Store_Launch", service.LaunchTaskName);
        Assert.Equal("ThreadPilot_Store_Startup", service.AutostartTaskName);
    }

    [Fact]
    public async Task StoreLogonTaskActivatesThePackageWithoutElevation()
    {
        Assert.True(StoreDistribution.IsStoreBuild);
        var calls = new System.Collections.Generic.List<string[]>();
        var exe = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ThreadPilot-{Guid.NewGuid():N}.exe");
        System.IO.File.WriteAllText(exe, string.Empty);
        try
        {
            var service = CreateTaskService(calls, () => "LorenzoMassafra.ThreadPilot_abc");
            Assert.True(await service.EnsureAutostartTaskAsync(exe, "--autostart"));

            var create = Assert.Single(calls, c => c[0] == "/Create");
            Assert.Equal("LIMITED", create[Array.IndexOf(create, "/RL") + 1]);
            Assert.Equal(
                $"\"{ElevatedTaskService.StoreActivationLauncherPath}\" shell:AppsFolder\\LorenzoMassafra.ThreadPilot_abc!App",
                create[Array.IndexOf(create, "/TR") + 1]);
        }
        finally
        {
            System.IO.File.Delete(exe);
        }
    }

    [Fact]
    public async Task StoreLogonTaskIsNotRegisteredWithoutPackageIdentity()
    {
        Assert.True(StoreDistribution.IsStoreBuild);
        var calls = new System.Collections.Generic.List<string[]>();
        var exe = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ThreadPilot-{Guid.NewGuid():N}.exe");
        System.IO.File.WriteAllText(exe, string.Empty);
        try
        {
            Assert.False(await CreateTaskService(calls, () => null).EnsureAutostartTaskAsync(exe, "--autostart"));
            Assert.Empty(calls);
        }
        finally
        {
            System.IO.File.Delete(exe);
        }
    }

    [Fact]
    public async Task StoreBuildNeverCreatesTheUacFreeLaunchTaskAndKeepsTheActivationTask()
    {
        Assert.True(StoreDistribution.IsStoreBuild);
        var calls = new System.Collections.Generic.List<string[]>();
        var service = CreateTaskService(calls, () => "LorenzoMassafra.ThreadPilot_abc", query: taskName => taskName == "ThreadPilot_Store_Startup"
            ? $"<Task><Actions><Exec><Command>{ElevatedTaskService.StoreActivationLauncherPath}</Command></Exec></Actions></Task>"
            : null);

        Assert.False(await service.EnsureLaunchTaskAsync());

        Assert.DoesNotContain(calls, c => c[0] == "/Create");
        Assert.Contains(calls, c => c[0] == "/Delete" && c[2] == "ThreadPilot_Store_Launch");
        Assert.DoesNotContain(calls, c => c[0] == "/Delete" && c[2] == "ThreadPilot_Store_Startup");
    }

    private static ElevatedTaskService CreateTaskService(
        System.Collections.Generic.List<string[]> calls,
        Func<string?> packageFamilyName,
        Func<string, string?>? query = null)
    {
        var runner = new Mock<ThreadPilot.Services.Abstractions.IProcessRunner>();
        runner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<System.Collections.Generic.IReadOnlyList<string>>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync((string _, System.Collections.Generic.IReadOnlyList<string> args, TimeSpan _) =>
            {
                calls.Add(System.Linq.Enumerable.ToArray(args));
                var xml = args[0] == "/Query" ? query?.Invoke(args[2]) : null;
                return args[0] == "/Query"
                    ? new ThreadPilot.Services.Abstractions.ProcessRunResult(xml == null ? 1 : 0, xml ?? string.Empty, string.Empty)
                    : new ThreadPilot.Services.Abstractions.ProcessRunResult(0, string.Empty, string.Empty);
            });
        return new ElevatedTaskService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ElevatedTaskService>.Instance,
            runner.Object,
            executablePathProvider: null,
            currentUserProvider: () => @"TEST\User",
            isProtectedInstallPath: _ => true,
            packageFamilyNameProvider: packageFamilyName);
    }
}
