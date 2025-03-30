using System;
using System.IO;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Main view model
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        // Current view model
        private ViewModelBase _currentViewModel;
        
        // Status message
        private string _statusMessage = "Ready";
        
        // Is dark mode
        private bool _isDarkMode;
        
        // Dashboard view model
        private readonly DashboardViewModel _dashboardViewModel;
        
        // Processes view model
        private readonly ProcessesViewModel _processesViewModel;
        
        // CPU cores view model
        private readonly CpuCoresViewModel _cpuCoresViewModel;
        
        // Profile editor view model
        private readonly ProfileEditorViewModel _profileEditorViewModel;
        
        // System info service
        private readonly ISystemInfoService _systemInfoService;
        
        // Power profile service
        private readonly IPowerProfileService _powerProfileService;
        
        // File dialog service
        private readonly IFileDialogService _fileDialogService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Set notification callback
            _notificationService.NotificationReceived += OnNotificationReceived;
            
            // Create view models
            _dashboardViewModel = new DashboardViewModel();
            _processesViewModel = new ProcessesViewModel();
            _cpuCoresViewModel = new CpuCoresViewModel();
            _profileEditorViewModel = new ProfileEditorViewModel();
            
            // Create commands
            NavigateCommand = new RelayCommand<string>(Navigate);
            ApplyProfileCommand = new RelayCommand(ApplyProfile);
            ExportLogsCommand = new RelayCommand(ExportLogs);
            
            // Set default view
            CurrentViewModel = _dashboardViewModel;
            
            // Set dark mode based on system settings
            IsDarkMode = IsSystemUsingDarkMode();
        }
        
        /// <summary>
        /// Navigate command
        /// </summary>
        public ICommand NavigateCommand { get; }
        
        /// <summary>
        /// Apply profile command
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Export logs command
        /// </summary>
        public ICommand ExportLogsCommand { get; }
        
        /// <summary>
        /// Current view model
        /// </summary>
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
        
        /// <summary>
        /// Status message
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// Is dark mode
        /// </summary>
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    ApplyTheme();
                }
            }
        }
        
        /// <summary>
        /// System info summary
        /// </summary>
        public string SystemInfoSummary => 
            $"{_systemInfoService.GetSystemInfo().CpuName} | {_systemInfoService.GetSystemInfo().CpuCores.Count} Cores | {_systemInfoService.GetSystemInfo().TotalMemoryGB:F1} GB RAM";
        
        /// <summary>
        /// Version info
        /// </summary>
        public string VersionInfo => $"ThreadPilot v1.0.0";
        
        /// <summary>
        /// Navigate
        /// </summary>
        private void Navigate(string? viewName)
        {
            if (string.IsNullOrEmpty(viewName))
            {
                return;
            }
            
            CurrentViewModel = viewName switch
            {
                "Dashboard" => _dashboardViewModel,
                "Processes" => _processesViewModel,
                "CpuCores" => _cpuCoresViewModel,
                "ProfileEditor" => _profileEditorViewModel,
                _ => _dashboardViewModel
            };
            
            StatusMessage = $"Viewing {viewName}";
        }
        
        /// <summary>
        /// Apply profile
        /// </summary>
        private void ApplyProfile(object? parameter)
        {
            try
            {
                // Get the first enabled profile
                var profile = _powerProfileService.GetAllProfiles().FirstOrDefault(p => p.IsEnabled);
                
                if (profile == null)
                {
                    _notificationService.ShowError("No enabled profile found");
                    return;
                }
                
                // Apply profile
                if (_powerProfileService.ApplyProfile(profile.Id))
                {
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' applied successfully");
                }
                else
                {
                    _notificationService.ShowError($"Failed to apply profile '{profile.Name}'");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Export logs
        /// </summary>
        private void ExportLogs(object? parameter)
        {
            try
            {
                // Get file path
                var filePath = _fileDialogService.ShowSaveDialog("Log Files (*.log)|*.log|All Files (*.*)|*.*", "ThreadPilot.log");
                
                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }
                
                // Export logs
                var logs = new[]
                {
                    $"ThreadPilot Log Export - {DateTime.Now}",
                    $"System Info: {_systemInfoService.GetSystemInfo().CpuName}",
                    $"CPU Cores: {_systemInfoService.GetSystemInfo().CpuCores.Count}",
                    $"Total Memory: {_systemInfoService.GetSystemInfo().TotalMemoryGB:F1} GB",
                    $"Used Memory: {_systemInfoService.GetSystemInfo().UsedMemoryGB:F1} GB",
                    $"CPU Temperature: {_systemInfoService.GetSystemInfo().CpuTemperatureCelsius:F1}°C",
                    $"Active Processes: {_systemInfoService.GetSystemInfo().ActiveProcessesCount}",
                    $"Total Processes: {_systemInfoService.GetSystemInfo().TotalProcessesCount}"
                };
                
                // Write logs to file
                File.WriteAllLines(filePath, logs);
                
                _notificationService.ShowSuccess("Logs exported successfully");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error exporting logs: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply theme
        /// </summary>
        private void ApplyTheme()
        {
            try
            {
                // In a real application, we would change the theme resources here
                // For now, we'll just change the status message
                StatusMessage = IsDarkMode ? "Dark theme applied" : "Light theme applied";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying theme: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Is system using dark mode
        /// </summary>
        private bool IsSystemUsingDarkMode()
        {
            try
            {
                // In a real application, we would check system settings here
                // For now, we'll just return a default value
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// On notification received
        /// </summary>
        private void OnNotificationReceived(object? sender, NotificationEventArgs e)
        {
            StatusMessage = e.Message;
        }
    }
}