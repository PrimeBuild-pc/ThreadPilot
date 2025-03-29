using System;
using System.Threading.Tasks;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class SystemOptimizationViewModel : ViewModelBase
    {
        private readonly SystemOptimizationService _systemOptimizationService;
        private readonly NotificationService _notificationService;

        private bool _isBusy;
        private string _statusMessage = "Ready";
        private SystemSettings _currentSettings;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public SystemSettings CurrentSettings
        {
            get => _currentSettings;
            set => SetProperty(ref _currentSettings, value);
        }

        public ICommand RefreshSettingsCommand { get; }
        public ICommand ApplySettingsCommand { get; }
        public ICommand ResetToDefaultCommand { get; }
        public ICommand OptimizeForGamingCommand { get; }
        public ICommand OptimizeForWorkstationCommand { get; }
        public ICommand OptimizeForBalancedCommand { get; }

        public SystemOptimizationViewModel(
            SystemOptimizationService systemOptimizationService,
            NotificationService notificationService)
        {
            _systemOptimizationService = systemOptimizationService;
            _notificationService = notificationService;

            // Set up commands
            RefreshSettingsCommand = new RelayCommand(_ => RefreshSettings());
            ApplySettingsCommand = new RelayCommand(_ => ApplySettings());
            ResetToDefaultCommand = new RelayCommand(_ => ResetToDefault());
            OptimizeForGamingCommand = new RelayCommand(_ => OptimizeForGaming());
            OptimizeForWorkstationCommand = new RelayCommand(_ => OptimizeForWorkstation());
            OptimizeForBalancedCommand = new RelayCommand(_ => OptimizeForBalanced());

            // Initialize with default settings
            CurrentSettings = new SystemSettings();
            
            // Load current settings
            RefreshSettings();
        }

        public void RefreshSettings()
        {
            IsBusy = true;
            StatusMessage = "Loading current system settings...";

            Task.Run(() =>
            {
                try
                {
                    var settings = _systemOptimizationService.GetCurrentSystemSettings();
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentSettings = settings;
                        IsBusy = false;
                        StatusMessage = "Current system settings loaded";
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error loading settings: {ex.Message}";
                    });
                }
            });
        }

        public void ApplySettings()
        {
            if (CurrentSettings == null)
                return;

            IsBusy = true;
            StatusMessage = "Applying system settings...";

            Task.Run(() =>
            {
                try
                {
                    _systemOptimizationService.ApplySystemOptimizations(CurrentSettings);
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = "System settings applied successfully";
                        _notificationService.ShowNotification("ThreadPilot", "System settings applied successfully");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error applying settings: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        public void ResetToDefault()
        {
            IsBusy = true;
            StatusMessage = "Resetting to default settings...";

            Task.Run(() =>
            {
                try
                {
                    _systemOptimizationService.ResetToDefaultSettings();
                    
                    // Refresh settings after reset
                    var settings = _systemOptimizationService.GetCurrentSystemSettings();
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentSettings = settings;
                        IsBusy = false;
                        StatusMessage = "Settings reset to Windows defaults";
                        _notificationService.ShowNotification("ThreadPilot", "Settings reset to Windows defaults");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error resetting settings: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        public void OptimizeForGaming()
        {
            IsBusy = true;
            StatusMessage = "Applying gaming optimization...";

            Task.Run(() =>
            {
                try
                {
                    // Create gaming-optimized settings
                    var gamingSettings = new SystemSettings
                    {
                        CoreParkingEnabled = false,
                        PerformanceBoostMode = 2, // Aggressive
                        SystemResponsiveness = 0, // Prioritize foreground
                        NetworkThrottlingIndex = -1, // Disabled
                        PrioritySeparation = 38, // High foreground boost
                        GameModeEnabled = true,
                        GameBarEnabled = false,
                        GameDVREnabled = false,
                        HibernationEnabled = false,
                        VisualEffectsLevel = 2 // Custom (performance optimized)
                    };

                    // Apply the settings
                    _systemOptimizationService.ApplySystemOptimizations(gamingSettings);
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentSettings = gamingSettings;
                        IsBusy = false;
                        StatusMessage = "Gaming optimization applied";
                        _notificationService.ShowNotification("ThreadPilot", "Gaming optimization applied");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error applying gaming optimization: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        public void OptimizeForWorkstation()
        {
            IsBusy = true;
            StatusMessage = "Applying workstation optimization...";

            Task.Run(() =>
            {
                try
                {
                    // Create workstation-optimized settings
                    var workstationSettings = new SystemSettings
                    {
                        CoreParkingEnabled = false,
                        PerformanceBoostMode = 3, // Efficient Aggressive
                        SystemResponsiveness = 20, // Default - balanced
                        NetworkThrottlingIndex = -1, // Disabled
                        PrioritySeparation = 26, // Medium foreground boost
                        GameModeEnabled = false,
                        GameBarEnabled = false,
                        GameDVREnabled = false,
                        HibernationEnabled = false,
                        VisualEffectsLevel = 3 // Best appearance
                    };

                    // Apply the settings
                    _systemOptimizationService.ApplySystemOptimizations(workstationSettings);
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentSettings = workstationSettings;
                        IsBusy = false;
                        StatusMessage = "Workstation optimization applied";
                        _notificationService.ShowNotification("ThreadPilot", "Workstation optimization applied");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error applying workstation optimization: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        public void OptimizeForBalanced()
        {
            IsBusy = true;
            StatusMessage = "Applying balanced optimization...";

            Task.Run(() =>
            {
                try
                {
                    // Create balanced settings
                    var balancedSettings = new SystemSettings
                    {
                        CoreParkingEnabled = true, // Enable for power savings
                        PerformanceBoostMode = 1, // Enabled
                        SystemResponsiveness = 20, // Default
                        NetworkThrottlingIndex = 10, // Default
                        PrioritySeparation = 2, // Default
                        GameModeEnabled = true,
                        GameBarEnabled = true,
                        GameDVREnabled = false, // Off to save resources
                        HibernationEnabled = true,
                        VisualEffectsLevel = 3 // Best appearance
                    };

                    // Apply the settings
                    _systemOptimizationService.ApplySystemOptimizations(balancedSettings);
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentSettings = balancedSettings;
                        IsBusy = false;
                        StatusMessage = "Balanced optimization applied";
                        _notificationService.ShowNotification("ThreadPilot", "Balanced optimization applied");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error applying balanced optimization: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }
    }
}
