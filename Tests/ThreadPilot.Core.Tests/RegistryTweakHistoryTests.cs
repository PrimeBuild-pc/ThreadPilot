namespace ThreadPilot.Core.Tests;

using Microsoft.Win32;
using ThreadPilot.Services;

public sealed class RegistryTweakHistoryTests
{
    [Fact]
    public void RestoreRequiresTheSameValueAndRegistryTypeThreadPilotWrote()
    {
        var original = new RegistryTweakHistory.RegistryValue("1", RegistryValueKind.DWord);
        var written = new RegistryTweakHistory.RegistryValue("2", RegistryValueKind.DWord);
        Assert.True(RegistryTweakHistory.ShouldRestore(written, original, written));
        Assert.False(RegistryTweakHistory.ShouldRestore(original, original, written));
        Assert.False(RegistryTweakHistory.ShouldRestore(new("2", RegistryValueKind.String), original, written));
        Assert.False(RegistryTweakHistory.ShouldRestore(default, original, written));
        Assert.True(RegistryTweakHistory.ShouldRestore(written, default, written));
    }
}
