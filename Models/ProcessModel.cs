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
using System;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThreadPilot.Models
{
    public partial class ProcessModel : ObservableObject
    {
        private Process? _process;
        public Process? Process
        {
            get => _process;
            set
            {
                _process = value;
                if (value != null)
                {
                    ProcessId = value.Id;
                    Name = value.ProcessName;
                    try
                    {
                        ProcessorAffinity = (long)value.ProcessorAffinity;
                        Priority = value.PriorityClass;
                        MemoryUsage = value.PrivateMemorySize64;
                        ExecutablePath = value.MainModule?.FileName ?? string.Empty;
                        MainWindowHandle = value.MainWindowHandle;
                        MainWindowTitle = value.MainWindowTitle ?? string.Empty;
                        HasVisibleWindow = MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(MainWindowTitle);
                    }
                    catch (Exception)
                    {
                        // Process may have terminated or access denied
                    }
                }
            }
        }

        [ObservableProperty]
        private int processId;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string executablePath = string.Empty;

        [ObservableProperty]
        private double cpuUsage;

        [ObservableProperty]
        private long memoryUsage;

        [ObservableProperty]
        private ProcessPriorityClass priority;

        [ObservableProperty]
        private long processorAffinity;

        [ObservableProperty]
        private IntPtr mainWindowHandle;

        [ObservableProperty]
        private string mainWindowTitle = string.Empty;

        [ObservableProperty]
        private bool hasVisibleWindow;

        [ObservableProperty]
        private bool isIdleServerDisabled;

        [ObservableProperty]
        private bool isRegistryPriorityEnabled;

        /// <summary>
        /// Forces PropertyChanged notification for ProcessorAffinity.
        /// Used to update DataGrid binding when affinity changes from background thread.
        /// </summary>
        public void ForceNotifyProcessorAffinityChanged()
        {
            OnPropertyChanged(nameof(ProcessorAffinity));
        }
    }
}
