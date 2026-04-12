/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using System.Windows.Interop;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for managing global keyboard shortcuts using Windows API.
    /// </summary>
    public class KeyboardShortcutService : IKeyboardShortcutService, IDisposable
    {
        private readonly ILogger<KeyboardShortcutService> logger;
        private readonly IApplicationSettingsService settingsService;
        private readonly Dictionary<string, KeyboardShortcut> registeredShortcuts = new();
        private readonly Dictionary<int, string> hotkeyIdToAction = new();
        private int nextHotkeyId = 1;
        private IntPtr windowHandle = IntPtr.Zero;
        private HwndSource? hwndSource;
        private bool disposed;

        // Windows API constants
        private const int WMHOTKEY = 0x0312;
        private const int MODALT = 0x0001;
        private const int MODCONTROL = 0x0002;
        private const int MODSHIFT = 0x0004;
        private const int MODWIN = 0x0008;

        // Windows API functions
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public event EventHandler<ShortcutActivatedEventArgs>? ShortcutActivated;

        public KeyboardShortcutService(
            ILogger<KeyboardShortcutService> logger,
            IApplicationSettingsService settingsService)
        {
            this.logger = logger;
            this.settingsService = settingsService;
        }

        public async Task<bool> RegisterShortcutAsync(string actionName, Key key, ModifierKeys modifiers)
        {
            try
            {
                if (string.IsNullOrEmpty(actionName))
                {
                    return false;
                }

                // Check if shortcut is already registered
                if (await this.IsShortcutRegisteredAsync(key, modifiers))
                {
                    this.logger.LogWarning("Shortcut {Key}+{Modifiers} is already registered", key, modifiers);
                    return false;
                }

                // Unregister existing shortcut for this action if it exists
                if (this.registeredShortcuts.ContainsKey(actionName))
                {
                    await this.UnregisterShortcutAsync(actionName);
                }

                var shortcut = new KeyboardShortcut
                {
                    ActionName = actionName,
                    Key = key,
                    Modifiers = modifiers,
                    Description = this.GetActionDescription(actionName),
                    IsEnabled = true,
                    IsGlobal = true,
                };

                // Register with Windows API
                var hotkeyId = this.nextHotkeyId++;
                var winModifiers = this.ConvertToWinModifiers(modifiers);
                var virtualKey = KeyInterop.VirtualKeyFromKey(key);

                if (RegisterHotKey(this.windowHandle, hotkeyId, winModifiers, (uint)virtualKey))
                {
                    this.registeredShortcuts[actionName] = shortcut;
                    this.hotkeyIdToAction[hotkeyId] = actionName;

                    this.logger.LogInformation(
                        "Registered global shortcut {Shortcut} for action {Action}",
                        shortcut.ToString(), actionName);
                    return true;
                }
                else
                {
                    this.logger.LogError(
                        "Failed to register global shortcut {Shortcut} for action {Action}",
                        shortcut.ToString(), actionName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error registering shortcut for action {Action}", actionName);
                return false;
            }
        }

        public async Task<bool> UnregisterShortcutAsync(string actionName)
        {
            try
            {
                if (!this.registeredShortcuts.TryGetValue(actionName, out var shortcut))
                {
                    return false;
                }

                // Find the hotkey ID
                var hotkeyId = this.hotkeyIdToAction.FirstOrDefault(kvp => kvp.Value == actionName).Key;
                if (hotkeyId == 0)
                {
                    return false;
                }

                // Unregister from Windows API
                if (UnregisterHotKey(this.windowHandle, hotkeyId))
                {
                    this.registeredShortcuts.Remove(actionName);
                    this.hotkeyIdToAction.Remove(hotkeyId);

                    this.logger.LogInformation(
                        "Unregistered shortcut {Shortcut} for action {Action}",
                        shortcut.ToString(), actionName);
                    return true;
                }
                else
                {
                    this.logger.LogError(
                        "Failed to unregister shortcut {Shortcut} for action {Action}",
                        shortcut.ToString(), actionName);
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error unregistering shortcut for action {Action}", actionName);
                return false;
            }
        }

        public async Task<bool> UpdateShortcutAsync(string actionName, Key key, ModifierKeys modifiers)
        {
            // Unregister existing shortcut and register new one
            await this.UnregisterShortcutAsync(actionName);
            return await this.RegisterShortcutAsync(actionName, key, modifiers);
        }

        public async Task<Dictionary<string, KeyboardShortcut>> GetRegisteredShortcutsAsync()
        {
            return new Dictionary<string, KeyboardShortcut>(this.registeredShortcuts);
        }

        public async Task<bool> IsShortcutRegisteredAsync(Key key, ModifierKeys modifiers)
        {
            return this.registeredShortcuts.Values.Any(s => s.Key == key && s.Modifiers == modifiers);
        }

        public async Task LoadShortcutsFromSettingsAsync()
        {
            try
            {
                var settings = this.settingsService.Settings;
                if (settings.KeyboardShortcuts != null)
                {
                    foreach (var shortcutSetting in settings.KeyboardShortcuts)
                    {
                        if (shortcutSetting.IsEnabled)
                        {
                            await this.RegisterShortcutAsync(shortcutSetting.ActionName, shortcutSetting.Key, shortcutSetting.Modifiers);
                        }
                    }
                }
                else
                {
                    // Load default shortcuts if none are configured
                    await this.LoadDefaultShortcutsAsync();
                }

                this.logger.LogInformation("Loaded {Count} keyboard shortcuts from settings", this.registeredShortcuts.Count);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error loading shortcuts from settings");
            }
        }

        public async Task SaveShortcutsToSettingsAsync()
        {
            try
            {
                var settings = this.settingsService.Settings;
                settings.KeyboardShortcuts = this.registeredShortcuts.Values.ToList();
                await this.settingsService.UpdateSettingsAsync(settings);

                this.logger.LogInformation("Saved {Count} keyboard shortcuts to settings", this.registeredShortcuts.Count);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error saving shortcuts to settings");
            }
        }

        public async Task ClearAllShortcutsAsync()
        {
            var actions = this.registeredShortcuts.Keys.ToList();
            foreach (var action in actions)
            {
                await this.UnregisterShortcutAsync(action);
            }
        }

        public Dictionary<string, KeyboardShortcut> GetDefaultShortcuts()
        {
            return new Dictionary<string, KeyboardShortcut>
            {
                [ShortcutActions.ShowMainWindow] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.ShowMainWindow,
                    Key = Key.T,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Show/Hide main window",
                    IsEnabled = true,
                    IsGlobal = true,
                },
                [ShortcutActions.ToggleMonitoring] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.ToggleMonitoring,
                    Key = Key.M,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Toggle process monitoring",
                    IsEnabled = true,
                    IsGlobal = true,
                },
                [ShortcutActions.PowerPlanHighPerformance] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.PowerPlanHighPerformance,
                    Key = Key.H,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Switch to High Performance power plan",
                    IsEnabled = true,
                    IsGlobal = true,
                },
                [ShortcutActions.OpenTweaks] = new KeyboardShortcut
                {
                    ActionName = ShortcutActions.OpenTweaks,
                    Key = Key.W,
                    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Description = "Open System Tweaks tab",
                    IsEnabled = true,
                    IsGlobal = true
                },
            };
        }

        public void SetWindowHandle(IntPtr windowHandle)
        {
            this.windowHandle = windowHandle;

            // Set up message hook for hotkey messages
            if (this.windowHandle != nint.Zero)
            {
                this.hwndSource = HwndSource.FromHwnd(this.windowHandle);
                if (this.hwndSource != null)
                {
                    this.hwndSource.AddHook(this.WndProc);
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WMHOTKEY)
            {
                var hotkeyId = wParam.ToInt32();
                if (this.hotkeyIdToAction.TryGetValue(hotkeyId, out var actionName) &&
                    this.registeredShortcuts.TryGetValue(actionName, out var shortcut))
                {
                    this.ShortcutActivated?.Invoke(this, new ShortcutActivatedEventArgs(actionName, shortcut));
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private async Task LoadDefaultShortcutsAsync()
        {
            var defaultShortcuts = this.GetDefaultShortcuts();
            foreach (var shortcut in defaultShortcuts.Values)
            {
                await this.RegisterShortcutAsync(shortcut.ActionName, shortcut.Key, shortcut.Modifiers);
            }
        }

        private uint ConvertToWinModifiers(ModifierKeys modifiers)
        {
            uint winModifiers = 0;

            if (modifiers.HasFlag(ModifierKeys.Alt))
            {
                winModifiers |= MODALT;
            }

            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                winModifiers |= MODCONTROL;
            }

            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                winModifiers |= MODSHIFT;
            }

            if (modifiers.HasFlag(ModifierKeys.Windows))
            {
                winModifiers |= MODWIN;
            }

            return winModifiers;
        }

        private string GetActionDescription(string actionName)
        {
            return actionName switch
            {
                ShortcutActions.QuickApply => "Quick apply current settings",
                ShortcutActions.ToggleMonitoring => "Toggle process monitoring",
                ShortcutActions.ShowMainWindow => "Show/Hide main window",
                ShortcutActions.HideToTray => "Hide to system tray",
                ShortcutActions.PowerPlanBalanced => "Switch to Balanced power plan",
                ShortcutActions.PowerPlanHighPerformance => "Switch to High Performance power plan",
                ShortcutActions.PowerPlanPowerSaver => "Switch to Power Saver power plan",
                ShortcutActions.RefreshProcessList => "Refresh process list",
                ShortcutActions.OpenSettings => "Open Settings tab",
                ShortcutActions.OpenTweaks => "Open System Tweaks tab",
                ShortcutActions.ExitApplication => "Exit application",
                _ => actionName,
            };
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.ClearAllShortcutsAsync().Wait();

                if (this.hwndSource != null)
                {
                    this.hwndSource.RemoveHook(this.WndProc);
                    this.hwndSource = null;
                }

                this.disposed = true;
            }
        }
    }
}

