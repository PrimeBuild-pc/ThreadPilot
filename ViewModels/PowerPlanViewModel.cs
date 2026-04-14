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
        private bool isAutoRefreshPaused;
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
            this.refreshTimer.Start();
        }

        public void PauseAutoRefresh()
        {
            this.isAutoRefreshPaused = true;
            this.refreshTimer?.Stop();
        }

        public void ResumeAutoRefresh(bool refreshImmediately = true)
        {
            this.isAutoRefreshPaused = false;
            this.refreshTimer?.Start();

            if (!refreshImmediately)
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
                this.PowerPlans = await this.powerPlanService.GetPowerPlansAsync();
                this.CustomPowerPlans = await this.powerPlanService.GetCustomPowerPlansAsync();
                this.ActivePowerPlan = await this.powerPlanService.GetActivePowerPlan();
                this.ClearStatus();
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error loading power plans: {ex.Message}", false);
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
                var currentPlans = await this.powerPlanService.GetPowerPlansAsync();
                var currentActive = await this.powerPlanService.GetActivePowerPlan();
                var customPlans = await this.powerPlanService.GetCustomPowerPlansAsync();

                // Update power plans
                this.PowerPlans = new ObservableCollection<PowerPlanModel>(currentPlans);
                this.CustomPowerPlans = new ObservableCollection<PowerPlanModel>(customPlans);
                this.ActivePowerPlan = currentActive;

                // Update selected plan if it exists
                if (this.SelectedPowerPlan != null)
                {
                    this.SelectedPowerPlan = this.PowerPlans.FirstOrDefault(p => p.Guid == this.SelectedPowerPlan.Guid);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error refreshing power plans: {ex.Message}", false);
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
                this.SetStatus($"Setting active power plan to {this.SelectedPowerPlan.Name}...");
                var success = await this.powerPlanService.SetActivePowerPlan(this.SelectedPowerPlan);

                if (success)
                {
                    this.ActivePowerPlan = this.SelectedPowerPlan;
                    await this.RefreshPowerPlans();
                    this.ClearStatus();
                }
                else
                {
                    this.SetStatus($"Failed to set power plan {this.SelectedPowerPlan.Name}", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error setting power plan: {ex.Message}", false);
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
                this.SetStatus($"Importing custom power plan {this.SelectedCustomPlan.Name}...");
                var success = await this.powerPlanService.ImportCustomPowerPlan(this.SelectedCustomPlan.FilePath);

                if (success)
                {
                    await this.RefreshPowerPlans();
                    this.ClearStatus();
                }
                else
                {
                    this.SetStatus($"Failed to import power plan {this.SelectedCustomPlan.Name}", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error importing power plan: {ex.Message}", false);
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
                    await this.RefreshPowerPlans();
                    this.SetStatus("Custom power plan added to library.", false);
                }
                else
                {
                    this.SetStatus("Failed to add custom power plan file.", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error adding custom power plan file: {ex.Message}", false);
            }
        }
    }
}
