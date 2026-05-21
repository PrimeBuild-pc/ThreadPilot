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
    using System.Windows.Input;
    using ThreadPilot.Helpers;
    using ThreadPilot.ViewModels;

    public partial class ProcessView : System.Windows.Controls.UserControl
    {
        public ProcessView()
        {
            this.InitializeComponent();
            this.DataContext = ServiceProviderExtensions.GetService<ProcessViewModel>();
        }

        private void ProcessRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            row.IsSelected = true;
            row.Focus();
        }
    }
}
