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
namespace ThreadPilot
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public partial class MainWindowViewModel : BaseViewModel
    {
        private readonly IProcessMonitorManagerService? processMonitorManagerService;
        private readonly INotificationService? notificationService;
        private readonly IElevationService? elevationService;
        private readonly ISecurityService? securityService;

        [ObservableProperty]
        private bool isProcessMonitoringActive = false;

        [ObservableProperty]
        private string processMonitoringStatusText = "Process Monitoring: Inactive";

        [ObservableProperty]
        private bool isRunningAsAdministrator = false;

        [ObservableProperty]
        private string elevationStatusText = "Checking elevation status...";

        [ObservableProperty]
        private bool showElevationPrompt = false;

        [ObservableProperty]
        private string initializationStage = "Starting ThreadPilot...";

        [ObservableProperty]
        private string initializationDetails = "Preparing startup sequence.";

        [ObservableProperty]
        private bool isDarkTheme = false;

        [ObservableProperty]
        private string applicationVersion = "v0.0.0";

        public MainWindowViewModel(
            ILogger<MainWindowViewModel> logger,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IProcessMonitorManagerService? processMonitorManagerService = null,
            INotificationService? notificationService = null,
            IElevationService? elevationService = null,
            ISecurityService? securityService = null)
            : base(logger, enhancedLoggingService)
        {
            this.processMonitorManagerService = processMonitorManagerService;
            this.notificationService = notificationService;
            this.elevationService = elevationService;
            this.securityService = securityService;
            this.ApplicationVersion = GetApplicationVersion();
        }

        public override async Task InitializeAsync()
        {
            await this.ExecuteAsync(
                async () =>
            {
                // Subscribe to service events
                if (this.processMonitorManagerService != null)
                {
                    this.processMonitorManagerService.ServiceStatusChanged += this.OnServiceStatusChanged;
                }

                // Initialize status
                await this.UpdateStatusAsync();
                this.UpdateElevationStatus();

                await this.LogUserActionAsync("MainWindow", "Initialized main window", "Application startup");
            }, "Initializing main window...");
        }

        [RelayCommand]
        private async Task ToggleProcessMonitoringAsync()
        {
            if (this.processMonitorManagerService == null)
            {
                return;
            }

            await this.ExecuteAsync(
                async () =>
            {
                if (this.IsProcessMonitoringActive)
                {
                    await this.processMonitorManagerService.StopAsync();
                    await this.LogUserActionAsync("ProcessMonitoring", "Stopped process monitoring", "User action");
                }
                else
                {
                    await this.processMonitorManagerService.StartAsync();
                    await this.LogUserActionAsync("ProcessMonitoring", "Started process monitoring", "User action");
                }
            }, this.IsProcessMonitoringActive ? "Stopping monitoring..." : "Starting monitoring...");
        }

        [RelayCommand]
        private async Task RequestElevationAsync()
        {
            if (this.elevationService == null)
            {
                return;
            }

            await this.ExecuteAsync(
                async () =>
            {
                var success = await this.elevationService.RequestElevationIfNeeded();
                if (success)
                {
                    await this.LogUserActionAsync("Elevation", "Requested elevation", "User action");
                }
                else
                {
                    await this.LogUserActionAsync("Elevation", "Elevation request failed or cancelled", "User action");
                }
            }, "Requesting elevation...");
        }

        private async Task UpdateStatusAsync()
        {
            try
            {
                // Update process monitoring status
                if (this.processMonitorManagerService != null)
                {
                    this.IsProcessMonitoringActive = this.processMonitorManagerService.IsRunning;
                    this.ProcessMonitoringStatusText = this.IsProcessMonitoringActive
                        ? "Process Monitoring: Active"
                        : "Process Monitoring: Inactive";
                }

                // Update elevation status
                this.UpdateElevationStatus();
            }
            catch (Exception ex)
            {
                this.SetError("Failed to update status", ex);
            }
        }

        private void UpdateElevationStatus()
        {
            if (this.elevationService == null)
            {
                this.IsRunningAsAdministrator = false;
                this.ElevationStatusText = "Elevation service not available";
                this.ShowElevationPrompt = false;
                return;
            }

            this.IsRunningAsAdministrator = this.elevationService.IsRunningAsAdministrator();
            this.ElevationStatusText = this.elevationService.GetElevationStatus();
            this.ShowElevationPrompt = !this.IsRunningAsAdministrator;
        }

        private void OnServiceStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.IsProcessMonitoringActive = e.IsRunning;
                this.ProcessMonitoringStatusText = $"Process Monitoring: {e.Status}";
            });
        }

        public void UpdateProcessMonitoringStatus(bool isActive, string status)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                this.IsProcessMonitoringActive = isActive;
                this.ProcessMonitoringStatusText = $"Process Monitoring: {status}";
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.IsProcessMonitoringActive = isActive;
                    this.ProcessMonitoringStatusText = $"Process Monitoring: {status}";
                });
            }
        }

        protected override void OnDispose()
        {
            if (this.processMonitorManagerService != null)
            {
                this.processMonitorManagerService.ServiceStatusChanged -= this.OnServiceStatusChanged;
            }

            base.OnDispose();
        }

        private static string GetApplicationVersion()
        {
            var assembly = typeof(MainWindowViewModel).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            var normalizedVersion = string.IsNullOrWhiteSpace(informationalVersion)
                ? assembly.GetName().Version?.ToString(3) ?? "0.0.0"
                : informationalVersion.Split('+')[0];

            return normalizedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? normalizedVersion
                : $"v{normalizedVersion}";
        }
    }
}
