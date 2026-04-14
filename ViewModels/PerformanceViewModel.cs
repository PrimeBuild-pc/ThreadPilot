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
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public partial class PerformanceViewModel : BaseViewModel
    {
        private readonly IPerformanceMonitoringService performanceService;
        private readonly IProcessService processService;
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IPowerPlanService powerPlanService;
        private readonly IProcessMonitorManagerService processMonitorManagerService;
        private readonly ISystemTweaksService systemTweaksService;
        private readonly ILogger<PerformanceViewModel> logger;

        [ObservableProperty]
        private ObservableCollection<CpuCoreUsage> coreUsages = new();

        [ObservableProperty]
        private ObservableCollection<ProcessPerformanceInfo> topCpuProcesses = new();

        [ObservableProperty]
        private ObservableCollection<SystemPerformanceMetrics> historicalData = new();

        [ObservableProperty]
        private ObservableCollection<PerformanceTimelineEvent> timelineEvents = new();

        [ObservableProperty]
        private ProcessPerformanceInfo? selectedHotspotProcess;

        [ObservableProperty]
        private bool isMonitoring;

        [ObservableProperty]
        private string monitoringStatusText = "Monitoring Stopped";

        [ObservableProperty]
        private DateTime lastUpdateTime;

        [ObservableProperty]
        private double totalCpuUsage;

        [ObservableProperty]
        private long totalMemoryUsage;

        [ObservableProperty]
        private long totalMemory;

        [ObservableProperty]
        private long availableMemory;

        [ObservableProperty]
        private double memoryUsagePercentage;

        [ObservableProperty]
        private int activeProcessCount;

        [ObservableProperty]
        private string cpuUsageText = "0.0%";

        [ObservableProperty]
        private string memoryUsageText = "0 MB / 0 MB";

        [ObservableProperty]
        private string processCountText = "0";

        [ObservableProperty]
        private string currentGlobalPowerPlanText = "Unknown";

        [ObservableProperty]
        private string monitoringStateText = "Stopped";

        [ObservableProperty]
        private string selectedProcessName = "No hotspot selected";

        [ObservableProperty]
        private string selectedProcessExecutable = "-";

        [ObservableProperty]
        private string selectedProcessCpuText = "-";

        [ObservableProperty]
        private string selectedProcessMemoryText = "-";

        [ObservableProperty]
        private string selectedProcessRuleStatus = "No linked rule";

        [ObservableProperty]
        private string selectedProcessRuleSummary = "Create a rule from this process to automate affinity and priority behavior.";

        [ObservableProperty]
        private string selectedProcessLastApplyText = "No recent automation event";

        [ObservableProperty]
        private bool canCreateRuleFromSelectedProcess;

        [ObservableProperty]
        private string timelineSampleCountText = "0 samples";

        [ObservableProperty]
        private string lastTimelineEventText = "No events yet";

        [ObservableProperty]
        private int updateInterval = 2000;

        [ObservableProperty]
        private bool showCoreDetails = true;

        [ObservableProperty]
        private bool showProcessDetails = true;

        [ObservableProperty]
        private bool showOnlyRuleBackedHotspots;

        [ObservableProperty]
        private bool showOnlyActionableHotspots = true;

        [ObservableProperty]
        private string sortMode = "Cpu";

        [ObservableProperty]
        private string lastManualRefreshText = "Not refreshed yet";

        [ObservableProperty]
        private string processSearchText = string.Empty;

        [ObservableProperty]
        private bool isRuleCreateBusy;

        [ObservableProperty]
        private bool isPopupVisible;

        [ObservableProperty]
        private string popupTitle = string.Empty;

        [ObservableProperty]
        private string popupContent = string.Empty;

        [ObservableProperty]
        private int blurRadius;

        private readonly Dictionary<string, DateTime> lastRuleApplyByExecutable = new(StringComparer.OrdinalIgnoreCase);
        private bool monitoringWasActiveBeforeSuspend;
        private readonly SemaphoreSlim topProcessRefreshGate = new(1, 1);
        private bool pendingTopProcessRefresh;

        public PerformanceViewModel(
            IPerformanceMonitoringService performanceService,
            IProcessService processService,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            IProcessMonitorManagerService processMonitorManagerService,
            ISystemTweaksService systemTweaksService,
            ILogger<PerformanceViewModel> logger)
            : base(logger, null)
        {
            this.performanceService = performanceService;
            this.processService = processService;
            this.associationService = associationService;
            this.powerPlanService = powerPlanService;
            this.processMonitorManagerService = processMonitorManagerService;
            this.systemTweaksService = systemTweaksService;
            this.logger = logger;

            this.performanceService.MetricsUpdated += this.OnMetricsUpdated;
            this.processMonitorManagerService.ProcessPowerPlanChanged += this.OnProcessPowerPlanChanged;
            this.powerPlanService.PowerPlanChanged += this.OnPowerPlanChanged;
            this.systemTweaksService.TweakStatusChanged += this.OnTweakStatusChanged;
        }

        public override async Task InitializeAsync()
        {
            try
            {
                this.SetStatus("Initializing performance dashboard...");

                await this.RefreshMetricsAsync();
                await this.LoadHistoricalDataAsync();
                await this.LoadTopProcessesAsync();
                await this.RefreshGlobalPowerPlanAsync();

                this.MonitoringStateText = this.IsMonitoring ? "Active" : "Stopped";
                this.SetStatus("Performance dashboard initialized", false);
            }
            catch (Exception ex)
            {
                this.SetError("Failed to initialize performance dashboard", ex);
                this.logger.LogError(ex, "Error initializing performance dashboard");
            }
        }

        [RelayCommand]
        private async Task StartMonitoringAsync()
        {
            try
            {
                this.SetStatus("Starting performance monitoring...");
                await this.performanceService.StartMonitoringAsync();

                this.IsMonitoring = true;
                this.MonitoringStatusText = "Monitoring Active";
                this.MonitoringStateText = "Active";
                this.AddTimelineEvent("Monitoring", "Real-time monitoring started.", "Info");

                this.SetStatus("Performance monitoring started", false);
            }
            catch (Exception ex)
            {
                this.SetError("Failed to start performance monitoring", ex);
            }
        }

        [RelayCommand]
        private async Task StopMonitoringAsync()
        {
            try
            {
                this.SetStatus("Stopping performance monitoring...");
                await this.performanceService.StopMonitoringAsync();

                this.IsMonitoring = false;
                this.MonitoringStatusText = "Monitoring Stopped";
                this.MonitoringStateText = "Stopped";
                this.AddTimelineEvent("Monitoring", "Real-time monitoring stopped.", "Warning");

                this.SetStatus("Performance monitoring stopped", false);
            }
            catch (Exception ex)
            {
                this.SetError("Failed to stop performance monitoring", ex);
            }
        }

        public async Task SuspendBackgroundMonitoringAsync()
        {
            try
            {
                if (!this.IsMonitoring)
                {
                    this.monitoringWasActiveBeforeSuspend = false;
                    return;
                }

                this.monitoringWasActiveBeforeSuspend = true;
                await this.performanceService.StopMonitoringAsync();

                this.IsMonitoring = false;
                this.MonitoringStatusText = "Monitoring Paused";
                this.MonitoringStateText = "Paused";
                this.AddTimelineEvent("Monitoring", "Monitoring paused while app is minimized.", "Info");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to suspend performance monitoring while minimized");
            }
        }

        public async Task ResumeBackgroundMonitoringAsync()
        {
            try
            {
                if (!this.monitoringWasActiveBeforeSuspend || this.IsMonitoring)
                {
                    return;
                }

                await this.performanceService.StartMonitoringAsync();

                this.IsMonitoring = true;
                this.MonitoringStatusText = "Monitoring Active";
                this.MonitoringStateText = "Active";
                this.AddTimelineEvent("Monitoring", "Monitoring resumed after restore.", "Info");
                this.monitoringWasActiveBeforeSuspend = false;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to resume performance monitoring after restore");
            }
        }

        [RelayCommand]
        private async Task RefreshMetricsAsync()
        {
            try
            {
                this.SetStatus("Refreshing performance snapshot...");

                var metrics = await this.performanceService.GetSystemMetricsAsync();
                await this.RefreshGlobalPowerPlanAsync();
                await this.LoadTopProcessesAsync();
                this.UpdateMetrics(metrics);

                this.LastManualRefreshText = $"Refreshed at {DateTime.Now:HH:mm:ss}";
                this.SetStatus("Performance snapshot refreshed", false);
            }
            catch (Exception ex)
            {
                this.SetError("Failed to refresh performance snapshot", ex);
            }
        }

        [RelayCommand]
        private async Task ClearHistoricalDataAsync()
        {
            try
            {
                await this.performanceService.ClearHistoricalDataAsync();
                this.HistoricalData.Clear();
                this.UpdateTimelineSummary();
                this.AddTimelineEvent("History", "Historical metrics cleared.", "Info");
                this.SetStatus("Historical data cleared", false);
            }
            catch (Exception ex)
            {
                this.SetError("Failed to clear historical data", ex);
            }
        }

        [RelayCommand]
        private async Task LoadHistoricalDataAsync()
        {
            try
            {
                var history = await this.performanceService.GetHistoricalDataAsync(TimeSpan.FromHours(1));
                this.HistoricalData = new ObservableCollection<SystemPerformanceMetrics>(history);
                this.UpdateTimelineSummary();
            }
            catch (Exception ex)
            {
                this.SetError("Failed to load historical data", ex);
            }
        }

        [RelayCommand]
        private async Task CreateRuleFromSelectedProcessAsync()
        {
            if (this.SelectedHotspotProcess == null || this.IsRuleCreateBusy)
            {
                return;
            }

            this.IsRuleCreateBusy = true;

            try
            {
                var liveProcesses = await this.processService.GetProcessesAsync();
                var targetProcess = liveProcesses.FirstOrDefault(p => p.ProcessId == this.SelectedHotspotProcess.ProcessId)
                                   ?? liveProcesses.FirstOrDefault(p =>
                                       string.Equals(p.Name, this.SelectedHotspotProcess.ProcessName, StringComparison.OrdinalIgnoreCase));

                if (targetProcess == null)
                {
                    this.SetStatus("Selected hotspot process is no longer running", false);
                    return;
                }

                var activePlan = await this.powerPlanService.GetActivePowerPlan();
                if (activePlan == null)
                {
                    this.SetStatus("Could not resolve active global power plan", false);
                    return;
                }

                var executableName = NormalizeExecutableName(targetProcess.Name);
                var existing = await this.associationService.FindAssociationByExecutableAsync(executableName);

                if (existing == null)
                {
                    var association = new ProcessPowerPlanAssociation
                    {
                        ExecutableName = executableName,
                        ExecutablePath = targetProcess.ExecutablePath ?? string.Empty,
                        PowerPlanGuid = activePlan.Guid,
                        PowerPlanName = activePlan.Name,
                        ProcessPriority = targetProcess.Priority.ToString(),
                        MatchByPath = !string.IsNullOrWhiteSpace(targetProcess.ExecutablePath),
                        Priority = 0,
                        Description = $"Created from Performance hotspot on {DateTime.Now:g}",
                        IsEnabled = true,
                        UpdatedAt = DateTime.UtcNow,
                    };

                    var added = await this.associationService.AddAssociationAsync(association);
                    if (!added)
                    {
                        this.SetStatus("A rule already exists and could not be created", false);
                        return;
                    }

                    this.AddTimelineEvent("Rule", $"Rule created for {executableName} from hotspot panel.", "Success");
                    this.SetStatus($"Rule created for {executableName} and ready for automation.", false);
                }
                else
                {
                    existing.ExecutablePath = targetProcess.ExecutablePath ?? existing.ExecutablePath;
                    existing.PowerPlanGuid = activePlan.Guid;
                    existing.PowerPlanName = activePlan.Name;
                    existing.ProcessPriority = targetProcess.Priority.ToString();
                    existing.IsEnabled = true;
                    existing.MatchByPath = !string.IsNullOrWhiteSpace(existing.ExecutablePath);
                    existing.Description = $"Updated from Performance hotspot on {DateTime.Now:g}";
                    existing.UpdatedAt = DateTime.UtcNow;

                    var updated = await this.associationService.UpdateAssociationAsync(existing);
                    if (!updated)
                    {
                        this.SetStatus("Failed to update existing rule from hotspot", false);
                        return;
                    }

                    this.AddTimelineEvent("Rule", $"Rule updated for {executableName} from hotspot panel.", "Success");
                    this.SetStatus($"Rule updated for {executableName} from hotspot panel.", false);
                }

                await this.RefreshSelectedProcessRuleImpactAsync();
                await this.LoadTopProcessesAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create or update rule from performance hotspot");
                this.SetError("Failed to create rule from selected hotspot", ex);
            }
            finally
            {
                this.IsRuleCreateBusy = false;
            }
        }

        [RelayCommand]
        private void ToggleCoreDetails()
        {
            this.ShowCoreDetails = !this.ShowCoreDetails;
        }

        [RelayCommand]
        private void ToggleProcessDetails()
        {
            this.ShowProcessDetails = !this.ShowProcessDetails;
        }

        [RelayCommand]
        private void ShowPopup((string Title, string Content) parameters)
        {
            this.PopupTitle = parameters.Title;
            this.PopupContent = parameters.Content;
            this.IsPopupVisible = true;
        }

        [RelayCommand]
        private void HidePopup()
        {
            this.IsPopupVisible = false;
        }

        partial void OnSelectedHotspotProcessChanged(ProcessPerformanceInfo? value)
        {
            _ = RefreshSelectedProcessRuleImpactAsync();
            CanCreateRuleFromSelectedProcess = value != null;
        }

        partial void OnShowOnlyRuleBackedHotspotsChanged(bool value)
        {
            _ = LoadTopProcessesAsync();
        }

        partial void OnShowOnlyActionableHotspotsChanged(bool value)
        {
            _ = LoadTopProcessesAsync();
        }

        partial void OnSortModeChanged(string value)
        {
            _ = LoadTopProcessesAsync();
        }

        partial void OnProcessSearchTextChanged(string value)
        {
            _ = LoadTopProcessesAsync();
        }

        partial void OnIsPopupVisibleChanged(bool value)
        {
            this.BlurRadius = value ? 15 : 0;
        }

        private async Task RefreshSelectedProcessRuleImpactAsync()
        {
            try
            {
                if (this.SelectedHotspotProcess == null)
                {
                    this.SelectedProcessName = "No hotspot selected";
                    this.SelectedProcessExecutable = "-";
                    this.SelectedProcessCpuText = "-";
                    this.SelectedProcessMemoryText = "-";
                    this.SelectedProcessRuleStatus = "No linked rule";
                    this.SelectedProcessRuleSummary = "Create a rule from this process to automate affinity and priority behavior.";
                    this.SelectedProcessLastApplyText = "No recent automation event";
                    return;
                }

                var executableName = NormalizeExecutableName(this.SelectedHotspotProcess.ProcessName);
                var association = await this.associationService.FindAssociationByExecutableAsync(executableName);

                this.SelectedProcessName = this.SelectedHotspotProcess.ProcessName;
                this.SelectedProcessExecutable = string.IsNullOrWhiteSpace(this.SelectedHotspotProcess.ExecutablePath)
                    ? executableName
                    : this.SelectedHotspotProcess.ExecutablePath;
                this.SelectedProcessCpuText = $"{this.SelectedHotspotProcess.CpuUsage:F1}% CPU";
                this.SelectedProcessMemoryText = FormatBytes(this.SelectedHotspotProcess.MemoryUsage);

                if (association == null)
                {
                    this.SelectedProcessRuleStatus = "No linked rule";
                    this.SelectedProcessRuleSummary = "No automation rule matches this executable yet.";
                }
                else
                {
                    this.SelectedProcessRuleStatus = association.IsEnabled ? "Linked rule is active" : "Linked rule is disabled";
                    this.SelectedProcessRuleSummary = BuildRuleSummary(association);
                }

                if (this.lastRuleApplyByExecutable.TryGetValue(executableName, out var appliedAt))
                {
                    this.SelectedProcessLastApplyText = $"Last rule application: {appliedAt:HH:mm:ss}";
                }
                else
                {
                    this.SelectedProcessLastApplyText = "No recent automation event";
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to refresh rule impact panel");
            }
        }

        private async Task LoadTopProcessesAsync()
        {
            if (this.topProcessRefreshGate.CurrentCount == 0)
            {
                this.pendingTopProcessRefresh = true;
                return;
            }

            await this.topProcessRefreshGate.WaitAsync();

            try
            {
                do
                {
                    this.pendingTopProcessRefresh = false;

                    var topCpu = await this.performanceService.GetTopCpuProcessesAsync(25);
                    var topMemory = await this.performanceService.GetTopMemoryProcessesAsync(25);

                    var merged = topCpu
                        .Concat(topMemory)
                        .GroupBy(p => p.ProcessId)
                        .Select(g => g.OrderByDescending(x => x.CpuUsage).First())
                        .ToList();

                    var associations = await this.associationService.GetAssociationsAsync();
                    var associationSet = associations
                        .Select(a => NormalizeExecutableName(a.ExecutableName))
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    IEnumerable<ProcessPerformanceInfo> filtered = merged;

                    if (!string.IsNullOrWhiteSpace(this.ProcessSearchText))
                    {
                        filtered = filtered.Where(p => p.ProcessName.Contains(this.ProcessSearchText, StringComparison.OrdinalIgnoreCase));
                    }

                    if (this.ShowOnlyRuleBackedHotspots)
                    {
                        filtered = filtered.Where(p => associationSet.Contains(NormalizeExecutableName(p.ProcessName)));
                    }

                    if (this.ShowOnlyActionableHotspots)
                    {
                        filtered = filtered.Where(p => p.CpuUsage >= 1.0 || p.MemoryUsage >= (200L * 1024 * 1024));
                    }

                    filtered = this.SortMode switch
                    {
                        "Memory" => filtered.OrderByDescending(p => p.MemoryUsage),
                        "Name" => filtered.OrderBy(p => p.ProcessName),
                        _ => filtered.OrderByDescending(p => p.CpuUsage),
                    };

                    var snapshot = filtered.Take(50).ToList();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        this.TopCpuProcesses = new ObservableCollection<ProcessPerformanceInfo>(snapshot);

                        if (this.SelectedHotspotProcess != null)
                        {
                            var refreshedSelection = this.TopCpuProcesses.FirstOrDefault(p => p.ProcessId == this.SelectedHotspotProcess.ProcessId);
                            if (refreshedSelection != null)
                            {
                                this.SelectedHotspotProcess = refreshedSelection;
                            }
                        }
                    });

                    await this.RefreshSelectedProcessRuleImpactAsync();
                }
                while (this.pendingTopProcessRefresh);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error loading hotspot process lists");
            }
            finally
            {
                this.topProcessRefreshGate.Release();
            }
        }

        private async Task RefreshGlobalPowerPlanAsync()
        {
            try
            {
                var activePlan = await this.powerPlanService.GetActivePowerPlan();
                this.CurrentGlobalPowerPlanText = activePlan?.Name ?? "Unknown";
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to refresh global power plan text");
                this.CurrentGlobalPowerPlanText = "Unknown";
            }
        }

        private void OnMetricsUpdated(object? sender, PerformanceMetricsUpdatedEventArgs e)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    this.UpdateMetrics(e.Metrics);
                    _ = this.LoadTopProcessesAsync();
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error updating performance metrics in UI");
            }
        }

        private void UpdateMetrics(SystemPerformanceMetrics metrics)
        {
            this.TotalCpuUsage = metrics.TotalCpuUsage;
            this.TotalMemoryUsage = metrics.TotalMemoryUsage;
            this.AvailableMemory = metrics.AvailableMemory;
            this.TotalMemory = metrics.TotalMemory;
            this.MemoryUsagePercentage = metrics.MemoryUsagePercentage;
            this.ActiveProcessCount = metrics.ActiveProcessCount;
            this.LastUpdateTime = metrics.Timestamp;

            this.CpuUsageText = $"{this.TotalCpuUsage:F1}%";
            this.MemoryUsageText = $"{FormatBytes(this.TotalMemoryUsage)} / {FormatBytes(this.TotalMemory)}";
            this.ProcessCountText = this.ActiveProcessCount.ToString();

            void Apply()
            {
                this.CoreUsages = new ObservableCollection<CpuCoreUsage>(metrics.CpuCoreUsages);

                if (this.IsMonitoring)
                {
                    this.HistoricalData.Add(metrics);
                    while (this.HistoricalData.Count > 360)
                    {
                        this.HistoricalData.RemoveAt(0);
                    }
                }

                this.UpdateTimelineSummary();
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }

        private void OnProcessPowerPlanChanged(object? sender, ProcessPowerPlanChangeEventArgs e)
        {
            try
            {
                var executable = NormalizeExecutableName(e.Process.Name);
                this.lastRuleApplyByExecutable[executable] = e.Timestamp;

                var detail = $"{e.Action}: {e.Process.Name} -> {e.NewPowerPlan?.Name ?? "Unknown"}";
                this.AddTimelineEvent("Rule Applied", detail, "Success");

                _ = this.RefreshSelectedProcessRuleImpactAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed handling process power plan change event");
            }
        }

        private void OnPowerPlanChanged(object? sender, PowerPlanChangedEventArgs e)
        {
            var detail = $"Global plan changed to {e.NewPowerPlan?.Name ?? "Unknown"}";
            this.AddTimelineEvent("Power Plan", detail, "Info");
            this.CurrentGlobalPowerPlanText = e.NewPowerPlan?.Name ?? "Unknown";
        }

        private void OnTweakStatusChanged(object? sender, TweakStatusChangedEventArgs e)
        {
            var state = e.Status.IsEnabled ? "enabled" : "disabled";
            this.AddTimelineEvent("Tweak", $"{e.TweakName} {state}", "Warning");
        }

        private void AddTimelineEvent(string category, string detail, string severity)
        {
            var evt = new PerformanceTimelineEvent
            {
                Category = category,
                Detail = detail,
                Severity = severity,
                Timestamp = DateTime.Now,
            };

            void Apply()
            {
                this.TimelineEvents.Insert(0, evt);
                while (this.TimelineEvents.Count > 200)
                {
                    this.TimelineEvents.RemoveAt(this.TimelineEvents.Count - 1);
                }

                this.UpdateTimelineSummary();
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }

        private void UpdateTimelineSummary()
        {
            this.TimelineSampleCountText = $"{this.HistoricalData.Count} samples";
            if (this.TimelineEvents.Count == 0)
            {
                this.LastTimelineEventText = "No events yet";
                return;
            }

            var latest = this.TimelineEvents[0];
            this.LastTimelineEventText = $"{latest.Timestamp:HH:mm:ss} - {latest.Category}";
        }

        private static string BuildRuleSummary(ProcessPowerPlanAssociation association)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(association.PowerPlanName))
            {
                parts.Add($"Plan: {association.PowerPlanName}");
            }

            if (!string.IsNullOrWhiteSpace(association.CoreMaskName))
            {
                parts.Add($"Mask: {association.CoreMaskName}");
            }

            if (!string.IsNullOrWhiteSpace(association.ProcessPriority))
            {
                parts.Add($"Priority: {association.ProcessPriority}");
            }

            return parts.Count == 0
                ? "Rule exists but has no advanced affinity/priority settings."
                : string.Join(" | ", parts);
        }

        private static string NormalizeExecutableName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return System.IO.Path.GetFileNameWithoutExtension(name.Trim());
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 MB";
            }

            const double kb = 1024d;
            const double mb = kb * 1024d;
            const double gb = mb * 1024d;

            if (bytes >= gb)
            {
                return $"{bytes / gb:F2} GB";
            }

            return $"{bytes / mb:F0} MB";
        }

        protected override void OnDispose()
        {
            this.performanceService.MetricsUpdated -= this.OnMetricsUpdated;
            this.processMonitorManagerService.ProcessPowerPlanChanged -= this.OnProcessPowerPlanChanged;
            this.powerPlanService.PowerPlanChanged -= this.OnPowerPlanChanged;
            this.systemTweaksService.TweakStatusChanged -= this.OnTweakStatusChanged;

            this.topProcessRefreshGate.Dispose();

            if (this.IsMonitoring)
            {
                _ = Task.Run(async () => await this.performanceService.StopMonitoringAsync());
            }

            base.OnDispose();
        }
    }

    public class PerformanceTimelineEvent
    {
        public DateTime Timestamp { get; set; }

        public string Category { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Severity { get; set; } = "Info";
    }
}
