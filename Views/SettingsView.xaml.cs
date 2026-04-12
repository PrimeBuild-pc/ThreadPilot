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
using System.Windows;
using System.Windows.Controls;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
        }

        public SettingsView(SettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(SettingsView_LoadedAsync(), _ =>
            {
                // Non-critical load refresh failures are handled by the view model.
            });
        }

        private async Task SettingsView_LoadedAsync()
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RefreshSettingsAsync();
            }
        }
    }
}

