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
    public class DashboardViewModel : ViewModelBase, IRefreshable
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly IPowerProfileService _powerProfileService;
        private SystemInfo _systemInfo;
        private Timer _updateTimer;
        private ObservableCollection<PowerProfile> _powerProfiles;
        private PowerProfile _selectedPowerProfile;
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Power profiles
        /// </summary>
        public ObservableCollection<PowerProfile> PowerProfiles
        {
            get => _powerProfiles;
            set => SetProperty(ref _powerProfiles, value);
        }
        
        /// <summary>
        /// Selected power profile
        /// </summary>
        public PowerProfile SelectedPowerProfile
        {
            get => _selectedPowerProfile;
            set => SetProperty(ref _selectedPowerProfile, value);
        }
        
        /// <summary>
        /// Apply profile command
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            _systemInfoService = ServiceLocator.Resolve<ISystemInfoService>();
            _powerProfileService = ServiceLocator.Resolve<IPowerProfileService>();
            
            PowerProfiles = new ObservableCollection<PowerProfile>();
            
            // Initialize commands
            ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            
            // Initialize timer for auto-refresh
            _updateTimer = new Timer(2000);
            _updateTimer.Elapsed += (s, e) => Refresh();
            _updateTimer.Start();
            
            // Initial refresh
            Refresh();
        }
        
        /// <summary>
        /// Refresh dashboard data
        /// </summary>
        public void Refresh()
        {
            try
            {
                // Update system info
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update power profiles
                var profiles = _powerProfileService.GetAllProfiles();
                
                // Execute on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    PowerProfiles.Clear();
                    foreach (var profile in profiles)
                    {
                        PowerProfiles.Add(profile);
                        
                        // Select active profile
                        if (profile.IsActive)
                        {
                            SelectedPowerProfile = profile;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                var notificationService = ServiceLocator.Resolve<INotificationService>();
                notificationService.ShowError($"Error refreshing dashboard: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply selected power profile
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        private void ApplyProfile(object parameter)
        {
            try
            {
                if (SelectedPowerProfile != null)
                {
                    var result = _powerProfileService.ActivateProfile(SelectedPowerProfile);
                    
                    var notificationService = ServiceLocator.Resolve<INotificationService>();
                    if (result)
                    {
                        notificationService.ShowSuccess($"Profile '{SelectedPowerProfile.Name}' activated successfully.");
                        Refresh();
                    }
                    else
                    {
                        notificationService.ShowError($"Failed to activate profile '{SelectedPowerProfile.Name}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                var notificationService = ServiceLocator.Resolve<INotificationService>();
                notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if a profile can be applied
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <returns>True if a profile can be applied, false otherwise</returns>
        private bool CanApplyProfile(object parameter)
        {
            return SelectedPowerProfile != null && !SelectedPowerProfile.IsActive;
        }
    }
}