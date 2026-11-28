namespace ThreadPilot.Core.Tests;

using System.IO;
using System.Text;
using ThreadPilot.Services;

public sealed class UninstallCleanupTests
{
    [Theory]
    [InlineData("""{"ProcessName":"test","Priority":32,"ProcessorAffinity":7}""", true)]
    [InlineData("""{"Settings":{},"Rules":[],"CoreMasks":[]}""", false)]
    [InlineData("{broken", false)]
    public void CleanupDistinguishesProcessProfilesFromExports(string json, bool expected)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray());
        Assert.Equal(expected, UninstallCleanup.IsProcessProfile(stream));
    }

    [Theory]
    [InlineData("SETTINGS.JSON", true)]
    [InlineData("settings.json.bak", true)]
    [InlineData(".settings.json.002b3091041f4e929d7d4b35f4b97320.tmp", true)]
    [InlineData(".settings.json.random.tmp", false)]
    [InlineData(".export.json.002b3091041f4e929d7d4b35f4b97320.tmp", false)]
    [InlineData("device_tweak_backups.json.bak", false)]
    public void CleanupRecognizesOnlyTheDataFileAndItsAtomicArtifacts(string name, bool expected)
    {
        Assert.Equal(expected, UninstallCleanup.MatchesDataArtifact(name,
            candidate => candidate.Equals("settings.json", StringComparison.OrdinalIgnoreCase)));
    }
}
