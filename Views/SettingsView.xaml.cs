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
    using System.Windows;
    using System.Windows.Controls;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    /// <summary>
    /// Interaction logic for SettingsView.xaml.
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            this.InitializeComponent();
            this.Loaded += this.SettingsView_Loaded;
        }

        public SettingsView(SettingsViewModel viewModel)
            : this()
        {
            this.DataContext = viewModel;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.SettingsView_LoadedAsync(), _ =>
            {
                // Non-critical load refresh failures are handled by the view model.
            });
        }

        private async Task SettingsView_LoadedAsync()
        {
            if (this.DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RefreshSettingsAsync();
            }
        }
    }
}

