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
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Services;

    /// <summary>
    /// ViewModel for the System Tweaks tab.
    /// </summary>
    public partial class SystemTweaksViewModel : BaseViewModel
    {
        private readonly ISystemTweaksService systemTweaksService;
        private readonly INotificationService notificationService;

        [ObservableProperty]
        private ObservableCollection<SystemTweakItem> tweakItems = new();

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string refreshStatusText = "Ready";

        public SystemTweaksViewModel(
            ISystemTweaksService systemTweaksService,
            INotificationService notificationService,
            ILogger<SystemTweaksViewModel> logger)
            : base(logger, null)
        {
            this.systemTweaksService = systemTweaksService;
            this.notificationService = notificationService;

            // Subscribe to tweak status changes
            this.systemTweaksService.TweakStatusChanged += this.OnTweakStatusChanged;

            this.InitializeTweakItems();
        }

        private void InitializeTweakItems()
        {
            this.TweakItems = new ObservableCollection<SystemTweakItem>
            {
                new SystemTweakItem
                {
                    Name = "Core Parking",
                    Description = "Controls CPU core parking for power management",
                    TweakType = SystemTweak.CoreParking,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "C-States",
                    Description = "Controls CPU C-States for power management",
                    TweakType = SystemTweak.CStates,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "SysMain Service",
                    Description = "Windows Superfetch/SysMain service for memory management",
                    TweakType = SystemTweak.SysMain,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "Prefetch",
                    Description = "Windows Prefetch feature for faster application loading",
                    TweakType = SystemTweak.Prefetch,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "Power Throttling",
                    Description = "Windows Power Throttling for energy efficiency",
                    TweakType = SystemTweak.PowerThrottling,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "HPET",
                    Description = "High Precision Event Timer for system timing",
                    TweakType = SystemTweak.Hpet,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "High Scheduling Category",
                    Description = "High scheduling priority for gaming applications",
                    TweakType = SystemTweak.HighSchedulingCategory,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                },
                new SystemTweakItem
                {
                    Name = "Menu Show Delay",
                    Description = "Delay before showing context menus",
                    TweakType = SystemTweak.MenuShowDelay,
                    IsEnabled = false,
                    IsAvailable = true,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync)
                },
            };
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await this.ExecuteAsync(
                async () =>
            {
                await this.RefreshAllTweaksAsync();
            }, "Loading system tweaks...", "System tweaks loaded successfully");
        }

        [RelayCommand]
        public async Task RefreshAllTweaksAsync()
        {
            try
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.IsRefreshing = true;
                    this.RefreshStatusText = "Refreshing system tweaks...";
                });

                await this.systemTweaksService.RefreshAllStatusesAsync();

                // Update each tweak item with current status
                foreach (var item in this.TweakItems)
                {
                    await this.UpdateTweakItemStatusAsync(item);
                }

                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.RefreshStatusText = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetError("Failed to refresh system tweaks", ex);
                    this.RefreshStatusText = "Refresh failed";
                });
            }
            finally
            {
                // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.IsRefreshing = false;
                });
            }
        }

        private async Task UpdateTweakItemStatusAsync(SystemTweakItem item)
        {
            try
            {
                TweakStatus status = item.TweakType switch
                {
                    SystemTweak.CoreParking => await this.systemTweaksService.GetCoreParkingStatusAsync(),
                    SystemTweak.CStates => await this.systemTweaksService.GetCStatesStatusAsync(),
                    SystemTweak.SysMain => await this.systemTweaksService.GetSysMainStatusAsync(),
                    SystemTweak.Prefetch => await this.systemTweaksService.GetPrefetchStatusAsync(),
                    SystemTweak.PowerThrottling => await this.systemTweaksService.GetPowerThrottlingStatusAsync(),
                    SystemTweak.Hpet => await this.systemTweaksService.GetHpetStatusAsync(),
                    SystemTweak.HighSchedulingCategory => await this.systemTweaksService.GetHighSchedulingCategoryStatusAsync(),
                    SystemTweak.MenuShowDelay => await this.systemTweaksService.GetMenuShowDelayStatusAsync(),
                    _ => new TweakStatus { IsAvailable = false, ErrorMessage = "Unknown tweak type" },
                };

                item.IsEnabled = status.IsEnabled;
                item.IsAvailable = status.IsAvailable;
                item.ErrorMessage = status.ErrorMessage;
                if (!string.IsNullOrEmpty(status.Description))
                {
                    item.Description = status.Description;
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error updating status for tweak {TweakName}", item.Name);
                item.IsAvailable = false;
                item.ErrorMessage = ex.Message;
            }
        }

        private async Task ToggleTweakAsync(SystemTweakItem? item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetStatus($"Toggling {item.Name}...");
                });

                var newState = !item.IsEnabled;
                bool success = item.TweakType switch
                {
                    SystemTweak.CoreParking => await this.systemTweaksService.SetCoreParkingAsync(newState),
                    SystemTweak.CStates => await this.systemTweaksService.SetCStatesAsync(newState),
                    SystemTweak.SysMain => await this.systemTweaksService.SetSysMainAsync(newState),
                    SystemTweak.Prefetch => await this.systemTweaksService.SetPrefetchAsync(newState),
                    SystemTweak.PowerThrottling => await this.systemTweaksService.SetPowerThrottlingAsync(newState),
                    SystemTweak.Hpet => await this.systemTweaksService.SetHpetAsync(newState),
                    SystemTweak.HighSchedulingCategory => await this.systemTweaksService.SetHighSchedulingCategoryAsync(newState),
                    SystemTweak.MenuShowDelay => await this.systemTweaksService.SetMenuShowDelayAsync(newState),
                    _ => false,
                };

                if (success)
                {
                    await this.UpdateTweakItemStatusAsync(item);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetStatus($"{item.Name} {(newState ? "enabled" : "disabled")} successfully");
                    });

                    await this.notificationService.ShowSuccessNotificationAsync(
                        "System Tweak Updated",
                        $"{item.Name} has been {(newState ? "enabled" : "disabled")}");
                }
                else
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.SetError($"Failed to toggle {item.Name}", null);
                    });

                    await this.notificationService.ShowErrorNotificationAsync(
                        "System Tweak Failed",
                        $"Failed to {(newState ? "enable" : "disable")} {item.Name}");
                }
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    this.SetError($"Error toggling {item.Name}", ex);
                });
                this.Logger.LogError(ex, "Error toggling tweak {TweakName}", item.Name);
            }
        }

        private void OnTweakStatusChanged(object? sender, TweakStatusChangedEventArgs e)
        {
            try
            {
                var item = this.TweakItems.FirstOrDefault(t => t.TweakType.ToString() == e.TweakName);
                if (item != null)
                {
                    item.IsEnabled = e.Status.IsEnabled;
                    item.IsAvailable = e.Status.IsAvailable;
                    item.ErrorMessage = e.Status.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error handling tweak status change for {TweakName}", e.TweakName);
            }
        }

        protected override void OnDispose()
        {
            this.systemTweaksService.TweakStatusChanged -= this.OnTweakStatusChanged;
            base.OnDispose();
        }
    }

    /// <summary>
    /// Represents a system tweak item in the UI.
    /// </summary>
    public partial class SystemTweakItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private SystemTweak tweakType;

        [ObservableProperty]
        private bool isEnabled;

        [ObservableProperty]
        private bool isAvailable = true;

        [ObservableProperty]
        private string? errorMessage;

        public IAsyncRelayCommand<SystemTweakItem>? ToggleCommand { get; set; }
    }
}

