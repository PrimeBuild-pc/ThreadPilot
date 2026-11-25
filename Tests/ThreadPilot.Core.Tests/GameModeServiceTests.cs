namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Win32;
    using ThreadPilot.Services;

    /// <summary>
    /// Runs against a throw-away key under HKCU\Software\ThreadPilot.Tests, never the real Game Bar settings.
    /// </summary>
    public sealed class GameModeServiceTests : IDisposable
    {
        private const string ValueName = "AutoGameModeEnabled";
        private const string ParentPath = @"Software\ThreadPilot.Tests";
        private readonly string keyPath = $@"{ParentPath}\GameBar-{Guid.NewGuid():N}";

        public void Dispose()
        {
            Registry.CurrentUser.DeleteSubKeyTree(this.keyPath, throwOnMissingSubKey: false);
            using var parent = Registry.CurrentUser.OpenSubKey(ParentPath);
            if (parent != null && parent.SubKeyCount == 0 && parent.ValueCount == 0)
            {
                parent.Dispose();
                Registry.CurrentUser.DeleteSubKey(ParentPath, throwOnMissingSubKey: false);
            }
        }

        [Fact]
        public async Task MissingKey_IsTheWindowsDefault_Enabled()
        {
            Assert.True(await this.CreateService().IsGameModeEnabledAsync());
        }

        [Fact]
        public async Task KeyWithoutValue_IsTheWindowsDefault_Enabled()
        {
            Registry.CurrentUser.CreateSubKey(this.keyPath)!.Dispose();

            Assert.True(await this.CreateService().IsGameModeEnabledAsync());
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public async Task ExplicitValue_IsReadAsIs(int value, bool expected)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(this.keyPath)!)
            {
                key.SetValue(ValueName, value, RegistryValueKind.DWord);
            }

            Assert.Equal(expected, await this.CreateService().IsGameModeEnabledAsync());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Set_ThenRead_RoundTrips(bool enabled)
        {
            var service = this.CreateService();

            Assert.True(await service.SetGameModeAsync(enabled));

            Assert.Equal(enabled, await service.IsGameModeEnabledAsync());
            using var key = Registry.CurrentUser.OpenSubKey(this.keyPath)!;
            Assert.Equal(enabled ? 1 : 0, key.GetValue(ValueName));
        }

        [Fact]
        public void UnexpectedValueType_FallsBackToTheWindowsDefault()
        {
            Assert.True(GameModeService.IsEnabledFromRegistryValue("0"));
            Assert.True(GameModeService.IsEnabledFromRegistryValue(null));
            Assert.False(GameModeService.IsEnabledFromRegistryValue(0L));
        }

        private GameModeService CreateService() => new(NullLogger<GameModeService>.Instance, this.keyPath);
    }
}
