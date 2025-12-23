/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows Game Mode settings
    /// Windows Game Mode can interfere with CPU Sets and affinity, particularly on AMD systems
    /// Reference: CPU Set Setter warning system
    /// </summary>
    public class GameModeService : IGameModeService
    {
        private readonly ILogger<GameModeService> _logger;
        private const string GameBarKeyPath = @"Software\Microsoft\GameBar";
        private const string GameModeValueName = "AutoGameModeEnabled";

        public GameModeService(ILogger<GameModeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<bool> IsGameModeEnabledAsync()
        {
            await Task.CompletedTask; // Make async for consistency

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(GameBarKeyPath, writable: false);
                if (key == null)
                {
                    _logger.LogDebug("GameBar registry key not found, assuming Game Mode is disabled");
                    return false;
                }

                var value = key.GetValue(GameModeValueName);
                if (value is int intValue)
                {
                    bool isEnabled = intValue != 0;
                    _logger.LogDebug("Game Mode status: {Status}", isEnabled ? "Enabled" : "Disabled");
                    return isEnabled;
                }

                _logger.LogDebug("GameMode value not found, assuming disabled");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Game Mode registry key, assuming disabled");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SetGameModeAsync(bool enabled)
        {
            await Task.CompletedTask; // Make async for consistency

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(GameBarKeyPath, writable: true);
                if (key == null)
                {
                    _logger.LogWarning("GameBar registry key not found, cannot modify Game Mode");
                    return false;
                }

                key.SetValue(GameModeValueName, enabled ? 1 : 0, RegistryValueKind.DWord);
                _logger.LogInformation("Set Windows Game Mode to {State}", enabled ? "enabled" : "disabled");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Insufficient permissions to modify Game Mode registry key");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set Game Mode to {State}", enabled ? "enabled" : "disabled");
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DisableGameModeForAffinityAsync()
        {
            try
            {
                bool isEnabled = await IsGameModeEnabledAsync();
                if (!isEnabled)
                {
                    _logger.LogDebug("Game Mode already disabled, no action needed");
                    return false;
                }

                _logger.LogInformation("Game Mode is enabled, disabling for better CPU affinity control");
                bool success = await SetGameModeAsync(false);

                if (success)
                {
                    _logger.LogInformation("Successfully disabled Windows Game Mode for CPU affinity optimization");
                }
                else
                {
                    _logger.LogWarning("Failed to disable Game Mode, CPU affinity may be affected");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling Game Mode for affinity");
                return false;
            }
        }
    }
}

