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
namespace ThreadPilot.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using ThreadPilot.ViewModels;

    /// <summary>
    /// Coordinates serialized navigation transitions and unsaved-changes policy.
    /// </summary>
    public sealed class NavigationBehavior : IDisposable
    {
        private readonly SemaphoreSlim navigationGuard = new(1, 1);
        private bool isHandlingNavigation;

        /// <summary>
        /// Tries entering the navigation critical section.
        /// </summary>
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

        /// <summary>
        /// Leaves the navigation critical section.
        /// </summary>
        public void Exit()
        {
            this.isHandlingNavigation = false;
            this.navigationGuard.Release();
        }

        /// <summary>
        /// Applies unsaved-settings policy before navigating away from the settings section.
        /// </summary>
        public static async Task<bool> EnsureCanNavigateAsync(string targetTag, SettingsViewModel settingsViewModel)
        {
            ArgumentNullException.ThrowIfNull(targetTag);
            ArgumentNullException.ThrowIfNull(settingsViewModel);

            if (!settingsViewModel.HasPendingChanges || string.Equals(targetTag, "Settings", StringComparison.Ordinal))
            {
                return true;
            }

            var result = MessageBox.Show(
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
