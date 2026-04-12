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
namespace ThreadPilot.Models
{
    using System;
    using System.Diagnostics;
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class ProcessModel : ObservableObject
    {
        private Process? process;

        public Process? Process
        {
            get => this.process;
            set
            {
                this.process = value;
                if (value != null)
                {
                    this.ProcessId = value.Id;
                    this.Name = value.ProcessName;
                    try
                    {
                        this.ProcessorAffinity = (long)value.ProcessorAffinity;
                        this.Priority = value.PriorityClass;
                        this.MemoryUsage = value.PrivateMemorySize64;
                        this.ExecutablePath = value.MainModule?.FileName ?? string.Empty;
                        this.MainWindowHandle = value.MainWindowHandle;
                        this.MainWindowTitle = value.MainWindowTitle ?? string.Empty;
                        this.HasVisibleWindow = this.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(this.MainWindowTitle);
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
            this.OnPropertyChanged(nameof(this.ProcessorAffinity));
        }
    }
}
