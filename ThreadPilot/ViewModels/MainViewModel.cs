using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly IProcessService _processService;
        private readonly IPowerProfileService _powerProfileService;
        private readonly INotificationService _notificationService;
        private readonly IFileDialogService _fileDialogService;
        
        private readonly DispatcherTimer _refreshTimer;
        
        private bool _isAutoRefreshEnabled;
        private double _cpuUsagePercent;
        private string _memoryUsage = string.Empty;
        private double _memoryUsagePercent;
        private string _cpuName = string.Empty;
        private string _operatingSystem = string.Empty;
        private string _currentPowerPlan = string.Empty;
        private string _statusMessage = string.Empty;
        
        private PowerProfile? _selectedProfile;
        private ProcessInfo? _selectedProcess;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class
        /// </summary>
        public MainViewModel()
        {
            // Get services from ServiceLocator
            _systemInfoService = ServiceLocator.GetService<ISystemInfoService>();
            _processService = ServiceLocator.GetService<IProcessService>();
            _powerProfileService = ServiceLocator.GetService<IPowerProfileService>();
            _notificationService = ServiceLocator.GetService<INotificationService>();
            _fileDialogService = ServiceLocator.GetService<IFileDialogService>();
            
            // Initialize collections
            PowerProfiles = new ObservableCollection<PowerProfile>();
            Processes = new ObservableCollection<ProcessInfo>();
            CpuCores = new ObservableCollection<CpuCore>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshData());
            ApplyProfileCommand = new RelayCommand(_ => ApplySelectedProfile(), _ => SelectedProfile != null);
            ImportProfileCommand = new RelayCommand(_ => ImportProfile());
            ExportProfileCommand = new RelayCommand(_ => ExportSelectedProfile(), _ => SelectedProfile != null);
            CreateProfileCommand = new RelayCommand(_ => CreateNewProfile());
            DeleteProfileCommand = new RelayCommand(_ => DeleteSelectedProfile(), _ => SelectedProfile != null && !SelectedProfile.IsSystemDefault);
            SetProcessAffinityCommand = new RelayCommand(_ => SetSelectedProcessAffinity(), _ => SelectedProcess != null);
            SetProcessPriorityCommand = new RelayCommand(param => SetSelectedProcessPriority((ProcessPriority)param), _ => SelectedProcess != null);
            TerminateProcessCommand = new RelayCommand(_ => TerminateSelectedProcess(), _ => SelectedProcess != null && !SelectedProcess.IsSystemProcess);
            
            // Setup auto-refresh timer
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _refreshTimer.Tick += (s, e) => RefreshData();
            IsAutoRefreshEnabled = true;
            
            // Initial data load
            LoadProfilesAsync();
            RefreshData();
            
            StatusMessage = "Ready";
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether auto-refresh is enabled
        /// </summary>
        public bool IsAutoRefreshEnabled
        {
            get => _isAutoRefreshEnabled;
            set
            {
                if (SetProperty(ref _isAutoRefreshEnabled, value))
                {
                    if (value)
                    {
                        _refreshTimer.Start();
                    }
                    else
                    {
                        _refreshTimer.Stop();
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the CPU usage percentage
        /// </summary>
        public double CpuUsagePercent
        {
            get => _cpuUsagePercent;
            set => SetProperty(ref _cpuUsagePercent, value);
        }
        
        /// <summary>
        /// Gets or sets the memory usage string
        /// </summary>
        public string MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }
        
        /// <summary>
        /// Gets or sets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercent
        {
            get => _memoryUsagePercent;
            set => SetProperty(ref _memoryUsagePercent, value);
        }
        
        /// <summary>
        /// Gets or sets the CPU name
        /// </summary>
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }
        
        /// <summary>
        /// Gets or sets the operating system string
        /// </summary>
        public string OperatingSystem
        {
            get => _operatingSystem;
            set => SetProperty(ref _operatingSystem, value);
        }
        
        /// <summary>
        /// Gets or sets the current power plan
        /// </summary>
        public string CurrentPowerPlan
        {
            get => _currentPowerPlan;
            set => SetProperty(ref _currentPowerPlan, value);
        }
        
        /// <summary>
        /// Gets or sets the status message
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// Gets the power profiles collection
        /// </summary>
        public ObservableCollection<PowerProfile> PowerProfiles { get; }
        
        /// <summary>
        /// Gets the processes collection
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; }
        
        /// <summary>
        /// Gets the CPU cores collection
        /// </summary>
        public ObservableCollection<CpuCore> CpuCores { get; }
        
        /// <summary>
        /// Gets or sets the selected power profile
        /// </summary>
        public PowerProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    // Update commands availability
                    (ApplyProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ExportProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the selected process
        /// </summary>
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    // Update commands availability
                    (SetProcessAffinityCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SetProcessPriorityCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (TerminateProcessCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Gets the refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Gets the apply profile command
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Gets the import profile command
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Gets the export profile command
        /// </summary>
        public ICommand ExportProfileCommand { get; }
        
        /// <summary>
        /// Gets the create profile command
        /// </summary>
        public ICommand CreateProfileCommand { get; }
        
        /// <summary>
        /// Gets the delete profile command
        /// </summary>
        public ICommand DeleteProfileCommand { get; }
        
        /// <summary>
        /// Gets the set process affinity command
        /// </summary>
        public ICommand SetProcessAffinityCommand { get; }
        
        /// <summary>
        /// Gets the set process priority command
        /// </summary>
        public ICommand SetProcessPriorityCommand { get; }
        
        /// <summary>
        /// Gets the terminate process command
        /// </summary>
        public ICommand TerminateProcessCommand { get; }
        
        /// <summary>
        /// Refreshes all data
        /// </summary>
        private void RefreshData()
        {
            try
            {
                RefreshSystemInfo();
                RefreshProcesses();
                RefreshCpuCores();
                
                CurrentPowerPlan = _powerProfileService.GetCurrentPowerPlan();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing data: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Refreshes system information
        /// </summary>
        private void RefreshSystemInfo()
        {
            var systemInfo = _systemInfoService.GetSystemInfo();
            CpuName = systemInfo.CpuName;
            OperatingSystem = systemInfo.OperatingSystem;
            
            var memoryUsage = _systemInfoService.GetMemoryUsage();
            MemoryUsage = $"{memoryUsage.UsedMB:N0} MB / {memoryUsage.TotalMB:N0} MB ({memoryUsage.UsagePercent:N1}%)";
            MemoryUsagePercent = memoryUsage.UsagePercent;
            
            CpuUsagePercent = _systemInfoService.GetCpuUsagePercentage();
        }
        
        /// <summary>
        /// Refreshes the processes list
        /// </summary>
        private void RefreshProcesses()
        {
            var processes = _processService.GetRunningProcesses();
            
            // Remember the selected process ID
            int? selectedProcessId = SelectedProcess?.ProcessId;
            
            Processes.Clear();
            foreach (var process in processes.OrderByDescending(p => p.CpuUsagePercent))
            {
                Processes.Add(process);
            }
            
            // Restore the selected process if it still exists
            if (selectedProcessId.HasValue)
            {
                SelectedProcess = Processes.FirstOrDefault(p => p.ProcessId == selectedProcessId.Value);
            }
        }
        
        /// <summary>
        /// Refreshes the CPU cores information
        /// </summary>
        private void RefreshCpuCores()
        {
            var cores = _systemInfoService.GetCpuCores();
            
            CpuCores.Clear();
            foreach (var core in cores.OrderBy(c => c.CoreId))
            {
                CpuCores.Add(core);
            }
        }
        
        /// <summary>
        /// Loads all power profiles
        /// </summary>
        private async Task LoadProfilesAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var profiles = _powerProfileService.GetAllProfiles();
                    
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        PowerProfiles.Clear();
                        foreach (var profile in profiles)
                        {
                            PowerProfiles.Add(profile);
                        }
                        
                        // Select the first profile if available
                        if (PowerProfiles.Count > 0 && SelectedProfile == null)
                        {
                            SelectedProfile = PowerProfiles[0];
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading profiles: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Applies the selected profile
        /// </summary>
        private void ApplySelectedProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                StatusMessage = $"Applying profile '{SelectedProfile.Name}'...";
                
                bool success = _powerProfileService.ApplyProfile(SelectedProfile);
                
                if (success)
                {
                    _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' applied successfully.");
                    StatusMessage = $"Profile '{SelectedProfile.Name}' applied successfully.";
                    
                    // Refresh data to show changes
                    RefreshData();
                }
                else
                {
                    _notificationService.ShowError($"Failed to apply profile '{SelectedProfile.Name}'.");
                    StatusMessage = $"Failed to apply profile '{SelectedProfile.Name}'.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Imports a profile from a file
        /// </summary>
        private void ImportProfile()
        {
            try
            {
                string? filePath = _fileDialogService.ShowOpenFileDialog(
                    "Import Power Profile",
                    "Power Profile (*.pow)|*.pow|All Files (*.*)|*.*");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return; // User canceled
                }
                
                StatusMessage = $"Importing profile from {filePath}...";
                
                PowerProfile? profile = _powerProfileService.ImportProfile(filePath);
                
                if (profile != null)
                {
                    PowerProfiles.Add(profile);
                    SelectedProfile = profile;
                    
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' imported successfully.");
                    StatusMessage = $"Profile '{profile.Name}' imported successfully.";
                }
                else
                {
                    _notificationService.ShowError("Failed to import profile.");
                    StatusMessage = "Failed to import profile.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Exports the selected profile to a file
        /// </summary>
        private void ExportSelectedProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                string? filePath = _fileDialogService.ShowSaveFileDialog(
                    "Export Power Profile",
                    "Power Profile (*.pow)|*.pow|All Files (*.*)|*.*",
                    $"{SelectedProfile.Name}.pow");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return; // User canceled
                }
                
                StatusMessage = $"Exporting profile to {filePath}...";
                
                bool success = _powerProfileService.ExportProfile(SelectedProfile, filePath);
                
                if (success)
                {
                    _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' exported successfully.");
                    StatusMessage = $"Profile '{SelectedProfile.Name}' exported successfully.";
                }
                else
                {
                    _notificationService.ShowError($"Failed to export profile '{SelectedProfile.Name}'.");
                    StatusMessage = $"Failed to export profile '{SelectedProfile.Name}'.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting profile: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Creates a new power profile
        /// </summary>
        private void CreateNewProfile()
        {
            try
            {
                // Create a new default profile
                var newProfile = _powerProfileService.CreateDefaultProfile("New Profile");
                
                // Add to list and select it
                PowerProfiles.Add(newProfile);
                SelectedProfile = newProfile;
                
                StatusMessage = "New profile created. Please customize and save it.";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error creating new profile: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Deletes the selected profile
        /// </summary>
        private void DeleteSelectedProfile()
        {
            if (SelectedProfile == null || SelectedProfile.IsSystemDefault)
            {
                return;
            }
            
            try
            {
                string profileName = SelectedProfile.Name;
                
                StatusMessage = $"Deleting profile '{profileName}'...";
                
                bool success = _powerProfileService.DeleteProfile(profileName);
                
                if (success)
                {
                    PowerProfiles.Remove(SelectedProfile);
                    SelectedProfile = PowerProfiles.FirstOrDefault();
                    
                    _notificationService.ShowSuccess($"Profile '{profileName}' deleted successfully.");
                    StatusMessage = $"Profile '{profileName}' deleted successfully.";
                }
                else
                {
                    _notificationService.ShowError($"Failed to delete profile '{profileName}'.");
                    StatusMessage = $"Failed to delete profile '{profileName}'.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error deleting profile: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Sets the affinity for the selected process
        /// </summary>
        private void SetSelectedProcessAffinity()
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                // TODO: Implement affinity dialog
                StatusMessage = "Setting process affinity is not implemented yet.";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting process affinity: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Sets the priority for the selected process
        /// </summary>
        /// <param name="priority">The new priority</param>
        private void SetSelectedProcessPriority(ProcessPriority priority)
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                StatusMessage = $"Setting priority for process '{SelectedProcess.Name}' to {priority}...";
                
                bool success = _processService.SetProcessPriority(SelectedProcess.ProcessId, priority);
                
                if (success)
                {
                    // Update the process in the list
                    SelectedProcess.Priority = priority;
                    
                    _notificationService.ShowSuccess($"Priority for process '{SelectedProcess.Name}' set to {priority}.");
                    StatusMessage = $"Priority for process '{SelectedProcess.Name}' set to {priority}.";
                    
                    // Refresh to update the process list
                    RefreshProcesses();
                }
                else
                {
                    _notificationService.ShowError($"Failed to set priority for process '{SelectedProcess.Name}'.");
                    StatusMessage = $"Failed to set priority for process '{SelectedProcess.Name}'.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting process priority: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Terminates the selected process
        /// </summary>
        private void TerminateSelectedProcess()
        {
            if (SelectedProcess == null || SelectedProcess.IsSystemProcess)
            {
                return;
            }
            
            try
            {
                string processName = SelectedProcess.Name;
                int processId = SelectedProcess.ProcessId;
                
                StatusMessage = $"Terminating process '{processName}'...";
                
                bool success = _processService.TerminateProcess(processId);
                
                if (success)
                {
                    _notificationService.ShowSuccess($"Process '{processName}' terminated successfully.");
                    StatusMessage = $"Process '{processName}' terminated successfully.";
                    
                    // Refresh to update the process list
                    RefreshProcesses();
                }
                else
                {
                    _notificationService.ShowError($"Failed to terminate process '{processName}'.");
                    StatusMessage = $"Failed to terminate process '{processName}'.";
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error terminating process: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
        }
    }
}