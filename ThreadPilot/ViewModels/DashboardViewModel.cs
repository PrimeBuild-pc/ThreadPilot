using System;
using System.Collections.ObjectModel;
using System.Timers;
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
        // Selected profile
        private BundledPowerProfile? _selectedProfile;
        
        // System info
        private SystemInfo? _systemInfo;
        
        // System info service
        private readonly ISystemInfoService _systemInfoService;
        
        // Power profile service
        private readonly IPowerProfileService _powerProfileService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        // Timer for updating system info
        private readonly Timer _updateTimer;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize collections
            PowerProfiles = new ObservableCollection<BundledPowerProfile>();
            
            // Create commands
            RefreshCommand = new RelayCommand(Refresh);
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            
            // Create timer
            _updateTimer = new Timer(1000);
            _updateTimer.Elapsed += UpdateTimer_Elapsed;
            _updateTimer.Start();
            
            // Initial refresh
            Refresh(null);
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
        /// Power profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> PowerProfiles { get; }
        
        /// <summary>
        /// System info
        /// </summary>
        public SystemInfo? SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Selected profile
        /// </summary>
        public BundledPowerProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    ((RelayCommand)ApplyProfileCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsagePercentage => SystemInfo?.CpuUsagePercentage ?? 0;
        
        /// <summary>
        /// Memory usage percentage
        /// </summary>
        public double MemoryUsagePercentage => SystemInfo?.MemoryUsagePercentage ?? 0;
        
        /// <summary>
        /// Used memory in GB
        /// </summary>
        public double UsedMemoryGB => SystemInfo?.UsedMemoryGB ?? 0;
        
        /// <summary>
        /// Total memory in GB
        /// </summary>
        public double TotalMemoryGB => SystemInfo?.TotalMemoryGB ?? 0;
        
        /// <summary>
        /// Total processes count
        /// </summary>
        public int TotalProcessesCount => SystemInfo?.TotalProcessesCount ?? 0;
        
        /// <summary>
        /// Active processes count
        /// </summary>
        public int ActiveProcessesCount => SystemInfo?.ActiveProcessesCount ?? 0;
        
        /// <summary>
        /// CPU temperature in Celsius
        /// </summary>
        public double CpuTemperatureCelsius => SystemInfo?.CpuTemperatureCelsius ?? 0;
        
        /// <summary>
        /// Timer elapsed event handler
        /// </summary>
        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Refresh(null);
        }
        
        /// <summary>
        /// Refresh
        /// </summary>
        private void Refresh(object? parameter)
        {
            try
            {
                // Get system info
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update profiles
                PowerProfiles.Clear();
                foreach (var profile in _powerProfileService.GetAllProfiles())
                {
                    PowerProfiles.Add(profile);
                }
                
                // Select default profile if none is selected
                SelectedProfile ??= PowerProfiles.Count > 0 ? PowerProfiles[0] : null;
                
                // Notify properties changed
                OnPropertyChanged(nameof(CpuUsagePercentage));
                OnPropertyChanged(nameof(MemoryUsagePercentage));
                OnPropertyChanged(nameof(UsedMemoryGB));
                OnPropertyChanged(nameof(TotalMemoryGB));
                OnPropertyChanged(nameof(TotalProcessesCount));
                OnPropertyChanged(nameof(ActiveProcessesCount));
                OnPropertyChanged(nameof(CpuTemperatureCelsius));
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing dashboard: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply profile
        /// </summary>
        private void ApplyProfile(object? parameter)
        {
            if (SelectedProfile == null)
            {
                return;
            }
            
            try
            {
                if (_powerProfileService.ApplyProfile(SelectedProfile.Id))
                {
                    _notificationService.ShowSuccess($"Profile '{SelectedProfile.Name}' applied successfully");
                }
                else
                {
                    _notificationService.ShowError($"Failed to apply profile '{SelectedProfile.Name}'");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Can apply profile
        /// </summary>
        private bool CanApplyProfile(object? parameter)
        {
            return SelectedProfile != null;
        }
    }
}