namespace ThreadPilot.ViewModels
{
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using ThreadPilot.Services;

    public partial class SettingsViewModel
    {
        private readonly IKeyboardShortcutService? keyboardShortcutService;
        private bool isEditingShortcutRows;

        [ObservableProperty]
        private IReadOnlyList<ShortcutSettingsRow> shortcutRows = [];

        private void RefreshShortcutRows()
        {
            if (this.keyboardShortcutService == null)
            {
                return;
            }

            var defaults = this.keyboardShortcutService.GetDefaultShortcuts().Values;
            var configured = this.Settings.KeyboardShortcuts;
            var shortcuts = configured.Count == 0
                ? defaults
                : configured.Concat(defaults.Where(fallback => configured.All(s => s.ActionName != fallback.ActionName))
                    .Select(fallback =>
                    {
                        fallback.IsEnabled = false;
                        return fallback;
                    }));

            this.ShortcutRows = shortcuts.Select(shortcut => new ShortcutSettingsRow(shortcut, this.OnShortcutRowChanged)).ToArray();
        }

        private void OnShortcutRowChanged()
        {
            this.isEditingShortcutRows = true;
            try
            {
                this.Settings.KeyboardShortcuts = this.ShortcutRows.Select(row => row.ToShortcut()).ToList();
            }
            finally
            {
                this.isEditingShortcutRows = false;
            }
        }
    }

    public sealed class ShortcutSettingsRow : ObservableObject
    {
        private static readonly Key[] CommonKeys =
        [
            .. Enumerable.Range((int)Key.A, 26).Select(value => (Key)value),
            .. Enumerable.Range((int)Key.D0, 10).Select(value => (Key)value),
            .. Enumerable.Range((int)Key.F1, 24).Select(value => (Key)value),
        ];

        private readonly KeyboardShortcut shortcut;
        private readonly Action onChanged;

        public ShortcutSettingsRow(KeyboardShortcut shortcut, Action onChanged)
        {
            this.shortcut = shortcut.Clone();
            this.onChanged = onChanged;
            this.Keys = CommonKeys.Contains(shortcut.Key) ? CommonKeys : [shortcut.Key, .. CommonKeys];
        }

        public string ActionName => this.shortcut.ActionName;

        public string Description => this.shortcut.Description;

        public IReadOnlyList<Key> Keys { get; }

        public bool IsEnabled
        {
            get
            {
                return this.shortcut.IsEnabled;
            }

            set
            {
                if (this.SetProperty(this.shortcut.IsEnabled, value, this.shortcut, (model, enabled) => model.IsEnabled = enabled))
                {
                    this.onChanged();
                }
            }
        }

        public Key Key
        {
            get
            {
                return this.shortcut.Key;
            }

            set
            {
                if (this.SetProperty(this.shortcut.Key, value, this.shortcut, (model, key) => model.Key = key))
                {
                    this.onChanged();
                }
            }
        }

        public bool Control
        {
            get => this.shortcut.Modifiers.HasFlag(ModifierKeys.Control);
            set => this.SetModifier(ModifierKeys.Control, value);
        }

        public bool Alt
        {
            get => this.shortcut.Modifiers.HasFlag(ModifierKeys.Alt);
            set => this.SetModifier(ModifierKeys.Alt, value);
        }

        public bool Shift
        {
            get => this.shortcut.Modifiers.HasFlag(ModifierKeys.Shift);
            set => this.SetModifier(ModifierKeys.Shift, value);
        }

        public bool Windows
        {
            get => this.shortcut.Modifiers.HasFlag(ModifierKeys.Windows);
            set => this.SetModifier(ModifierKeys.Windows, value);
        }

        internal KeyboardShortcut ToShortcut() => this.shortcut.Clone();

        private void SetModifier(ModifierKeys flag, bool enabled, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            var modifiers = enabled ? this.shortcut.Modifiers | flag : this.shortcut.Modifiers & ~flag;
            if (this.SetProperty(this.shortcut.Modifiers, modifiers, this.shortcut, (model, value) => model.Modifiers = value, propertyName))
            {
                this.onChanged();
            }
        }
    }
}
