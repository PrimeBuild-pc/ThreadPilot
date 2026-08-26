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
                    IsGuidedAction = type is SystemTweak.MemoryIntegrity or SystemTweak.Hags or SystemTweak.WindowedOptimizations,
                    ToggleCommand = new AsyncRelayCommand<SystemTweakItem>(this.ToggleTweakAsync),
                    ActionCommand = new AsyncRelayCommand<SystemTweakItem>(this.OpenTweakSettingsAsync),
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
                    SystemTweak.GameMode => await this.systemTweaksService.GetGameModeStatusAsync(),
                    SystemTweak.CoreParking => await this.systemTweaksService.GetCoreParkingStatusAsync(),
                    SystemTweak.CStates => await this.systemTweaksService.GetCStatesStatusAsync(),
                    SystemTweak.MemoryIntegrity => await this.systemTweaksService.GetMemoryIntegrityStatusAsync(),
                    SystemTweak.Hags or SystemTweak.WindowedOptimizations => new TweakStatus
                    {
                        IsAvailable = true,
                        StatusText = this.Localize("SystemTweaks_StatusManagedByWindows", "Managed by Windows"),
                    },
                    SystemTweak.UsbSelectiveSuspend => await this.systemTweaksService.GetUsbSelectiveSuspendStatusAsync(),
                    SystemTweak.PointerPrecision => await this.systemTweaksService.GetPointerPrecisionStatusAsync(),
                    SystemTweak.EthernetPowerSaving => await this.systemTweaksService.GetEthernetPowerSavingStatusAsync(),
                    SystemTweak.InterruptModeration => await this.systemTweaksService.GetInterruptModerationStatusAsync(),
                    SystemTweak.GpuMsiMode => await this.systemTweaksService.GetGpuMsiModeStatusAsync(),
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
                item.StatusText = status.StatusText ?? this.GetStatusText(status);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error updating status for tweak {TweakName}", item.Name);
                item.IsAvailable = false;

                // The exception text belongs in the log, not on the card: "Requested registry access
                // is not allowed." is a framework string that tells the user nothing they can act on.
                item.ErrorMessage = this.Localize(
                    "SystemTweaks_StatusDetectionFailed",
                    "ThreadPilot could not read this setting on this system.");
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
                    SystemTweak.GameMode => await this.systemTweaksService.SetGameModeAsync(newState),
                    SystemTweak.CoreParking => await this.systemTweaksService.SetCoreParkingAsync(newState),
                    SystemTweak.CStates => await this.systemTweaksService.SetCStatesAsync(newState),
                    SystemTweak.UsbSelectiveSuspend => await this.systemTweaksService.SetUsbSelectiveSuspendAsync(newState),
                    SystemTweak.PointerPrecision => await this.systemTweaksService.SetPointerPrecisionAsync(newState),
                    SystemTweak.EthernetPowerSaving => await this.systemTweaksService.SetEthernetPowerSavingAsync(newState),
                    SystemTweak.InterruptModeration => await this.systemTweaksService.SetInterruptModerationAsync(newState),
                    SystemTweak.GpuMsiMode => await this.systemTweaksService.SetGpuMsiModeAsync(newState),
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

        private async Task OpenTweakSettingsAsync(SystemTweakItem? item)
        {
            if (item == null || !await this.systemTweaksService.OpenSettingsAsync(item.TweakType))
            {
                this.SetError(this.Localize("SystemTweaks_StatusOpenSettingsFailed", "Could not open Windows settings"), null);
                return;
            }

            this.SetStatus(this.Localize("SystemTweaks_StatusSettingsOpenedFormat", "Opened settings for {0}", item.Name));
            await this.LogUserActionAsync("SystemTweakSettingsOpened", $"Opened settings for {item.Name}", item.TweakType.ToString());
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
                    SystemTweak.GameMode => ("SystemTweak_GameMode_Name", "Game Mode", "SystemTweak_GameMode_Desc", "Windows Game Mode scheduling optimizations"),
                    SystemTweak.CoreParking => ("SystemTweak_CoreParking_Name", "Core Parking", "SystemTweak_CoreParking_Desc", "Controls CPU core parking for power management"),
                    SystemTweak.CStates => ("SystemTweak_CStates_Name", "C-States", "SystemTweak_CStates_Desc", "Controls CPU C-States for power management"),
                    SystemTweak.MemoryIntegrity => ("SystemTweak_MemoryIntegrity_Name", "Memory Integrity (HVCI)", "SystemTweak_MemoryIntegrity_Desc", "Opens Windows Security. Disabling this protection reduces system security and may require a restart."),
                    SystemTweak.Hags => ("SystemTweak_Hags_Name", "Hardware-accelerated GPU scheduling", "SystemTweak_Hags_Desc", "Opens Windows graphics settings to let you review HAGS."),
                    SystemTweak.WindowedOptimizations => ("SystemTweak_WindowedOptimizations_Name", "Optimizations for windowed games", "SystemTweak_WindowedOptimizations_Desc", "Opens Windows graphics settings to manage Flip Model optimizations."),
                    SystemTweak.UsbSelectiveSuspend => ("SystemTweak_UsbSelectiveSuspend_Name", "USB Selective Suspend", "SystemTweak_UsbSelectiveSuspend_Desc", "Controls USB selective suspend for the active power plan while plugged in."),
                    SystemTweak.PointerPrecision => ("SystemTweak_PointerPrecision_Name", "Enhance pointer precision", "SystemTweak_PointerPrecision_Desc", "Controls Windows mouse acceleration."),
                    SystemTweak.EthernetPowerSaving => ("SystemTweak_EthernetPowerSaving_Name", "Disable Ethernet power saving", "SystemTweak_EthernetPowerSaving_Desc", "Disables supported EEE, Green Ethernet and power-saving driver properties on physical Ethernet adapters."),
                    SystemTweak.InterruptModeration => ("SystemTweak_InterruptModeration_Name", "Disable interrupt moderation", "SystemTweak_InterruptModeration_Desc", "Can reduce Ethernet latency at the cost of higher CPU and interrupt load."),
                    SystemTweak.GpuMsiMode => ("SystemTweak_GpuMsiMode_Name", "GPU MSI Mode", "SystemTweak_GpuMsiMode_Desc", "Controls MSI only when the display driver already exposes MSISupported. A restart is required."),
                    SystemTweak.SysMain => ("SystemTweak_SysMain_Name", "SysMain Service", "SystemTweak_SysMain_Desc", "Windows SysMain service for memory management"),
                    SystemTweak.Prefetch => ("SystemTweak_Prefetch_Name", "Prefetch", "SystemTweak_Prefetch_Desc", "Windows Prefetch for faster application loading"),
                    SystemTweak.PowerThrottling => ("SystemTweak_PowerThrottling_Name", "Power Throttling", "SystemTweak_PowerThrottling_Desc", "Turns off Windows Power Throttling for sustained performance"),
                    SystemTweak.HighSchedulingCategory => ("SystemTweak_HighSchedulingCategory_Name", "High Scheduling Category", "SystemTweak_HighSchedulingCategory_Desc", "Uses the high MMCSS scheduling category for games"),
                    SystemTweak.MenuShowDelay => ("SystemTweak_MenuShowDelay_Name", "Menu Show Delay", "SystemTweak_MenuShowDelay_Desc", "Delay before showing context menus"),
                    _ => throw new ArgumentOutOfRangeException(nameof(item.TweakType), item.TweakType, null),
                };

                item.Name = this.Localize(text.Item1, text.Item2);
                item.Description = this.Localize(text.Item3, text.Item4);
                if (!string.IsNullOrEmpty(item.StatusText) && item.TweakType is not SystemTweak.Hags and not SystemTweak.WindowedOptimizations)
                {
                    item.StatusText = this.GetStatusText(new TweakStatus { IsAvailable = item.IsAvailable, IsEnabled = item.IsEnabled });
                }
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

        private string GetStatusText(TweakStatus status) => !status.IsAvailable
            ? this.Localize("SystemTweaks_StatusNotAvailable", "Not available")
            : this.Localize(status.IsEnabled ? "SystemTweaks_StatusEnabled" : "SystemTweaks_StatusDisabled", status.IsEnabled ? "Enabled" : "Disabled");

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
                    item.StatusText = e.Status.StatusText ?? this.GetStatusText(e.Status);
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

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private bool isGuidedAction;

        public IAsyncRelayCommand<SystemTweakItem>? ToggleCommand { get; set; }

        public IAsyncRelayCommand<SystemTweakItem>? ActionCommand { get; set; }
    }
}
