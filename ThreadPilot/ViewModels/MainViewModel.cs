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
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly IProcessService _processService;
        private readonly IPowerProfileService _powerProfileService;
        private readonly INotificationService _notificationService;
        
        private SystemInfo _systemInfo;
        private ObservableCollection<ProcessInfo> _processes;
        private ObservableCollection<PowerProfile> _powerProfiles;
        private PowerProfile _selectedPowerProfile;
        private string _selectedTabName = "Dashboard";
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class
        /// </summary>
        public MainViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.GetService<ISystemInfoService>();
            _processService = ServiceLocator.GetService<IProcessService>();
            _powerProfileService = ServiceLocator.GetService<IPowerProfileService>();
            _notificationService = ServiceLocator.GetService<INotificationService>();
            
            // Initialize properties
            SystemInfo = _systemInfoService.GetSystemInfo();
            Processes = new ObservableCollection<ProcessInfo>(_processService.GetRunningProcesses().OrderByDescending(p => p.CpuUsagePercent));
            PowerProfiles = new ObservableCollection<PowerProfile>(_powerProfileService.GetAllProfiles());
            SelectedPowerProfile = PowerProfiles.FirstOrDefault(p => p.Name == "High Performance") ?? PowerProfiles.FirstOrDefault();
            
            // Initialize commands
            NavigateToTabCommand = new RelayCommand(ExecuteNavigateToTab);
            ApplyPowerProfileCommand = new RelayCommand(ExecuteApplyPowerProfile);
            RefreshDataCommand = new RelayCommand(ExecuteRefreshData);
            CreatePowerProfileCommand = new RelayCommand(ExecuteCreatePowerProfile);
            EditPowerProfileCommand = new RelayCommand(ExecuteEditPowerProfile);
            DeletePowerProfileCommand = new RelayCommand(ExecuteDeletePowerProfile, CanExecuteDeletePowerProfile);
            TerminateProcessCommand = new RelayCommand(ExecuteTerminateProcess);
            SetProcessPriorityCommand = new RelayCommand(ExecuteSetProcessPriority);
            SetProcessAffinityCommand = new RelayCommand(ExecuteSetProcessAffinity);
        }
        
        /// <summary>
        /// Gets or sets the system information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Gets or sets the collection of running processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set => SetProperty(ref _processes, value);
        }
        
        /// <summary>
        /// Gets or sets the collection of power profiles
        /// </summary>
        public ObservableCollection<PowerProfile> PowerProfiles
        {
            get => _powerProfiles;
            set => SetProperty(ref _powerProfiles, value);
        }
        
        /// <summary>
        /// Gets or sets the selected power profile
        /// </summary>
        public PowerProfile SelectedPowerProfile
        {
            get => _selectedPowerProfile;
            set => SetProperty(ref _selectedPowerProfile, value);
        }
        
        /// <summary>
        /// Gets or sets the selected tab name
        /// </summary>
        public string SelectedTabName
        {
            get => _selectedTabName;
            set => SetProperty(ref _selectedTabName, value);
        }
        
        /// <summary>
        /// Gets the command to navigate to a tab
        /// </summary>
        public ICommand NavigateToTabCommand { get; }
        
        /// <summary>
        /// Gets the command to apply the selected power profile
        /// </summary>
        public ICommand ApplyPowerProfileCommand { get; }
        
        /// <summary>
        /// Gets the command to refresh data
        /// </summary>
        public ICommand RefreshDataCommand { get; }
        
        /// <summary>
        /// Gets the command to create a new power profile
        /// </summary>
        public ICommand CreatePowerProfileCommand { get; }
        
        /// <summary>
        /// Gets the command to edit a power profile
        /// </summary>
        public ICommand EditPowerProfileCommand { get; }
        
        /// <summary>
        /// Gets the command to delete a power profile
        /// </summary>
        public ICommand DeletePowerProfileCommand { get; }
        
        /// <summary>
        /// Gets the command to terminate a process
        /// </summary>
        public ICommand TerminateProcessCommand { get; }
        
        /// <summary>
        /// Gets the command to set process priority
        /// </summary>
        public ICommand SetProcessPriorityCommand { get; }
        
        /// <summary>
        /// Gets the command to set process affinity
        /// </summary>
        public ICommand SetProcessAffinityCommand { get; }
        
        /// <summary>
        /// Execute the navigate to tab command
        /// </summary>
        /// <param name="parameter">Tab name</param>
        private void ExecuteNavigateToTab(object parameter)
        {
            if (parameter is string tabName)
            {
                SelectedTabName = tabName;
            }
        }
        
        /// <summary>
        /// Execute the refresh data command
        /// </summary>
        /// <param name="parameter">Not used</param>
        private void ExecuteRefreshData(object parameter)
        {
            try
            {
                SystemInfo = _systemInfoService.GetSystemInfo();
                Processes = new ObservableCollection<ProcessInfo>(_processService.GetRunningProcesses().OrderByDescending(p => p.CpuUsagePercent));
                PowerProfiles = new ObservableCollection<PowerProfile>(_powerProfileService.GetAllProfiles());
                
                if (SelectedPowerProfile != null)
                {
                    // Update selected profile with fresh data
                    string currentProfileName = SelectedPowerProfile.Name;
                    SelectedPowerProfile = PowerProfiles.FirstOrDefault(p => p.Name == currentProfileName) ?? PowerProfiles.FirstOrDefault();
                }
                else
                {
                    SelectedPowerProfile = PowerProfiles.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to refresh data", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the apply power profile command
        /// </summary>
        /// <param name="parameter">Power profile (optional)</param>
        private void ExecuteApplyPowerProfile(object parameter)
        {
            try
            {
                var profile = parameter as PowerProfile ?? SelectedPowerProfile;
                
                if (profile == null)
                {
                    _notificationService.ShowError("No profile selected", "Please select a power profile to apply.");
                    return;
                }
                
                bool success = _powerProfileService.ApplyProfile(profile);
                
                if (success)
                {
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' applied successfully.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to apply profile '{profile.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to apply profile", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the create power profile command
        /// </summary>
        /// <param name="parameter">Not used</param>
        private void ExecuteCreatePowerProfile(object parameter)
        {
            try
            {
                // In a real implementation, this would show a dialog to create a profile
                var profile = _powerProfileService.CreateDefaultProfile("Custom Profile", "Custom");
                
                if (_powerProfileService.SaveProfile(profile))
                {
                    // Refresh profiles
                    PowerProfiles = new ObservableCollection<PowerProfile>(_powerProfileService.GetAllProfiles());
                    SelectedPowerProfile = PowerProfiles.FirstOrDefault(p => p.Name == profile.Name);
                    
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' created successfully.");
                }
                else
                {
                    _notificationService.ShowError("Failed to create profile.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to create profile", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the edit power profile command
        /// </summary>
        /// <param name="parameter">Power profile (optional)</param>
        private void ExecuteEditPowerProfile(object parameter)
        {
            try
            {
                var profile = parameter as PowerProfile ?? SelectedPowerProfile;
                
                if (profile == null)
                {
                    _notificationService.ShowError("No profile selected", "Please select a power profile to edit.");
                    return;
                }
                
                // In a real implementation, this would show a dialog to edit the profile
                _notificationService.ShowSuccess($"Profile '{profile.Name}' would be edited here (not implemented).");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to edit profile", ex.Message);
            }
        }
        
        /// <summary>
        /// Determine whether the delete power profile command can execute
        /// </summary>
        /// <param name="parameter">Power profile (optional)</param>
        /// <returns>True if the command can execute, false otherwise</returns>
        private bool CanExecuteDeletePowerProfile(object parameter)
        {
            var profile = parameter as PowerProfile ?? SelectedPowerProfile;
            return profile != null && !profile.IsSystemDefault;
        }
        
        /// <summary>
        /// Execute the delete power profile command
        /// </summary>
        /// <param name="parameter">Power profile (optional)</param>
        private void ExecuteDeletePowerProfile(object parameter)
        {
            try
            {
                var profile = parameter as PowerProfile ?? SelectedPowerProfile;
                
                if (profile == null)
                {
                    _notificationService.ShowError("No profile selected", "Please select a power profile to delete.");
                    return;
                }
                
                if (profile.IsSystemDefault)
                {
                    _notificationService.ShowError("Cannot delete system profile", "System default profiles cannot be deleted.");
                    return;
                }
                
                // In a real implementation, this would show a confirmation dialog
                if (_powerProfileService.DeleteProfile(profile.Name))
                {
                    // Refresh profiles
                    PowerProfiles = new ObservableCollection<PowerProfile>(_powerProfileService.GetAllProfiles());
                    SelectedPowerProfile = PowerProfiles.FirstOrDefault();
                    
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' deleted successfully.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to delete profile '{profile.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to delete profile", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the terminate process command
        /// </summary>
        /// <param name="parameter">Process ID or ProcessInfo object</param>
        private void ExecuteTerminateProcess(object parameter)
        {
            try
            {
                int processId = 0;
                
                if (parameter is ProcessInfo processInfo)
                {
                    processId = processInfo.ProcessId;
                }
                else if (parameter is int id)
                {
                    processId = id;
                }
                else if (parameter is string idStr && int.TryParse(idStr, out int parsedId))
                {
                    processId = parsedId;
                }
                
                if (processId <= 0)
                {
                    _notificationService.ShowError("Invalid process ID", "Please select a valid process to terminate.");
                    return;
                }
                
                // In a real implementation, this would show a confirmation dialog
                if (_processService.TerminateProcess(processId))
                {
                    // Refresh processes
                    Processes = new ObservableCollection<ProcessInfo>(_processService.GetRunningProcesses().OrderByDescending(p => p.CpuUsagePercent));
                    
                    _notificationService.ShowSuccess($"Process (ID: {processId}) terminated successfully.");
                }
                else
                {
                    _notificationService.ShowError($"Failed to terminate process (ID: {processId}).");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to terminate process", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the set process priority command
        /// </summary>
        /// <param name="parameter">Tuple of process ID and priority</param>
        private void ExecuteSetProcessPriority(object parameter)
        {
            try
            {
                if (parameter is Tuple<int, ProcessPriority> tuple)
                {
                    int processId = tuple.Item1;
                    ProcessPriority priority = tuple.Item2;
                    
                    if (_processService.SetProcessPriority(processId, priority))
                    {
                        // Update process in collection
                        var process = Processes.FirstOrDefault(p => p.ProcessId == processId);
                        if (process != null)
                        {
                            process.Priority = priority;
                        }
                        
                        _notificationService.ShowSuccess($"Process priority set successfully.");
                    }
                    else
                    {
                        _notificationService.ShowError($"Failed to set process priority.");
                    }
                }
                else
                {
                    _notificationService.ShowError("Invalid parameters", "Please provide valid process ID and priority.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to set process priority", ex.Message);
            }
        }
        
        /// <summary>
        /// Execute the set process affinity command
        /// </summary>
        /// <param name="parameter">Tuple of process ID and affinity mask</param>
        private void ExecuteSetProcessAffinity(object parameter)
        {
            try
            {
                if (parameter is Tuple<int, long> tuple)
                {
                    int processId = tuple.Item1;
                    long affinityMask = tuple.Item2;
                    
                    if (_processService.SetProcessAffinity(processId, affinityMask))
                    {
                        // Update process in collection
                        var process = Processes.FirstOrDefault(p => p.ProcessId == processId);
                        if (process != null)
                        {
                            process.AffinityMask = affinityMask;
                        }
                        
                        _notificationService.ShowSuccess($"Process affinity set successfully.");
                    }
                    else
                    {
                        _notificationService.ShowError($"Failed to set process affinity.");
                    }
                }
                else
                {
                    _notificationService.ShowError("Invalid parameters", "Please provide valid process ID and affinity mask.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError("Failed to set process affinity", ex.Message);
            }
        }
    }
}