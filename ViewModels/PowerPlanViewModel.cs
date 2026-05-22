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
namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public partial class PowerPlanViewModel : BaseViewModel
    {
        private readonly IPowerPlanService powerPlanService;
        private System.Timers.Timer? refreshTimer;
        private bool isAutoRefreshPaused = true;
        private int isRefreshInProgress;

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> powerPlans = new();

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> customPowerPlans = new();

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private PowerPlanModel? selectedCustomPlan;

        [ObservableProperty]
        private PowerPlanModel? activePowerPlan;

        public PowerPlanViewModel(
            ILogger<PowerPlanViewModel> logger,
            IPowerPlanService powerPlanService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            this.powerPlanService = powerPlanService;
            this.SetupRefreshTimer();
        }

        private void SetupRefreshTimer()
        {
            this.refreshTimer = new System.Timers.Timer(10000); // PERFORMANCE OPTIMIZATION: Increased to 10 second refresh - power plans change infrequently
            this.refreshTimer.Elapsed += async (s, e) =>
            {
                if (this.isAutoRefreshPaused)
                {
                    return;
                }

                if (Interlocked.Exchange(ref this.isRefreshInProgress, 1) == 1)
                {
                    return;
                }

                try
                {
                    // Marshal timer callback to UI thread to prevent cross-thread access exceptions
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!this.isAutoRefreshPaused)
                        {
                            await this.RefreshPowerPlansCommand.ExecuteAsync(null);
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Power plan refresh timer error: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref this.isRefreshInProgress, 0);
                }
            };
        }

        public void PauseAutoRefresh()
        {
            this.isAutoRefreshPaused = true;
            this.refreshTimer?.Stop();
        }

        public void ResumeAutoRefresh(bool refreshImmediately = true)
        {
            var wasPaused = this.isAutoRefreshPaused;
            this.isAutoRefreshPaused = false;
            this.refreshTimer?.Start();

            if (!refreshImmediately || !wasPaused)
            {
                return;
            }

            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await this.RefreshPowerPlansCommand.ExecuteAsync(null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Power plan immediate refresh error: {ex.Message}");
                }
            });
        }

        [RelayCommand]
        public async Task LoadPowerPlans()
        {
            try
            {
                this.SetStatus("Loading power plans...");
                await this.RefreshPowerPlansCoreAsync(reportStatus: false);
                this.SetStatus("Power plans loaded.", false);
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error loading power plans: {ex.Message}", "PowerPlanLoadFailed");
            }
        }

        [RelayCommand]
        private async Task RefreshPowerPlans()
        {
            if (this.IsBusy)
            {
                return;
            }

            try
            {
                await this.RefreshPowerPlansCoreAsync(reportStatus: true);
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error refreshing power plans: {ex.Message}", "PowerPlanRefreshFailed");
            }
        }

        [RelayCommand]
        private async Task SetActivePlan()
        {
            if (this.SelectedPowerPlan == null)
            {
                return;
            }

            try
            {
                var targetPlan = this.SelectedPowerPlan;
                this.SetStatus($"Setting active power plan to {targetPlan.Name}...");
                var success = await this.powerPlanService.SetActivePowerPlan(targetPlan);

                if (success)
                {
                    this.ActivePowerPlan = targetPlan;
                    await this.RefreshPowerPlansCoreAsync(reportStatus: false);
                    this.SetStatus($"Power plan applied: {targetPlan.Name}.", false);
                    await this.LogUserActionAsync("PowerPlanApplied", $"Applied power plan {targetPlan.Name}", $"Guid: {targetPlan.Guid}");
                }
                else
                {
                    await this.SetOperationFailedAsync($"Failed to set power plan {targetPlan.Name}", "PowerPlanApplyFailed");
                }
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error setting power plan: {ex.Message}", "PowerPlanApplyFailed");
            }
        }

        [RelayCommand]
        private async Task ImportCustomPlan()
        {
            if (this.SelectedCustomPlan == null)
            {
                return;
            }

            try
            {
                var customPlan = this.SelectedCustomPlan;
                this.SetStatus($"Importing custom power plan {customPlan.Name}...");
                var success = await this.powerPlanService.ImportCustomPowerPlan(customPlan.FilePath);

                if (success)
                {
                    await this.RefreshPowerPlansCoreAsync(reportStatus: false);
                    this.SetStatus($"Power plan imported: {customPlan.Name}.", false);
                    await this.LogUserActionAsync("PowerPlanImported", $"Imported power plan {customPlan.Name}", customPlan.FilePath);
                }
                else
                {
                    await this.SetOperationFailedAsync($"Failed to import power plan {customPlan.Name}", "PowerPlanImportFailed");
                }
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error importing power plan: {ex.Message}", "PowerPlanImportFailed");
            }
        }

        [RelayCommand]
        private async Task AddCustomPlanFile()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select custom power plan",
                    Filter = "Power Plan Files (*.pow)|*.pow|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false,
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                this.SetStatus("Adding custom power plan file...");
                var success = await this.powerPlanService.AddCustomPowerPlanFileAsync(dialog.FileName);

                if (success)
                {
                    await this.RefreshPowerPlansCoreAsync(reportStatus: false);
                    this.SetStatus("Custom power plan added to library.", false);
                    await this.LogUserActionAsync("PowerPlanAdded", "Added custom power plan file", dialog.FileName);
                }
                else
                {
                    await this.SetOperationFailedAsync("Failed to add custom power plan file.", "PowerPlanAddFailed");
                }
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error adding custom power plan file: {ex.Message}", "PowerPlanAddFailed");
            }
        }

        [RelayCommand]
        private async Task DeletePowerPlan(PowerPlanModel? powerPlan)
        {
            var targetPlan = powerPlan ?? this.SelectedPowerPlan;
            if (targetPlan == null)
            {
                return;
            }

            var activePlan = this.ActivePowerPlan ?? await this.powerPlanService.GetActivePowerPlan();
            if (targetPlan.IsActive || string.Equals(targetPlan.Guid, activePlan?.Guid, StringComparison.OrdinalIgnoreCase))
            {
                await this.SetOperationFailedAsync("Switch to another power plan before deleting the active plan.", "PowerPlanDeleteBlocked");
                return;
            }

            try
            {
                this.SetStatus($"Deleting power plan {targetPlan.Name}...");
                var success = await this.powerPlanService.DeletePowerPlanAsync(targetPlan.Guid);
                if (!success)
                {
                    await this.SetOperationFailedAsync(
                        $"Could not delete power plan {targetPlan.Name}. Windows may not allow this plan to be removed.",
                        "PowerPlanDeleteFailed");
                    return;
                }

                await this.RefreshPowerPlansCoreAsync(reportStatus: false);
                this.SetStatus($"Power plan deleted: {targetPlan.Name}.", false);
                await this.LogUserActionAsync("PowerPlanDeleted", $"Deleted power plan {targetPlan.Name}", $"Guid: {targetPlan.Guid}");
            }
            catch (Exception ex)
            {
                await this.SetOperationFailedAsync($"Error deleting power plan: {ex.Message}", "PowerPlanDeleteFailed");
            }
        }

        private async Task RefreshPowerPlansCoreAsync(bool reportStatus)
        {
            var currentPlans = await this.powerPlanService.GetPowerPlansAsync();
            var currentActive = await this.powerPlanService.GetActivePowerPlan();
            var customPlans = await this.powerPlanService.GetCustomPowerPlansAsync();

            this.PowerPlans = new ObservableCollection<PowerPlanModel>(currentPlans);
            this.CustomPowerPlans = new ObservableCollection<PowerPlanModel>(customPlans);
            this.ActivePowerPlan = currentActive;

            foreach (var plan in this.PowerPlans)
            {
                plan.IsActive = string.Equals(plan.Guid, currentActive?.Guid, StringComparison.OrdinalIgnoreCase);
            }

            if (this.SelectedPowerPlan != null)
            {
                this.SelectedPowerPlan = this.PowerPlans.FirstOrDefault(p => p.Guid == this.SelectedPowerPlan.Guid);
            }

            if (reportStatus)
            {
                this.SetStatus("Power plans refreshed.", false);
                await this.LogUserActionAsync("PowerPlansRefreshed", "Refreshed power plan list");
            }
        }

        private async Task SetOperationFailedAsync(string message, string action)
        {
            this.SetStatus(message, false);
            this.SetError(message);
            await this.LogUserActionAsync(action, message);
        }
    }
}
