using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Dashboard view model
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly IPowerProfileService _powerProfileService;
        private SystemInfo _systemInfo;
        private PowerProfile _currentProfile;
        private bool _isAutoOptimizationEnabled;

        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Resolve<ISystemInfoService>();
            _powerProfileService = ServiceLocator.Resolve<IPowerProfileService>();
            
            // Initialize properties
            SystemInfo = _systemInfoService?.GetSystemInfo() ?? new SystemInfo();
            TopProcesses = new ObservableCollection<ProcessInfo>();
            AvailableProfiles = new ObservableCollection<PowerProfile>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => Refresh());
            ApplyProfileCommand = new RelayCommand(_ => ApplyProfile(), _ => CurrentProfile != null);
            EnableAutoOptimizationCommand = new RelayCommand(_ => ToggleAutoOptimization());
            
            // Load available profiles
            LoadProfiles();
            
            // Load top processes
            LoadTopProcesses();
        }
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Current selected power profile
        /// </summary>
        public PowerProfile CurrentProfile
        {
            get => _currentProfile;
            set => SetProperty(ref _currentProfile, value);
        }
        
        /// <summary>
        /// Top processes by CPU usage
        /// </summary>
        public ObservableCollection<ProcessInfo> TopProcesses { get; }
        
        /// <summary>
        /// Available power profiles
        /// </summary>
        public ObservableCollection<PowerProfile> AvailableProfiles { get; }
        
        /// <summary>
        /// Gets or sets a value indicating whether auto optimization is enabled
        /// </summary>
        public bool IsAutoOptimizationEnabled
        {
            get => _isAutoOptimizationEnabled;
            set => SetProperty(ref _isAutoOptimizationEnabled, value);
        }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Apply profile command
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Enable auto optimization command
        /// </summary>
        public ICommand EnableAutoOptimizationCommand { get; }
        
        /// <summary>
        /// Refresh dashboard
        /// </summary>
        private void Refresh()
        {
            // Refresh system info
            SystemInfo = _systemInfoService?.GetSystemInfo() ?? new SystemInfo();
            
            // Reload top processes
            LoadTopProcesses();
        }
        
        /// <summary>
        /// Apply the selected power profile
        /// </summary>
        private void ApplyProfile()
        {
            if (CurrentProfile == null)
            {
                return;
            }
            
            var notificationService = ServiceLocator.Resolve<INotificationService>();
            bool success = _powerProfileService?.ApplyProfile(CurrentProfile) ?? false;
            
            if (success)
            {
                notificationService?.ShowSuccess($"Profile '{CurrentProfile.Name}' applied successfully.", "Profile Applied");
            }
            else
            {
                notificationService?.ShowError($"Failed to apply profile '{CurrentProfile.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Toggle auto optimization
        /// </summary>
        private void ToggleAutoOptimization()
        {
            IsAutoOptimizationEnabled = !IsAutoOptimizationEnabled;
            
            // TODO: Implement auto optimization logic
            
            var notificationService = ServiceLocator.Resolve<INotificationService>();
            if (IsAutoOptimizationEnabled)
            {
                notificationService?.ShowSuccess("Auto optimization enabled.", "Auto Optimization");
            }
            else
            {
                notificationService?.ShowInformation("Auto optimization disabled.", "Auto Optimization");
            }
        }
        
        /// <summary>
        /// Load available power profiles
        /// </summary>
        private void LoadProfiles()
        {
            AvailableProfiles.Clear();
            
            var profiles = _powerProfileService?.GetAllProfiles() ?? Array.Empty<PowerProfile>();
            foreach (var profile in profiles)
            {
                AvailableProfiles.Add(profile);
            }
            
            // Set default profile if available
            if (AvailableProfiles.Count > 0)
            {
                CurrentProfile = AvailableProfiles[0];
            }
        }
        
        /// <summary>
        /// Load top processes by CPU usage
        /// </summary>
        private void LoadTopProcesses()
        {
            TopProcesses.Clear();
            
            var processService = ServiceLocator.Resolve<IProcessService>();
            var processes = processService?.GetProcesses(10) ?? Array.Empty<ProcessInfo>();
            
            foreach (var process in processes)
            {
                TopProcesses.Add(process);
            }
        }
    }
}