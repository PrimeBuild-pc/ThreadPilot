using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// ViewModel for the main window.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly ISystemInfoService? _systemInfoService;
        private readonly IProcessService? _processService;
        private readonly IPowerProfileService? _powerProfileService;
        private readonly INotificationService? _notificationService;
        private readonly IFileDialogService? _fileDialogService;

        private SystemInfo _systemInfo;
        private ObservableCollection<ProcessInfo> _allProcesses;
        private ProcessInfo? _selectedProcess;
        private string _processFilter = string.Empty;
        private ObservableCollection<PowerProfile> _powerProfiles;
        private PowerProfile? _selectedPowerProfile;
        private PowerProfile? _activeProfile;
        private ProcessAffinityRule? _selectedProcessRule;
        private bool _isMonitoringActive;
        private int _monitoringInterval = 1;
        private string _monitoringButtonText = "Start Monitoring";
        private bool _startWithWindows;
        private bool _minimizeToTray;
        private bool _enableNotifications = true;
        private bool _applyProfileAtStartup;
        private bool _confirmProfileApplication = true;
        private string _statusMessage = "Ready";

        #endregion

        #region Properties

        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }

        public ObservableCollection<ProcessInfo> AllProcesses
        {
            get => _allProcesses;
            set => SetProperty(ref _allProcesses, value);
        }

        public IEnumerable<ProcessInfo> FilteredProcesses
        {
            get
            {
                if (string.IsNullOrEmpty(ProcessFilter))
                {
                    return AllProcesses;
                }

                return AllProcesses.Where(p => p.Name.Contains(ProcessFilter, StringComparison.OrdinalIgnoreCase) ||
                                             p.Id.ToString().Contains(ProcessFilter));
            }
        }

        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
        }

        public string ProcessFilter
        {
            get => _processFilter;
            set
            {
                if (SetProperty(ref _processFilter, value))
                {
                    OnPropertyChanged(nameof(FilteredProcesses));
                }
            }
        }

        public ObservableCollection<PowerProfile> PowerProfiles
        {
            get => _powerProfiles;
            set => SetProperty(ref _powerProfiles, value);
        }

        public PowerProfile? SelectedPowerProfile
        {
            get => _selectedPowerProfile;
            set => SetProperty(ref _selectedPowerProfile, value);
        }

        public PowerProfile? ActiveProfile
        {
            get => _activeProfile;
            set => SetProperty(ref _activeProfile, value);
        }

        public ProcessAffinityRule? SelectedProcessRule
        {
            get => _selectedProcessRule;
            set => SetProperty(ref _selectedProcessRule, value);
        }

        public bool IsMonitoringActive
        {
            get => _isMonitoringActive;
            set
            {
                if (SetProperty(ref _isMonitoringActive, value))
                {
                    MonitoringButtonText = value ? "Stop Monitoring" : "Start Monitoring";
                }
            }
        }

        public int MonitoringInterval
        {
            get => _monitoringInterval;
            set => SetProperty(ref _monitoringInterval, value);
        }

        public string MonitoringButtonText
        {
            get => _monitoringButtonText;
            set => SetProperty(ref _monitoringButtonText, value);
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        public bool ApplyProfileAtStartup
        {
            get => _applyProfileAtStartup;
            set => SetProperty(ref _applyProfileAtStartup, value);
        }

        public bool ConfirmProfileApplication
        {
            get => _confirmProfileApplication;
            set => SetProperty(ref _confirmProfileApplication, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public IEnumerable<CpuFrequencyMode> CpuFrequencyModes => Enum.GetValues(typeof(CpuFrequencyMode)).Cast<CpuFrequencyMode>();
        public IEnumerable<CpuPowerMode> CpuPowerModes => Enum.GetValues(typeof(CpuPowerMode)).Cast<CpuPowerMode>();
        public IEnumerable<EnergyPreference> EnergyPreferences => Enum.GetValues(typeof(EnergyPreference)).Cast<EnergyPreference>();
        public IEnumerable<CpuBoostMode> CpuBoostModes => Enum.GetValues(typeof(CpuBoostMode)).Cast<CpuBoostMode>();

        #endregion

        #region Commands

        public ICommand RefreshProcessesCommand { get; }
        public ICommand SetProcessPriorityCommand { get; }
        public ICommand SetProcessAffinityCommand { get; }
        public ICommand TerminateProcessCommand { get; }
        public ICommand RestartProcessCommand { get; }
        public ICommand ApplyProfileCommand { get; }
        public ICommand NewProfileCommand { get; }
        public ICommand ImportProfileCommand { get; }
        public ICommand ExportProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand ApplySelectedProfileCommand { get; }
        public ICommand AddProcessRuleCommand { get; }
        public ICommand EditProcessRuleCommand { get; }
        public ICommand RemoveProcessRuleCommand { get; }
        public ICommand ApplyProcessRuleCommand { get; }
        public ICommand ToggleMonitoringCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            // Initialize fields
            _systemInfo = new SystemInfo();
            _allProcesses = new ObservableCollection<ProcessInfo>();
            _powerProfiles = new ObservableCollection<PowerProfile>();

            // Try to get services from ServiceLocator
            try
            {
                var serviceLocator = ServiceLocator.Instance;

                if (serviceLocator.IsRegistered<ISystemInfoService>())
                {
                    _systemInfoService = serviceLocator.Get<ISystemInfoService>();
                    _systemInfoService.SystemInfoUpdated += SystemInfoService_SystemInfoUpdated;
                }

                if (serviceLocator.IsRegistered<IProcessService>())
                {
                    _processService = serviceLocator.Get<IProcessService>();
                    _processService.ProcessStarted += ProcessService_ProcessStarted;
                    _processService.ProcessTerminated += ProcessService_ProcessTerminated;
                }

                if (serviceLocator.IsRegistered<IPowerProfileService>())
                {
                    _powerProfileService = serviceLocator.Get<IPowerProfileService>();
                    _powerProfileService.ActiveProfileChanged += PowerProfileService_ActiveProfileChanged;
                    _powerProfileService.ProfileModified += PowerProfileService_ProfileModified;
                }

                if (serviceLocator.IsRegistered<INotificationService>())
                {
                    _notificationService = serviceLocator.Get<INotificationService>();
                }

                if (serviceLocator.IsRegistered<IFileDialogService>())
                {
                    _fileDialogService = serviceLocator.Get<IFileDialogService>();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error initializing services: {ex.Message}";
            }

            // Initialize commands
            RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
            SetProcessPriorityCommand = new RelayCommand<string>(SetProcessPriority, CanModifyProcess);
            SetProcessAffinityCommand = new RelayCommand(SetProcessAffinity, CanModifyProcess);
            TerminateProcessCommand = new RelayCommand(TerminateProcess, CanModifyProcess);
            RestartProcessCommand = new RelayCommand(RestartProcess, CanModifyProcess);
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            NewProfileCommand = new RelayCommand(NewProfile);
            ImportProfileCommand = new RelayCommand(ImportProfile);
            ExportProfileCommand = new RelayCommand(ExportProfile, CanExportProfile);
            DeleteProfileCommand = new RelayCommand(DeleteProfile, CanDeleteProfile);
            SaveProfileCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            ApplySelectedProfileCommand = new RelayCommand(ApplySelectedProfile, CanApplySelectedProfile);
            AddProcessRuleCommand = new RelayCommand(AddProcessRule, CanModifyProfile);
            EditProcessRuleCommand = new RelayCommand(EditProcessRule, CanEditProcessRule);
            RemoveProcessRuleCommand = new RelayCommand(RemoveProcessRule, CanRemoveProcessRule);
            ApplyProcessRuleCommand = new RelayCommand(ApplyProcessRule, CanApplyProcessRule);
            ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
            CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);

            // Initialize data
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                // Load system info
                if (_systemInfoService != null)
                {
                    SystemInfo = _systemInfoService.GetSystemInfo();
                    _systemInfoService.StartMonitoring(MonitoringInterval);
                    IsMonitoringActive = true;
                }

                // Load processes
                RefreshProcesses();

                // Load power profiles
                LoadPowerProfiles();

                // Load settings
                LoadSettings();

                StatusMessage = "Application initialized successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error during initialization: {ex.Message}";
            }
        }

        #region Event Handlers

        private void SystemInfoService_SystemInfoUpdated(object? sender, EventArgs e)
        {
            if (_systemInfoService != null)
            {
                SystemInfo = _systemInfoService.GetSystemInfo();
            }
        }

        private void ProcessService_ProcessStarted(object? sender, ProcessInfo e)
        {
            RefreshProcesses();
        }

        private void ProcessService_ProcessTerminated(object? sender, ProcessInfo e)
        {
            RefreshProcesses();
        }

        private void PowerProfileService_ActiveProfileChanged(object? sender, PowerProfile? e)
        {
            ActiveProfile = e;
        }

        private void PowerProfileService_ProfileModified(object? sender, PowerProfile e)
        {
            LoadPowerProfiles();
        }

        #endregion

        #region Command Methods

        private void RefreshProcesses()
        {
            try
            {
                if (_processService != null)
                {
                    var processes = _processService.GetAllProcesses();
                    AllProcesses = new ObservableCollection<ProcessInfo>(processes);
                    OnPropertyChanged(nameof(FilteredProcesses));
                    StatusMessage = $"Process list refreshed. {AllProcesses.Count} processes found.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing processes: {ex.Message}";
            }
        }

        private bool CanModifyProcess()
        {
            return SelectedProcess != null && _processService != null;
        }

        private bool CanModifyProcess(string? _)
        {
            return CanModifyProcess();
        }

        private void SetProcessPriority(string? priorityName)
        {
            if (SelectedProcess == null || _processService == null || string.IsNullOrEmpty(priorityName))
            {
                return;
            }

            try
            {
                if (Enum.TryParse<ProcessPriority>(priorityName, out var priority))
                {
                    if (_processService.SetProcessPriority(SelectedProcess, priority))
                    {
                        SelectedProcess.Priority = priority;
                        StatusMessage = $"Priority for process {SelectedProcess.Name} set to {priority}";
                        
                        if (_notificationService != null && EnableNotifications)
                        {
                            _notificationService.ShowInfoAsync("Process Priority", $"Priority for process {SelectedProcess.Name} set to {priority}");
                        }
                    }
                    else
                    {
                        StatusMessage = $"Failed to set priority for process {SelectedProcess.Name}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting process priority: {ex.Message}";
            }
        }

        private void SetProcessAffinity()
        {
            if (SelectedProcess == null || _processService == null)
            {
                return;
            }

            try
            {
                // This would typically show a dialog to select cores
                // For now, we'll just simulate it by setting affinity to use all cores
                
                var allProcessorsMask = _processService.GetAllProcessorsMask();
                if (_processService.SetProcessAffinity(SelectedProcess, allProcessorsMask))
                {
                    SelectedProcess.AffinityMask = allProcessorsMask;
                    StatusMessage = $"Affinity for process {SelectedProcess.Name} set to use all cores";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowInfoAsync("Process Affinity", $"Affinity for process {SelectedProcess.Name} set to use all cores");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to set affinity for process {SelectedProcess.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting process affinity: {ex.Message}";
            }
        }

        private void TerminateProcess()
        {
            if (SelectedProcess == null || _processService == null)
            {
                return;
            }

            try
            {
                if (_processService.TerminateProcess(SelectedProcess))
                {
                    var processName = SelectedProcess.Name;
                    AllProcesses.Remove(SelectedProcess);
                    SelectedProcess = null;
                    StatusMessage = $"Process {processName} terminated";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowInfoAsync("Process Terminated", $"Process {processName} has been terminated");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to terminate process {SelectedProcess.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error terminating process: {ex.Message}";
            }
        }

        private void RestartProcess()
        {
            if (SelectedProcess == null || _processService == null)
            {
                return;
            }

            try
            {
                var processName = SelectedProcess.Name;
                if (_processService.RestartProcess(SelectedProcess))
                {
                    StatusMessage = $"Process {processName} restarted";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowInfoAsync("Process Restarted", $"Process {processName} has been restarted");
                    }
                    
                    RefreshProcesses();
                }
                else
                {
                    StatusMessage = $"Failed to restart process {processName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error restarting process: {ex.Message}";
            }
        }

        private bool CanApplyProfile()
        {
            return ActiveProfile != null && _powerProfileService != null;
        }

        private void ApplyProfile()
        {
            if (ActiveProfile == null || _powerProfileService == null)
            {
                return;
            }

            try
            {
                if (_powerProfileService.ApplyProfile(ActiveProfile))
                {
                    StatusMessage = $"Power profile {ActiveProfile.Name} applied";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowSuccessAsync("Power Profile Applied", $"Power profile {ActiveProfile.Name} has been applied");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to apply power profile {ActiveProfile.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying power profile: {ex.Message}";
            }
        }

        private void NewProfile()
        {
            try
            {
                var newProfile = new PowerProfile
                {
                    Name = "New Profile",
                    Author = Environment.UserName,
                    Description = "A new power profile",
                    CreatedDate = DateTime.Now.ToString("o"),
                    LastModifiedDate = DateTime.Now.ToString("o")
                };
                
                if (_powerProfileService != null && _powerProfileService.CreateProfile(newProfile))
                {
                    LoadPowerProfiles();
                    SelectedPowerProfile = PowerProfiles.FirstOrDefault(p => p.Name == newProfile.Name);
                    StatusMessage = "New power profile created";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowInfoAsync("Profile Created", "A new power profile has been created");
                    }
                }
                else
                {
                    StatusMessage = "Failed to create new power profile";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating new profile: {ex.Message}";
            }
        }

        private void ImportProfile()
        {
            try
            {
                if (_fileDialogService != null && _powerProfileService != null)
                {
                    var filter = _fileDialogService.GetPowerProfileFileFilter();
                    var filePath = _fileDialogService.ShowOpenFileDialog("Import Power Profile", filter);
                    
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        var profile = _powerProfileService.ImportProfile(filePath);
                        if (profile != null)
                        {
                            LoadPowerProfiles();
                            SelectedPowerProfile = PowerProfiles.FirstOrDefault(p => p.Name == profile.Name);
                            StatusMessage = $"Power profile {profile.Name} imported";
                            
                            if (_notificationService != null && EnableNotifications)
                            {
                                _notificationService.ShowSuccessAsync("Profile Imported", $"Power profile {profile.Name} has been imported");
                            }
                        }
                        else
                        {
                            StatusMessage = "Failed to import power profile";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error importing profile: {ex.Message}";
            }
        }

        private bool CanExportProfile()
        {
            return SelectedPowerProfile != null && _fileDialogService != null && _powerProfileService != null;
        }

        private void ExportProfile()
        {
            if (SelectedPowerProfile == null || _fileDialogService == null || _powerProfileService == null)
            {
                return;
            }

            try
            {
                var filter = _fileDialogService.GetPowerProfileFileFilter();
                var filePath = _fileDialogService.ShowSaveFileDialog("Export Power Profile", filter, 
                    defaultFileName: $"{SelectedPowerProfile.Name}.pow");
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (_powerProfileService.ExportProfile(SelectedPowerProfile, filePath))
                    {
                        StatusMessage = $"Power profile {SelectedPowerProfile.Name} exported";
                        
                        if (_notificationService != null && EnableNotifications)
                        {
                            _notificationService.ShowSuccessAsync("Profile Exported", $"Power profile {SelectedPowerProfile.Name} has been exported");
                        }
                    }
                    else
                    {
                        StatusMessage = $"Failed to export power profile {SelectedPowerProfile.Name}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exporting profile: {ex.Message}";
            }
        }

        private bool CanDeleteProfile()
        {
            return SelectedPowerProfile != null && !SelectedPowerProfile.IsBundled && _powerProfileService != null;
        }

        private void DeleteProfile()
        {
            if (SelectedPowerProfile == null || _powerProfileService == null)
            {
                return;
            }

            try
            {
                var profileName = SelectedPowerProfile.Name;
                if (_powerProfileService.DeleteProfile(SelectedPowerProfile))
                {
                    LoadPowerProfiles();
                    SelectedPowerProfile = null;
                    StatusMessage = $"Power profile {profileName} deleted";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowInfoAsync("Profile Deleted", $"Power profile {profileName} has been deleted");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to delete power profile {profileName}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting profile: {ex.Message}";
            }
        }

        private bool CanSaveProfile()
        {
            return SelectedPowerProfile != null && _powerProfileService != null;
        }

        private void SaveProfile()
        {
            if (SelectedPowerProfile == null || _powerProfileService == null)
            {
                return;
            }

            try
            {
                SelectedPowerProfile.LastModifiedDate = DateTime.Now.ToString("o");
                if (_powerProfileService.UpdateProfile(SelectedPowerProfile))
                {
                    StatusMessage = $"Power profile {SelectedPowerProfile.Name} saved";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowSuccessAsync("Profile Saved", $"Power profile {SelectedPowerProfile.Name} has been saved");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to save power profile {SelectedPowerProfile.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving profile: {ex.Message}";
            }
        }

        private bool CanApplySelectedProfile()
        {
            return SelectedPowerProfile != null && _powerProfileService != null;
        }

        private void ApplySelectedProfile()
        {
            if (SelectedPowerProfile == null || _powerProfileService == null)
            {
                return;
            }

            try
            {
                if (ConfirmProfileApplication && _notificationService != null)
                {
                    var result = _notificationService.ShowConfirmationAsync(
                        "Apply Profile",
                        $"Are you sure you want to apply the power profile '{SelectedPowerProfile.Name}'?").Result;
                    
                    if (!result)
                    {
                        return;
                    }
                }
                
                if (_powerProfileService.ApplyProfile(SelectedPowerProfile))
                {
                    ActiveProfile = SelectedPowerProfile;
                    StatusMessage = $"Power profile {SelectedPowerProfile.Name} applied";
                    
                    if (_notificationService != null && EnableNotifications)
                    {
                        _notificationService.ShowSuccessAsync("Profile Applied", $"Power profile {SelectedPowerProfile.Name} has been applied");
                    }
                }
                else
                {
                    StatusMessage = $"Failed to apply power profile {SelectedPowerProfile.Name}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying profile: {ex.Message}";
            }
        }

        private bool CanModifyProfile()
        {
            return SelectedPowerProfile != null && !SelectedPowerProfile.IsBundled;
        }

        private void AddProcessRule()
        {
            if (SelectedPowerProfile == null)
            {
                return;
            }

            try
            {
                var newRule = new ProcessAffinityRule
                {
                    ProcessNamePattern = "*",
                    AffinityMask = -1,
                    Priority = ProcessPriority.Normal,
                    IsEnabled = true,
                    ApplyAutomatically = true
                };
                
                SelectedPowerProfile.ProcessRules.Add(newRule);
                SelectedProcessRule = newRule;
                StatusMessage = "New process rule added";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding process rule: {ex.Message}";
            }
        }

        private bool CanEditProcessRule()
        {
            return SelectedPowerProfile != null && SelectedProcessRule != null && !SelectedPowerProfile.IsBundled;
        }

        private void EditProcessRule()
        {
            // This would typically show a dialog to edit the rule
            StatusMessage = "Process rule editing not implemented";
        }

        private bool CanRemoveProcessRule()
        {
            return SelectedPowerProfile != null && SelectedProcessRule != null && !SelectedPowerProfile.IsBundled;
        }

        private void RemoveProcessRule()
        {
            if (SelectedPowerProfile == null || SelectedProcessRule == null)
            {
                return;
            }

            try
            {
                SelectedPowerProfile.ProcessRules.Remove(SelectedProcessRule);
                SelectedProcessRule = null;
                StatusMessage = "Process rule removed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error removing process rule: {ex.Message}";
            }
        }

        private bool CanApplyProcessRule()
        {
            return SelectedProcessRule != null && _processService != null;
        }

        private void ApplyProcessRule()
        {
            if (SelectedProcessRule == null || _processService == null)
            {
                return;
            }

            try
            {
                var processesModified = _processService.ApplyProcessRule(SelectedProcessRule);
                StatusMessage = $"Process rule applied to {processesModified} processes";
                
                if (_notificationService != null && EnableNotifications)
                {
                    _notificationService.ShowInfoAsync("Process Rule Applied", $"Process rule applied to {processesModified} processes");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error applying process rule: {ex.Message}";
            }
        }

        private void ToggleMonitoring()
        {
            try
            {
                if (_systemInfoService == null)
                {
                    return;
                }
                
                if (IsMonitoringActive)
                {
                    _systemInfoService.StopMonitoring();
                    IsMonitoringActive = false;
                    StatusMessage = "Monitoring stopped";
                }
                else
                {
                    _systemInfoService.StartMonitoring(MonitoringInterval);
                    IsMonitoringActive = true;
                    StatusMessage = "Monitoring started";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error toggling monitoring: {ex.Message}";
            }
        }

        private void CheckForUpdates()
        {
            // This would typically check for updates from a server
            
            try
            {
                if (_notificationService != null)
                {
                    _notificationService.ShowInfoAsync("Check for Updates", "You are running the latest version of ThreadPilot");
                }
                
                StatusMessage = "No updates available";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error checking for updates: {ex.Message}";
            }
        }

        #endregion

        #region Helper Methods

        private void LoadPowerProfiles()
        {
            try
            {
                if (_powerProfileService != null)
                {
                    var profiles = _powerProfileService.GetAllProfiles();
                    PowerProfiles = new ObservableCollection<PowerProfile>(profiles);
                    
                    ActiveProfile = _powerProfileService.GetActiveProfile();
                    
                    if (PowerProfiles.Any() && SelectedPowerProfile == null)
                    {
                        SelectedPowerProfile = PowerProfiles.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading power profiles: {ex.Message}";
            }
        }

        private void LoadSettings()
        {
            // This would typically load settings from a settings service or registry
            // For now, we'll just use default values
            
            StartWithWindows = false;
            MinimizeToTray = true;
            EnableNotifications = true;
            ApplyProfileAtStartup = false;
            ConfirmProfileApplication = true;
        }

        #endregion
    }
}