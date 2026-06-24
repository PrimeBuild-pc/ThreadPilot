namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows.Input;

    public interface IKeyboardShortcutService
    {
        event EventHandler<ShortcutActivatedEventArgs>? ShortcutActivated;

        Task<bool> RegisterShortcutAsync(string actionName, Key key, ModifierKeys modifiers);

        Task<bool> UnregisterShortcutAsync(string actionName);

        Task<bool> UpdateShortcutAsync(string actionName, Key key, ModifierKeys modifiers);

        Task<Dictionary<string, KeyboardShortcut>> GetRegisteredShortcutsAsync();

        Task<bool> IsShortcutRegisteredAsync(Key key, ModifierKeys modifiers);

        Task LoadShortcutsFromSettingsAsync();

        Task SaveShortcutsToSettingsAsync();

        Task ClearAllShortcutsAsync();

        Dictionary<string, KeyboardShortcut> GetDefaultShortcuts();
    }

    public class KeyboardShortcut
    {
        public string ActionName { get; set; } = string.Empty;

        public Key Key { get; set; }

        public ModifierKeys Modifiers { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public bool IsGlobal { get; set; } = true;

        public override string ToString()
        {
            var parts = new List<string>();

            if (this.Modifiers.HasFlag(ModifierKeys.Control))
            {
                parts.Add("Ctrl");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                parts.Add("Alt");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                parts.Add("Shift");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(this.Key.ToString());

            return string.Join(" + ", parts);
        }
    }

    public class ShortcutActivatedEventArgs : EventArgs
    {
        public string ActionName { get; }

        public KeyboardShortcut Shortcut { get; }

        public DateTime ActivationTime { get; }

        public ShortcutActivatedEventArgs(string actionName, KeyboardShortcut shortcut)
        {
            this.ActionName = actionName;
            this.Shortcut = shortcut;
            this.ActivationTime = DateTime.UtcNow;
        }
    }

    public static class ShortcutActions
    {
        public const string QuickApply = "QuickApply";
        public const string ToggleMonitoring = "ToggleMonitoring";
        public const string ShowMainWindow = "ShowMainWindow";
        public const string HideToTray = "HideToTray";
        public const string PowerPlanBalanced = "PowerPlanBalanced";
        public const string PowerPlanHighPerformance = "PowerPlanHighPerformance";
        public const string PowerPlanPowerSaver = "PowerPlanPowerSaver";
        public const string RefreshProcessList = "RefreshProcessList";
        public const string OpenSettings = "OpenSettings";
        public const string OpenTweaks = "OpenTweaks";
        public const string ExitApplication = "ExitApplication";
    }
}

