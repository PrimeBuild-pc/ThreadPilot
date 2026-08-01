namespace ThreadPilot.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Services;

    public partial class SystemTweaksViewModel : BaseViewModel
    {
        private readonly ISystemTweaksService systemTweaksService;
        private readonly INotificationService notificationService;
        private readonly ILocalizationService localizationService;

        [ObservableProperty]
        private ObservableCollection<SystemTweakItem> tweakItems = new();

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string refreshStatusText = "Ready";

        public SystemTweaksViewModel(
            ISystemTweaksService systemTweaksService,
            INotificationService notificationService,
            ILocalizationService localizationService,
            ILogger<SystemTweaksViewModel> logger,
            IEnhancedLoggingService? enhancedLoggingService = null,
            IActivityAuditService? activityAuditService = null)
            : base(logger, enhancedLoggingService, activityAuditService)
        {
            this.systemTweaksService = systemTweaksService;
            this.notificationService = notificationService;
            this.localizationService = localizationService;

            this.systemTweaksService.TweakStatusChanged += this.OnTweakStatusChanged;
            this.localizationService.LanguageChanged += this.OnLanguageChanged;

            this.InitializeTweakItems();
        }

        private void InitializeTweakItems()
        {
            this.TweakItems = new ObservableCollection<SystemTweakItem>(
                Enum.GetValues<SystemTweak>().Select(type => new SystemTweakItem
                {
                    TweakType = type,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                }));
            this.ApplyLocalizedTweakText();
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await this.RefreshAllTweaksAsync();
        }

        [RelayCommand]
        public async Task RefreshAllTweaksAsync()
        {
            try
            {
                await InvokeOnUiAsync(() =>
                {
                    this.IsRefreshing = true;
                    this.RefreshStatusText = this.Localize("SystemTweaks_StatusRefreshing", "Refreshing system tweaks...");
                });

                await Task.WhenAll(this.TweakItems.Select(this.UpdateTweakItemStatusAsync));

                await InvokeOnUiAsync(() =>
                {
                    this.RefreshStatusText = this.Localize(
                        "SystemTweaks_StatusLastRefreshedFormat",
                        "Last refreshed: {0}",
                        DateTime.Now.ToString("T"));
                });
            }
            catch (Exception ex)
            {
                await InvokeOnUiAsync(() =>
                {
                    this.SetError(this.Localize("SystemTweaks_StatusRefreshFailed", "Failed to refresh system tweaks"), ex);
                    this.RefreshStatusText = this.Localize("SystemTweaks_StatusRefreshFailedShort", "Refresh failed");
                });
            }
            finally
            {
                await InvokeOnUiAsync(() =>
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
                    SystemTweak.HighSchedulingCategory => await this.systemTweaksService.GetHighSchedulingCategoryStatusAsync(),
                    SystemTweak.MenuShowDelay => await this.systemTweaksService.GetMenuShowDelayStatusAsync(),
                    _ => new TweakStatus { IsAvailable = false, ErrorMessage = "Unknown tweak type" },
                };

                item.IsEnabled = status.IsEnabled;
                item.IsAvailable = status.IsAvailable;
                item.ErrorMessage = status.ErrorMessage;
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
                await InvokeOnUiAsync(() =>
                {
                    this.SetStatus(this.Localize("SystemTweaks_StatusTogglingFormat", "Toggling {0}...", item.Name));
                });

                var newState = !item.IsEnabled;
                bool success = item.TweakType switch
                {
                    SystemTweak.CoreParking => await this.systemTweaksService.SetCoreParkingAsync(newState),
                    SystemTweak.CStates => await this.systemTweaksService.SetCStatesAsync(newState),
                    SystemTweak.SysMain => await this.systemTweaksService.SetSysMainAsync(newState),
                    SystemTweak.Prefetch => await this.systemTweaksService.SetPrefetchAsync(newState),
                    SystemTweak.PowerThrottling => await this.systemTweaksService.SetPowerThrottlingAsync(newState),
                    SystemTweak.HighSchedulingCategory => await this.systemTweaksService.SetHighSchedulingCategoryAsync(newState),
                    SystemTweak.MenuShowDelay => await this.systemTweaksService.SetMenuShowDelayAsync(newState),
                    _ => false,
                };

                if (success)
                {
                    await this.UpdateTweakItemStatusAsync(item);
                    await InvokeOnUiAsync(() =>
                    {
                        this.SetStatus(this.Localize(
                            newState ? "SystemTweaks_StatusEnabledFormat" : "SystemTweaks_StatusDisabledFormat",
                            newState ? "{0} enabled successfully" : "{0} disabled successfully",
                            item.Name));
                    });

                    await this.notificationService.ShowSuccessNotificationAsync(
                        this.Localize("SystemTweaks_NotifyUpdatedTitle", "System Tweak Updated"),
                        this.Localize(
                            newState ? "SystemTweaks_NotifyEnabledFormat" : "SystemTweaks_NotifyDisabledFormat",
                            newState ? "{0} has been enabled" : "{0} has been disabled",
                            item.Name));
                    await this.LogUserActionAsync(
                        "SystemTweakApplied",
                        $"{item.Name} {(newState ? "enabled" : "disabled")}",
                        item.TweakType.ToString());
                }
                else
                {
                    await InvokeOnUiAsync(() =>
                    {
                        this.SetError(this.Localize("SystemTweaks_StatusToggleFailedFormat", "Failed to toggle {0}", item.Name), null);
                    });

                    await this.notificationService.ShowErrorNotificationAsync(
                        this.Localize("SystemTweaks_NotifyFailedTitle", "System Tweak Failed"),
                        this.Localize(
                            newState ? "SystemTweaks_StatusEnableFailedFormat" : "SystemTweaks_StatusDisableFailedFormat",
                            newState ? "Failed to enable {0}" : "Failed to disable {0}",
                            item.Name));
                    await this.LogUserActionAsync(
                        "SystemTweakFailed",
                        $"Failed to {(newState ? "enable" : "disable")} {item.Name}",
                        item.TweakType.ToString());
                }
            }
            catch (Exception ex)
            {
                await InvokeOnUiAsync(() =>
                {
                    this.SetError(this.Localize("SystemTweaks_StatusErrorTogglingFormat", "Error toggling {0}", item.Name), ex);
                });
                this.Logger.LogError(ex, "Error toggling tweak {TweakName}", item.Name);
                await this.LogUserActionAsync(
                    "SystemTweakFailed",
                    $"Error toggling {item.Name}: {ex.Message}",
                    item.TweakType.ToString());
            }
        }

        private static Task InvokeOnUiAsync(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        private void ApplyLocalizedTweakText()
        {
            foreach (var item in this.TweakItems)
            {
                var text = item.TweakType switch
                {
                    SystemTweak.CoreParking => ("SystemTweak_CoreParking_Name", "Core Parking", "SystemTweak_CoreParking_Desc", "Controls CPU core parking for power management"),
                    SystemTweak.CStates => ("SystemTweak_CStates_Name", "C-States", "SystemTweak_CStates_Desc", "Controls CPU C-States for power management"),
                    SystemTweak.SysMain => ("SystemTweak_SysMain_Name", "SysMain Service", "SystemTweak_SysMain_Desc", "Windows SysMain service for memory management"),
                    SystemTweak.Prefetch => ("SystemTweak_Prefetch_Name", "Prefetch", "SystemTweak_Prefetch_Desc", "Windows Prefetch for faster application loading"),
                    SystemTweak.PowerThrottling => ("SystemTweak_PowerThrottling_Name", "Power Throttling", "SystemTweak_PowerThrottling_Desc", "Turns off Windows Power Throttling for sustained performance"),
                    SystemTweak.HighSchedulingCategory => ("SystemTweak_HighSchedulingCategory_Name", "High Scheduling Category", "SystemTweak_HighSchedulingCategory_Desc", "Uses the high MMCSS scheduling category for games"),
                    SystemTweak.MenuShowDelay => ("SystemTweak_MenuShowDelay_Name", "Menu Show Delay", "SystemTweak_MenuShowDelay_Desc", "Delay before showing context menus"),
                    _ => throw new ArgumentOutOfRangeException(nameof(item.TweakType), item.TweakType, null),
                };

                item.Name = this.Localize(text.Item1, text.Item2);
                item.Description = this.Localize(text.Item3, text.Item4);
            }

            this.RefreshStatusText = this.Localize("SystemTweaks_StatusReady", "Ready");
        }

        private string Localize(string key, string fallback, params object[] arguments)
        {
            var localized = this.localizationService.GetString(key);
            var format = string.IsNullOrWhiteSpace(localized) || string.Equals(localized, key, StringComparison.Ordinal)
                ? fallback
                : localized;
            return arguments.Length == 0
                ? format
                : string.Format(CultureInfo.CurrentCulture, format, arguments);
        }

        private void OnLanguageChanged(object? sender, string language) => this.ApplyLocalizedTweakText();

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
            this.localizationService.LanguageChanged -= this.OnLanguageChanged;
            base.OnDispose();
        }
    }

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
