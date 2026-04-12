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
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32;
    using ThreadPilot.Models;
    using ThreadPilot.Services;
    using ThreadPilot.ViewModels;

    public partial class ProcessPowerPlanAssociationViewModel : BaseViewModel
    {
        private readonly IProcessPowerPlanAssociationService associationService;
        private readonly IPowerPlanService powerPlanService;
        private readonly IProcessService processService;
        private readonly IProcessMonitorManagerService monitorManagerService;
        private readonly ICoreMaskService coreMaskService;

        [ObservableProperty]
        private ObservableCollection<ProcessPowerPlanAssociation> associations = new();

        [ObservableProperty]
        private ObservableCollection<PowerPlanModel> availablePowerPlans = new();

        [ObservableProperty]
        private ObservableCollection<CoreMask> availableCoreMasks = new();

        [ObservableProperty]
        private ObservableCollection<string> availablePriorities = new()
        {
            "Idle",
            "BelowNormal",
            "Normal",
            "AboveNormal",
            "High",
            "RealTime",
        };

        [ObservableProperty]
        private ObservableCollection<ProcessModel> runningProcesses = new();

        [ObservableProperty]
        private ProcessPowerPlanAssociation? selectedAssociation;

        [ObservableProperty]
        private PowerPlanModel? selectedPowerPlan;

        [ObservableProperty]
        private CoreMask? selectedCoreMask;

        [ObservableProperty]
        private string? selectedProcessPriority;

        [ObservableProperty]
        private ProcessModel? selectedProcess;

        [ObservableProperty]
        private string newExecutableName = string.Empty;

        [ObservableProperty]
        private string newExecutablePath = string.Empty;

        // Properties for the selected executable (read-only display)
        [ObservableProperty]
        private string selectedExecutableDisplayName = "No executable selected";

        [ObservableProperty]
        private string selectedExecutableFullPath = string.Empty;

        [ObservableProperty]
        private bool hasSelectedExecutable = false;

        [ObservableProperty]
        private bool matchByPath = false;

        [ObservableProperty]
        private int priority = 0;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private PowerPlanModel? defaultPowerPlan;

        [ObservableProperty]
        private bool isMonitoringEnabled = true;

        [ObservableProperty]
        private bool isEventBasedMonitoringEnabled = true;

        [ObservableProperty]
        private bool isFallbackPollingEnabled = true;

        [ObservableProperty]
        private int pollingIntervalSeconds = 5;

        [ObservableProperty]
        private bool preventDuplicatePowerPlanChanges = true;

        [ObservableProperty]
        private int powerPlanChangeDelayMs = 1000;

        [ObservableProperty]
        private string serviceStatus = "Stopped";

        [ObservableProperty]
        private bool isServiceRunning = false;

        public ProcessPowerPlanAssociationViewModel(
            ILogger<ProcessPowerPlanAssociationViewModel> logger,
            IProcessPowerPlanAssociationService associationService,
            IPowerPlanService powerPlanService,
            IProcessService processService,
            IProcessMonitorManagerService monitorManagerService,
            ICoreMaskService coreMaskService,
            IEnhancedLoggingService? enhancedLoggingService = null)
            : base(logger, enhancedLoggingService)
        {
            this.associationService = associationService ?? throw new ArgumentNullException(nameof(associationService));
            this.powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
            this.processService = processService ?? throw new ArgumentNullException(nameof(processService));
            this.monitorManagerService = monitorManagerService ?? throw new ArgumentNullException(nameof(monitorManagerService));
            this.coreMaskService = coreMaskService ?? throw new ArgumentNullException(nameof(coreMaskService));

            // Subscribe to events
            this.associationService.ConfigurationChanged += this.OnConfigurationChanged;
            this.monitorManagerService.ServiceStatusChanged += this.OnServiceStatusChanged;
            this.monitorManagerService.ProcessPowerPlanChanged += this.OnProcessPowerPlanChanged;

            // Initialize
            _ = this.InitializeAsync();
        }

        public override async Task InitializeAsync()
        {
            await this.LoadDataAsync();
            this.UpdateServiceStatus();
        }

        partial void OnSelectedAssociationChanged(ProcessPowerPlanAssociation? value)
        {
            if (value == null)
            {
                return;
            }

            PopulateEditorFromAssociation(value);
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                this.SetStatus("Loading data...");

                // Load associations
                var associationsData = await this.associationService.GetAssociationsAsync();
                this.Associations = new ObservableCollection<ProcessPowerPlanAssociation>(associationsData);

                // Load power plans
                var powerPlans = await this.powerPlanService.GetPowerPlansAsync();
                this.AvailablePowerPlans = powerPlans;

                // Load core masks
                await this.coreMaskService.InitializeAsync();
                this.AvailableCoreMasks = this.coreMaskService.AvailableMasks;

                // Load running processes
                var processes = await this.processService.GetProcessesAsync();
                this.RunningProcesses = processes;

                // Load configuration settings
                var config = this.associationService.Configuration;
                this.IsEventBasedMonitoringEnabled = config.IsEventBasedMonitoringEnabled;
                this.IsFallbackPollingEnabled = config.IsFallbackPollingEnabled;
                this.PollingIntervalSeconds = config.PollingIntervalSeconds;
                this.PreventDuplicatePowerPlanChanges = config.PreventDuplicatePowerPlanChanges;
                this.PowerPlanChangeDelayMs = config.PowerPlanChangeDelayMs;

                // Load default power plan
                var (defaultGuid, defaultName) = await this.associationService.GetDefaultPowerPlanAsync();
                this.DefaultPowerPlan = this.AvailablePowerPlans.FirstOrDefault(p => p.Guid == defaultGuid);

                this.ClearStatus();
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error loading data: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task AddAssociationAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(this.NewExecutableName) || this.SelectedPowerPlan == null)
                {
                    this.SetStatus("Please select an executable and a power plan", false);
                    return;
                }

                this.SetStatus("Adding association...");

                var association = new ProcessPowerPlanAssociation
                {
                    ExecutableName = this.NewExecutableName.Trim(),
                    ExecutablePath = this.NewExecutablePath.Trim(),
                    PowerPlanGuid = this.SelectedPowerPlan.Guid,
                    PowerPlanName = this.SelectedPowerPlan.Name,
                    CoreMaskId = this.SelectedCoreMask?.Id,
                    CoreMaskName = this.SelectedCoreMask?.Name,
                    ProcessPriority = this.SelectedProcessPriority,
                    MatchByPath = this.MatchByPath,
                    Priority = this.Priority,
                    Description = this.Description.Trim(),
                    IsEnabled = true,
                };

                var success = await this.associationService.AddAssociationAsync(association);
                if (success)
                {
                    // Clear form
                    this.NewExecutableName = string.Empty;
                    this.NewExecutablePath = string.Empty;
                    this.SelectedPowerPlan = null;
                    this.SelectedCoreMask = null;
                    this.SelectedProcessPriority = null;
                    this.MatchByPath = false;
                    this.Priority = 0;
                    this.Description = string.Empty;
                    this.SelectedAssociation = null;

                    await this.LoadDataAsync();
                    this.SetStatus("Rule created and applied successfully.", false);
                }
                else
                {
                    this.SetStatus("Failed to add rule - it may already exist", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error adding rule: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task UpdateAssociationAsync()
        {
            try
            {
                if (this.SelectedAssociation == null)
                {
                    this.SetStatus("Please select a rule to update", false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(this.NewExecutableName) || this.SelectedPowerPlan == null)
                {
                    this.SetStatus("Executable and power plan are required", false);
                    return;
                }

                this.SetStatus("Updating rule...");

                this.ApplyEditorToAssociation(this.SelectedAssociation);

                var success = await this.associationService.UpdateAssociationAsync(this.SelectedAssociation);
                if (success)
                {
                    await this.LoadDataAsync();
                    this.SetStatus("Rule updated and applied successfully.", false);
                }
                else
                {
                    this.SetStatus("Failed to update rule", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error updating rule: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task RemoveAssociationAsync()
        {
            try
            {
                if (this.SelectedAssociation == null)
                {
                    this.SetStatus("Please select a rule to remove", false);
                    return;
                }

                this.SetStatus("Removing rule...");

                var success = await this.associationService.RemoveAssociationAsync(this.SelectedAssociation.Id);
                if (success)
                {
                    this.SelectedAssociation = null;
                    await this.LoadDataAsync();
                    this.SetStatus("Rule removed successfully");
                }
                else
                {
                    this.SetStatus("Failed to remove rule", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error removing rule: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task SetDefaultPowerPlanAsync()
        {
            try
            {
                if (this.DefaultPowerPlan == null)
                {
                    this.SetStatus("Please select a default power plan", false);
                    return;
                }

                this.SetStatus("Setting default power plan...");

                var success = await this.associationService.SetDefaultPowerPlanAsync(this.DefaultPowerPlan.Guid, this.DefaultPowerPlan.Name);
                if (success)
                {
                    this.SetStatus("Default power plan set successfully");
                }
                else
                {
                    this.SetStatus("Failed to set default power plan", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error setting default power plan: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task StartMonitoringAsync()
        {
            try
            {
                this.SetStatus("Starting monitoring service...");
                await this.monitorManagerService.StartAsync();
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error starting monitoring: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task StopMonitoringAsync()
        {
            try
            {
                this.SetStatus("Stopping monitoring service...");
                await this.monitorManagerService.StopAsync();
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error stopping monitoring: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public async Task SaveConfigurationAsync()
        {
            try
            {
                this.SetStatus("Saving configuration...");

                // Update configuration with current settings
                var config = this.associationService.Configuration;
                config.IsEventBasedMonitoringEnabled = this.IsEventBasedMonitoringEnabled;
                config.IsFallbackPollingEnabled = this.IsFallbackPollingEnabled;
                config.PollingIntervalSeconds = this.PollingIntervalSeconds;
                config.PreventDuplicatePowerPlanChanges = this.PreventDuplicatePowerPlanChanges;
                config.PowerPlanChangeDelayMs = this.PowerPlanChangeDelayMs;

                var success = await this.associationService.SaveConfigurationAsync();
                if (success)
                {
                    this.SetStatus("Configuration saved and active.", false);
                }
                else
                {
                    this.SetStatus("Failed to save configuration", false);
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error saving configuration: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public void UseSelectedProcessForAssociation()
        {
            if (this.SelectedProcess != null)
            {
                this.NewExecutableName = this.SelectedProcess.Name;
                this.NewExecutablePath = this.SelectedProcess.ExecutablePath;

                // Update the selected executable display
                this.UpdateSelectedExecutableDisplay(this.SelectedProcess.ExecutablePath, this.SelectedProcess.Name);
            }
        }

        [RelayCommand]
        public void BrowseExecutable()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Executable File",
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    Multiselect = false,
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var selectedFilePath = openFileDialog.FileName;

                    // Validate that it's an executable file
                    if (!this.IsValidExecutable(selectedFilePath))
                    {
                        this.SetStatus("Selected file is not a valid executable", false);
                        return;
                    }

                    // Extract executable name from the full path
                    var executableName = Path.GetFileName(selectedFilePath);

                    // Auto-populate the fields
                    this.NewExecutableName = executableName;
                    this.NewExecutablePath = selectedFilePath;

                    // Update the display
                    this.UpdateSelectedExecutableDisplay(selectedFilePath, executableName);

                    this.SetStatus($"Selected executable: {executableName}");
                }
            }
            catch (Exception ex)
            {
                this.SetStatus($"Error selecting executable: {ex.Message}", false);
            }
        }

        [RelayCommand]
        public void ClearSelectedExecutable()
        {
            this.NewExecutableName = string.Empty;
            this.NewExecutablePath = string.Empty;
            this.SelectedExecutableDisplayName = "No executable selected";
            this.SelectedExecutableFullPath = string.Empty;
            this.HasSelectedExecutable = false;
            this.SetStatus("Executable selection cleared");
        }

        private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            // Reload data when configuration changes - marshal to UI thread to prevent cross-thread access exceptions
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await this.LoadDataAsync());
        }

        private void OnServiceStatusChanged(object? sender, ServiceStatusEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                this.ServiceStatus = e.Status;
                this.IsServiceRunning = e.IsRunning;
            });
        }

        private bool IsValidExecutable(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return false;
                }

                var extension = Path.GetExtension(filePath);
                return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private void UpdateSelectedExecutableDisplay(string fullPath, string executableName)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                this.SelectedExecutableDisplayName = "No executable selected";
                this.SelectedExecutableFullPath = string.Empty;
                this.HasSelectedExecutable = false;
            }
            else
            {
                this.SelectedExecutableDisplayName = executableName;
                this.SelectedExecutableFullPath = fullPath;
                this.HasSelectedExecutable = true;
            }
        }

        private void PopulateEditorFromAssociation(ProcessPowerPlanAssociation association)
        {
            this.NewExecutableName = association.ExecutableName ?? string.Empty;
            this.NewExecutablePath = association.ExecutablePath ?? string.Empty;
            this.MatchByPath = association.MatchByPath;
            this.Priority = association.Priority;
            this.Description = association.Description ?? string.Empty;
            this.SelectedProcessPriority = association.ProcessPriority;

            this.SelectedPowerPlan = this.AvailablePowerPlans.FirstOrDefault(p =>
                string.Equals(p.Guid, association.PowerPlanGuid, StringComparison.OrdinalIgnoreCase));

            this.SelectedCoreMask = this.AvailableCoreMasks.FirstOrDefault(m =>
                string.Equals(m.Id, association.CoreMaskId, StringComparison.Ordinal));

            this.UpdateSelectedExecutableDisplay(
                this.NewExecutablePath,
                string.IsNullOrWhiteSpace(this.NewExecutableName)
                    ? Path.GetFileName(this.NewExecutablePath)
                    : this.NewExecutableName);
        }

        private void ApplyEditorToAssociation(ProcessPowerPlanAssociation association)
        {
            association.ExecutableName = this.NewExecutableName.Trim();
            association.ExecutablePath = this.NewExecutablePath.Trim();
            association.MatchByPath = this.MatchByPath;
            association.Priority = this.Priority;
            association.Description = this.Description.Trim();
            association.ProcessPriority = this.SelectedProcessPriority;
            association.PowerPlanGuid = this.SelectedPowerPlan?.Guid ?? string.Empty;
            association.PowerPlanName = this.SelectedPowerPlan?.Name ?? string.Empty;
            association.CoreMaskId = this.SelectedCoreMask?.Id;
            association.CoreMaskName = this.SelectedCoreMask?.Name;
            association.UpdatedAt = DateTime.UtcNow;
        }

        private void OnProcessPowerPlanChanged(object? sender, ProcessPowerPlanChangeEventArgs e)
        {
            // Marshal UI updates to the UI thread to prevent cross-thread access exceptions
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Update status when power plan changes occur
                this.SetStatus($"Power plan changed: {e.NewPowerPlan?.Name} for {e.Process.Name}");
            });
        }

        private void UpdateServiceStatus()
        {
            this.ServiceStatus = this.monitorManagerService.Status;
            this.IsServiceRunning = this.monitorManagerService.IsRunning;
        }
    }
}

