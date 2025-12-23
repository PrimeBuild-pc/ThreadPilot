/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using ThreadPilot.Helpers;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for MasksView.xaml
    /// Based on CPUSetSetter's data-binding pattern
    /// </summary>
    public partial class MasksView : System.Windows.Controls.UserControl
    {
        public MasksView()
        {
            InitializeComponent();
            DataContext = ServiceProviderExtensions.GetService<MasksViewModel>();
        }
    }
}

