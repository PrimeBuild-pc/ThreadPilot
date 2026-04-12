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
    using System.Windows.Controls;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    /// <summary>
    /// Interaction logic for SystemTweaksView.xaml.
    /// </summary>
    public partial class SystemTweaksView : System.Windows.Controls.UserControl
    {
        public SystemTweaksView()
        {
            this.InitializeComponent();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            TaskSafety.FireAndForget(this.UserControl_LoadedAsync(), _ =>
            {
                // Ignore non-fatal loading errors to keep the view responsive.
            });
        }

        private async Task UserControl_LoadedAsync()
        {
            if (this.DataContext is SystemTweaksViewModel viewModel)
            {
                await viewModel.LoadCommand.ExecuteAsync(null);
            }
        }
    }
}

