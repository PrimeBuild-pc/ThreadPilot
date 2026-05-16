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
namespace ThreadPilot.Views
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using ThreadPilot.ViewModels;

    /// <summary>
    /// Interaction logic for SettingsWindow.xaml.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel viewModel;
        private bool isClosingAfterUnsavedPrompt;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            this.InitializeComponent();

            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.SettingsViewControl.DataContext = this.viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Check for unsaved changes
            if (!this.isClosingAfterUnsavedPrompt && !this.viewModel.CanClose())
            {
                e.Cancel = true;
                this.UnsavedSettingsOverlay.Visibility = Visibility.Visible;
                return;
            }

            base.OnClosing(e);
        }

        private async void UnsavedSettingsSave_Click(object sender, RoutedEventArgs e)
        {
            var saved = await this.viewModel.SaveIfDirtyAsync();
            if (saved)
            {
                this.CloseAfterUnsavedPrompt();
            }
        }

        private async void UnsavedSettingsDiscard_Click(object sender, RoutedEventArgs e)
        {
            await this.viewModel.DiscardPendingChangesAsync();
            this.CloseAfterUnsavedPrompt();
        }

        private void UnsavedSettingsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.UnsavedSettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseAfterUnsavedPrompt()
        {
            this.isClosingAfterUnsavedPrompt = true;
            this.UnsavedSettingsOverlay.Visibility = Visibility.Collapsed;
            this.Close();
        }
    }
}

