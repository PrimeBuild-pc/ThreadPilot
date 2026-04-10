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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public partial class PerformanceViewModel : BaseViewModel
    {
        private readonly IPerformanceMonitoringService _performanceService;
        private readonly IProcessService _processService;
        private readonly IProcessPowerPlanAssociationService _associationService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly IProcessMonitorManagerService _processMonitorManagerService;
        private readonly ISystemTweaksService _systemTweaksService;
        private readonly ILogger<PerformanceViewModel> _logger;

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

        private readonly Dictionary<string, DateTime> _lastRuleApplyByExecutable = new(StringComparer.OrdinalIgnoreCase);
        private bool _monitoringWasActiveBeforeSuspend;

        public PerformanceViewModel(
            IPerformanceMonitoringService performanceService,
            IProcessService processService,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            IProcessMonitorManagerService processMonitorManagerService,
            ISystemTweaksService systemTweaksService,
            ILogger<PerformanceViewModel> logger) : base(logger, null)
        {
            _performanceService = performanceService;
            _processService = processService;
            _associationService = associationService;
            _powerPlanService = powerPlanService;
            _processMonitorManagerService = processMonitorManagerService;
            _systemTweaksService = systemTweaksService;
            _logger = logger;

            _performanceService.MetricsUpdated += OnMetricsUpdated;
            _processMonitorManagerService.ProcessPowerPlanChanged += OnProcessPowerPlanChanged;
            _powerPlanService.PowerPlanChanged += OnPowerPlanChanged;
            _systemTweaksService.TweakStatusChanged += OnTweakStatusChanged;
        }

        public override async Task InitializeAsync()
        {
            try
            {
                SetStatus("Initializing performance dashboard...");

                await RefreshMetricsAsync();
                await LoadHistoricalDataAsync();
                await LoadTopProcessesAsync();
                await RefreshGlobalPowerPlanAsync();

                MonitoringStateText = IsMonitoring ? "Active" : "Stopped";
                SetStatus("Performance dashboard initialized", false);
            }
            catch (Exception ex)
            {
                SetError("Failed to initialize performance dashboard", ex);
                _logger.LogError(ex, "Error initializing performance dashboard");
            }
        }

        [RelayCommand]
        private async Task StartMonitoringAsync()
        {
            try
            {
                SetStatus("Starting performance monitoring...");
                await _performanceService.StartMonitoringAsync();

                IsMonitoring = true;
                MonitoringStatusText = "Monitoring Active";
                MonitoringStateText = "Active";
                AddTimelineEvent("Monitoring", "Real-time monitoring started.", "Info");

                SetStatus("Performance monitoring started", false);
            }
            catch (Exception ex)
            {
                SetError("Failed to start performance monitoring", ex);
            }
        }

        [RelayCommand]
        private async Task StopMonitoringAsync()
        {
            try
            {
                SetStatus("Stopping performance monitoring...");
                await _performanceService.StopMonitoringAsync();

                IsMonitoring = false;
                MonitoringStatusText = "Monitoring Stopped";
                MonitoringStateText = "Stopped";
                AddTimelineEvent("Monitoring", "Real-time monitoring stopped.", "Warning");

                SetStatus("Performance monitoring stopped", false);
            }
            catch (Exception ex)
            {
                SetError("Failed to stop performance monitoring", ex);
            }
        }

        public async Task SuspendBackgroundMonitoringAsync()
        {
            try
            {
                if (!IsMonitoring)
                {
                    _monitoringWasActiveBeforeSuspend = false;
                    return;
                }

                _monitoringWasActiveBeforeSuspend = true;
                await _performanceService.StopMonitoringAsync();

                IsMonitoring = false;
                MonitoringStatusText = "Monitoring Paused";
                MonitoringStateText = "Paused";
                AddTimelineEvent("Monitoring", "Monitoring paused while app is minimized.", "Info");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to suspend performance monitoring while minimized");
            }
        }

        public async Task ResumeBackgroundMonitoringAsync()
        {
            try
            {
                if (!_monitoringWasActiveBeforeSuspend || IsMonitoring)
                {
                    return;
                }

                await _performanceService.StartMonitoringAsync();

                IsMonitoring = true;
                MonitoringStatusText = "Monitoring Active";
                MonitoringStateText = "Active";
                AddTimelineEvent("Monitoring", "Monitoring resumed after restore.", "Info");
                _monitoringWasActiveBeforeSuspend = false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resume performance monitoring after restore");
            }
        }

        [RelayCommand]
        private async Task RefreshMetricsAsync()
        {
            try
            {
                SetStatus("Refreshing performance snapshot...");

                var metrics = await _performanceService.GetSystemMetricsAsync();
                await RefreshGlobalPowerPlanAsync();
                await LoadTopProcessesAsync();
                UpdateMetrics(metrics);

                LastManualRefreshText = $"Refreshed at {DateTime.Now:HH:mm:ss}";
                SetStatus("Performance snapshot refreshed", false);
            }
            catch (Exception ex)
            {
                SetError("Failed to refresh performance snapshot", ex);
            }
        }

        [RelayCommand]
        private async Task ClearHistoricalDataAsync()
        {
            try
            {
                await _performanceService.ClearHistoricalDataAsync();
                HistoricalData.Clear();
                UpdateTimelineSummary();
                AddTimelineEvent("History", "Historical metrics cleared.", "Info");
                SetStatus("Historical data cleared", false);
            }
            catch (Exception ex)
            {
                SetError("Failed to clear historical data", ex);
            }
        }

        [RelayCommand]
        private async Task LoadHistoricalDataAsync()
        {
            try
            {
                var history = await _performanceService.GetHistoricalDataAsync(TimeSpan.FromHours(1));
                HistoricalData = new ObservableCollection<SystemPerformanceMetrics>(history);
                UpdateTimelineSummary();
            }
            catch (Exception ex)
            {
                SetError("Failed to load historical data", ex);
            }
        }

        [RelayCommand]
        private async Task CreateRuleFromSelectedProcessAsync()
        {
            if (SelectedHotspotProcess == null || IsRuleCreateBusy)
            {
                return;
            }

            IsRuleCreateBusy = true;

            try
            {
                var liveProcesses = await _processService.GetProcessesAsync();
                var targetProcess = liveProcesses.FirstOrDefault(p => p.ProcessId == SelectedHotspotProcess.ProcessId)
                                   ?? liveProcesses.FirstOrDefault(p =>
                                       string.Equals(p.Name, SelectedHotspotProcess.ProcessName, StringComparison.OrdinalIgnoreCase));

                if (targetProcess == null)
                {
                    SetStatus("Selected hotspot process is no longer running", false);
                    return;
                }

                var activePlan = await _powerPlanService.GetActivePowerPlan();
                if (activePlan == null)
                {
                    SetStatus("Could not resolve active global power plan", false);
                    return;
                }

                var executableName = NormalizeExecutableName(targetProcess.Name);
                var existing = await _associationService.FindAssociationByExecutableAsync(executableName);

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
                        UpdatedAt = DateTime.UtcNow
                    };

                    var added = await _associationService.AddAssociationAsync(association);
                    if (!added)
                    {
                        SetStatus("A rule already exists and could not be created", false);
                        return;
                    }

                    AddTimelineEvent("Rule", $"Rule created for {executableName} from hotspot panel.", "Success");
                    SetStatus($"Rule created for {executableName} and ready for automation.", false);
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

                    var updated = await _associationService.UpdateAssociationAsync(existing);
                    if (!updated)
                    {
                        SetStatus("Failed to update existing rule from hotspot", false);
                        return;
                    }

                    AddTimelineEvent("Rule", $"Rule updated for {executableName} from hotspot panel.", "Success");
                    SetStatus($"Rule updated for {executableName} from hotspot panel.", false);
                }

                await RefreshSelectedProcessRuleImpactAsync();
                await LoadTopProcessesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create or update rule from performance hotspot");
                SetError("Failed to create rule from selected hotspot", ex);
            }
            finally
            {
                IsRuleCreateBusy = false;
            }
        }

        [RelayCommand]
        private void ToggleCoreDetails()
        {
            ShowCoreDetails = !ShowCoreDetails;
        }

        [RelayCommand]
        private void ToggleProcessDetails()
        {
            ShowProcessDetails = !ShowProcessDetails;
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

        private async Task RefreshSelectedProcessRuleImpactAsync()
        {
            try
            {
                if (SelectedHotspotProcess == null)
                {
                    SelectedProcessName = "No hotspot selected";
                    SelectedProcessExecutable = "-";
                    SelectedProcessCpuText = "-";
                    SelectedProcessMemoryText = "-";
                    SelectedProcessRuleStatus = "No linked rule";
                    SelectedProcessRuleSummary = "Create a rule from this process to automate affinity and priority behavior.";
                    SelectedProcessLastApplyText = "No recent automation event";
                    return;
                }

                var executableName = NormalizeExecutableName(SelectedHotspotProcess.ProcessName);
                var association = await _associationService.FindAssociationByExecutableAsync(executableName);

                SelectedProcessName = SelectedHotspotProcess.ProcessName;
                SelectedProcessExecutable = string.IsNullOrWhiteSpace(SelectedHotspotProcess.ExecutablePath)
                    ? executableName
                    : SelectedHotspotProcess.ExecutablePath;
                SelectedProcessCpuText = $"{SelectedHotspotProcess.CpuUsage:F1}% CPU";
                SelectedProcessMemoryText = FormatBytes(SelectedHotspotProcess.MemoryUsage);

                if (association == null)
                {
                    SelectedProcessRuleStatus = "No linked rule";
                    SelectedProcessRuleSummary = "No automation rule matches this executable yet.";
                }
                else
                {
                    SelectedProcessRuleStatus = association.IsEnabled ? "Linked rule is active" : "Linked rule is disabled";
                    SelectedProcessRuleSummary = BuildRuleSummary(association);
                }

                if (_lastRuleApplyByExecutable.TryGetValue(executableName, out var appliedAt))
                {
                    SelectedProcessLastApplyText = $"Last rule application: {appliedAt:HH:mm:ss}";
                }
                else
                {
                    SelectedProcessLastApplyText = "No recent automation event";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh rule impact panel");
            }
        }

        private async Task LoadTopProcessesAsync()
        {
            try
            {
                var topCpu = await _performanceService.GetTopCpuProcessesAsync(25);
                var topMemory = await _performanceService.GetTopMemoryProcessesAsync(25);

                var merged = topCpu
                    .Concat(topMemory)
                    .GroupBy(p => p.ProcessId)
                    .Select(g => g.OrderByDescending(x => x.CpuUsage).First())
                    .ToList();

                var associations = await _associationService.GetAssociationsAsync();
                var associationSet = associations
                    .Select(a => NormalizeExecutableName(a.ExecutableName))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                IEnumerable<ProcessPerformanceInfo> filtered = merged;

                if (!string.IsNullOrWhiteSpace(ProcessSearchText))
                {
                    filtered = filtered.Where(p => p.ProcessName.Contains(ProcessSearchText, StringComparison.OrdinalIgnoreCase));
                }

                if (ShowOnlyRuleBackedHotspots)
                {
                    filtered = filtered.Where(p => associationSet.Contains(NormalizeExecutableName(p.ProcessName)));
                }

                if (ShowOnlyActionableHotspots)
                {
                    filtered = filtered.Where(p => p.CpuUsage >= 1.0 || p.MemoryUsage >= (200L * 1024 * 1024));
                }

                filtered = SortMode switch
                {
                    "Memory" => filtered.OrderByDescending(p => p.MemoryUsage),
                    "Name" => filtered.OrderBy(p => p.ProcessName),
                    _ => filtered.OrderByDescending(p => p.CpuUsage)
                };

                TopCpuProcesses = new ObservableCollection<ProcessPerformanceInfo>(filtered.Take(50));

                if (SelectedHotspotProcess != null)
                {
                    var refreshedSelection = TopCpuProcesses.FirstOrDefault(p => p.ProcessId == SelectedHotspotProcess.ProcessId);
                    if (refreshedSelection != null)
                    {
                        SelectedHotspotProcess = refreshedSelection;
                    }
                }

                await RefreshSelectedProcessRuleImpactAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading hotspot process lists");
            }
        }

        private async Task RefreshGlobalPowerPlanAsync()
        {
            try
            {
                var activePlan = await _powerPlanService.GetActivePowerPlan();
                CurrentGlobalPowerPlanText = activePlan?.Name ?? "Unknown";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh global power plan text");
                CurrentGlobalPowerPlanText = "Unknown";
            }
        }

        private void OnMetricsUpdated(object? sender, PerformanceMetricsUpdatedEventArgs e)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateMetrics(e.Metrics);
                    _ = LoadTopProcessesAsync();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating performance metrics in UI");
            }
        }

        private void UpdateMetrics(SystemPerformanceMetrics metrics)
        {
            TotalCpuUsage = metrics.TotalCpuUsage;
            TotalMemoryUsage = metrics.TotalMemoryUsage;
            AvailableMemory = metrics.AvailableMemory;
            TotalMemory = metrics.TotalMemory;
            MemoryUsagePercentage = metrics.MemoryUsagePercentage;
            ActiveProcessCount = metrics.ActiveProcessCount;
            LastUpdateTime = metrics.Timestamp;

            CpuUsageText = $"{TotalCpuUsage:F1}%";
            MemoryUsageText = $"{FormatBytes(TotalMemoryUsage)} / {FormatBytes(TotalMemory)}";
            ProcessCountText = ActiveProcessCount.ToString();

            CoreUsages = new ObservableCollection<CpuCoreUsage>(metrics.CpuCoreUsages);

            if (IsMonitoring)
            {
                HistoricalData.Add(metrics);
                while (HistoricalData.Count > 360)
                {
                    HistoricalData.RemoveAt(0);
                }
            }

            UpdateTimelineSummary();
        }

        private void OnProcessPowerPlanChanged(object? sender, ProcessPowerPlanChangeEventArgs e)
        {
            try
            {
                var executable = NormalizeExecutableName(e.Process.Name);
                _lastRuleApplyByExecutable[executable] = e.Timestamp;

                var detail = $"{e.Action}: {e.Process.Name} -> {e.NewPowerPlan?.Name ?? "Unknown"}";
                AddTimelineEvent("Rule Applied", detail, "Success");

                _ = RefreshSelectedProcessRuleImpactAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed handling process power plan change event");
            }
        }

        private void OnPowerPlanChanged(object? sender, PowerPlanChangedEventArgs e)
        {
            var detail = $"Global plan changed to {e.NewPowerPlan?.Name ?? "Unknown"}";
            AddTimelineEvent("Power Plan", detail, "Info");
            CurrentGlobalPowerPlanText = e.NewPowerPlan?.Name ?? "Unknown";
        }

        private void OnTweakStatusChanged(object? sender, TweakStatusChangedEventArgs e)
        {
            var state = e.Status.IsEnabled ? "enabled" : "disabled";
            AddTimelineEvent("Tweak", $"{e.TweakName} {state}", "Warning");
        }

        private void AddTimelineEvent(string category, string detail, string severity)
        {
            var evt = new PerformanceTimelineEvent
            {
                Category = category,
                Detail = detail,
                Severity = severity,
                Timestamp = DateTime.Now
            };

            TimelineEvents.Insert(0, evt);
            while (TimelineEvents.Count > 200)
            {
                TimelineEvents.RemoveAt(TimelineEvents.Count - 1);
            }

            UpdateTimelineSummary();
        }

        private void UpdateTimelineSummary()
        {
            TimelineSampleCountText = $"{HistoricalData.Count} samples";
            if (TimelineEvents.Count == 0)
            {
                LastTimelineEventText = "No events yet";
                return;
            }

            var latest = TimelineEvents[0];
            LastTimelineEventText = $"{latest.Timestamp:HH:mm:ss} - {latest.Category}";
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
            _performanceService.MetricsUpdated -= OnMetricsUpdated;
            _processMonitorManagerService.ProcessPowerPlanChanged -= OnProcessPowerPlanChanged;
            _powerPlanService.PowerPlanChanged -= OnPowerPlanChanged;
            _systemTweaksService.TweakStatusChanged -= OnTweakStatusChanged;

            if (IsMonitoring)
            {
                _ = Task.Run(async () => await _performanceService.StopMonitoringAsync());
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
