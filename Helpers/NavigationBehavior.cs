namespace ThreadPilot.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using ThreadPilot.ViewModels;

    public sealed class NavigationBehavior : IDisposable
    {
        private readonly SemaphoreSlim navigationGuard = new(1, 1);
        private bool isHandlingNavigation;

        public async Task<bool> TryEnterAsync()
        {
            if (this.isHandlingNavigation)
            {
                return false;
            }

            await this.navigationGuard.WaitAsync().ConfigureAwait(false);
            this.isHandlingNavigation = true;
            return true;
        }

        public void Exit()
        {
            this.isHandlingNavigation = false;
            this.navigationGuard.Release();
        }

        public static async Task<bool> EnsureCanNavigateAsync(
            string targetTag,
            SettingsViewModel settingsViewModel,
            Func<Task<MessageBoxResult>>? showUnsavedSettingsPromptAsync = null)
        {
            ArgumentNullException.ThrowIfNull(targetTag);
            ArgumentNullException.ThrowIfNull(settingsViewModel);

            if (!settingsViewModel.HasPendingChanges || string.Equals(targetTag, "Settings", StringComparison.Ordinal))
            {
                return true;
            }

            var result = showUnsavedSettingsPromptAsync != null
                ? await showUnsavedSettingsPromptAsync().ConfigureAwait(false)
                : MessageBox.Show(
                    "You have unsaved changes in Settings.\n\nChoose an action:\n- Yes: Save changes\n- No: Discard changes\n- Cancel: Stay on current tab",
                    "Unsaved Settings",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

            return result switch
            {
                MessageBoxResult.Cancel => false,
                MessageBoxResult.Yes => await settingsViewModel.SaveIfDirtyAsync().ConfigureAwait(false),
                MessageBoxResult.No => await DiscardPendingChangesAsync(settingsViewModel).ConfigureAwait(false),
                _ => true,
            };
        }

        public void Dispose()
        {
            this.navigationGuard.Dispose();
        }

        private static async Task<bool> DiscardPendingChangesAsync(SettingsViewModel settingsViewModel)
        {
            await settingsViewModel.DiscardPendingChangesAsync().ConfigureAwait(false);
            return true;
        }
    }
}
