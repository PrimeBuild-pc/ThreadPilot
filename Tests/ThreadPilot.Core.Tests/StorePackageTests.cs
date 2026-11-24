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
}
