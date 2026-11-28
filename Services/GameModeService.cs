namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;

    public class GameModeService : IGameModeService
    {
        private const string DefaultGameBarKeyPath = @"Software\Microsoft\GameBar";
        private const string GameModeValueName = "AutoGameModeEnabled";
        private readonly ILogger<GameModeService> logger;
        private readonly string gameBarKeyPath;

        public GameModeService(ILogger<GameModeService> logger)
            : this(logger, DefaultGameBarKeyPath)
        {
        }

        // Tests point this at a scratch key under HKCU so the real Game Bar settings are never touched.
        internal GameModeService(ILogger<GameModeService> logger, string gameBarKeyPath)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.gameBarKeyPath = string.IsNullOrWhiteSpace(gameBarKeyPath)
                ? throw new ArgumentException("A registry key path is required.", nameof(gameBarKeyPath))
                : gameBarKeyPath;
        }

        public async Task<bool> IsGameModeEnabledAsync()
        {
            await Task.CompletedTask; // Make async for consistency

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(this.gameBarKeyPath, writable: false);
                var value = key?.GetValue(GameModeValueName);
                var isEnabled = IsEnabledFromRegistryValue(value);
                this.logger.LogDebug(
                    "Game Mode status: {Status} ({Source})",
                    isEnabled ? "Enabled" : "Disabled",
                    value == null ? "value not set, Windows default" : "registry value");
                return isEnabled;
            }
            catch (Exception ex)
            {
                // Unreadable is not the same as off: report the Windows default rather than a false "disabled".
                this.logger.LogWarning(ex, "Failed to read Game Mode registry key, assuming the Windows default (enabled)");
                return true;
            }
        }

        public async Task<bool> SetGameModeAsync(bool enabled)
        {
            await Task.CompletedTask; // Make async for consistency

            try
            {
                if (this.gameBarKeyPath == DefaultGameBarKeyPath)
                {
                    RegistryTweakHistory.Write(Registry.CurrentUser, this.gameBarKeyPath, GameModeValueName,
                        enabled ? 1 : 0, RegistryValueKind.DWord);
                }
                else
                {
                    using var key = Registry.CurrentUser.CreateSubKey(this.gameBarKeyPath, writable: true);
                    key.SetValue(GameModeValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
                }

                this.logger.LogInformation("Set Windows Game Mode to {State}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                this.logger.LogWarning(ex, "Insufficient permissions to modify Game Mode registry key");
                return false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to set Game Mode to {State}", enabled ? "enabled" : "disabled");
                return false;
            }
        }

        /// <summary>
        /// Windows 10 1703+ and Windows 11 turn Game Mode on by default and only write AutoGameModeEnabled
        /// once the user changes the switch, so a missing key or value means "on"; only an explicit 0 means "off".
        /// </summary>
        internal static bool IsEnabledFromRegistryValue(object? value) => value switch
        {
            int dword => dword != 0,
            long qword => qword != 0,
            _ => true,
        };
    }
}
