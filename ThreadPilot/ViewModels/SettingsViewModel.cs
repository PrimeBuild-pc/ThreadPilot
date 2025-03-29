using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;

        private bool _startWithWindows;
        private bool _startMinimized;
        private bool _minimizeToTray;
        private bool _closeToTray;
        private bool _checkUpdatesAutomatically;
        private bool _showProcessNotifications;
        private bool _isDarkTheme;
        private int _refreshInterval;
        private bool _isBusy;
        private string _statusMessage = "Ready";

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set => SetProperty(ref _minimizeToTray, value);
        }

        public bool CloseToTray
        {
            get => _closeToTray;
            set => SetProperty(ref _closeToTray, value);
        }

        public bool CheckUpdatesAutomatically
        {
            get => _checkUpdatesAutomatically;
            set => SetProperty(ref _checkUpdatesAutomatically, value);
        }

        public bool ShowProcessNotifications
        {
            get => _showProcessNotifications;
            set => SetProperty(ref _showProcessNotifications, value);
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
        }

        public int RefreshInterval
        {
            get => _refreshInterval;
            set => SetProperty(ref _refreshInterval, value);
        }

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

        public ObservableCollection<int> RefreshIntervals { get; } = new ObservableCollection<int> { 5, 10, 30, 60 };

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public SettingsViewModel(
            SettingsService settingsService,
            NotificationService notificationService)
        {
            _settingsService = settingsService;
            _notificationService = notificationService;

            // Set up commands
            SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
            ResetSettingsCommand = new RelayCommand(_ => ResetSettings());

            // Load settings
            LoadSettings();
        }

        private void LoadSettings()
        {
            StartWithWindows = _settingsService.StartWithWindows;
            StartMinimized = _settingsService.StartMinimized;
            MinimizeToTray = _settingsService.MinimizeToTray;
            CloseToTray = _settingsService.CloseToTray;
            CheckUpdatesAutomatically = _settingsService.CheckUpdatesAutomatically;
            ShowProcessNotifications = _settingsService.ShowProcessNotifications;
            IsDarkTheme = _settingsService.IsDarkTheme;
            RefreshInterval = _settingsService.RefreshInterval;

            // Add the current refresh interval if it's not in the list
            if (!RefreshIntervals.Contains(RefreshInterval))
            {
                RefreshIntervals.Add(RefreshInterval);
                RefreshIntervals = new ObservableCollection<int>(RefreshIntervals.OrderBy(x => x));
            }
        }

        private void SaveSettings()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Saving settings...";

                // Update settings service
                _settingsService.StartWithWindows = StartWithWindows;
                _settingsService.StartMinimized = StartMinimized;
                _settingsService.MinimizeToTray = MinimizeToTray;
                _settingsService.CloseToTray = CloseToTray;
                _settingsService.CheckUpdatesAutomatically = CheckUpdatesAutomatically;
                _settingsService.ShowProcessNotifications = ShowProcessNotifications;
                _settingsService.IsDarkTheme = IsDarkTheme;
                _settingsService.RefreshInterval = RefreshInterval;

                // Save to disk
                _settingsService.SaveSettings();

                // Apply startup setting
                _settingsService.SetStartWithWindows(StartWithWindows);

                // Apply theme
                _settingsService.ApplyTheme();

                IsBusy = false;
                StatusMessage = "Settings saved successfully";
                _notificationService.ShowNotification("ThreadPilot", "Settings saved successfully");
            }
            catch (Exception ex)
            {
                IsBusy = false;
                StatusMessage = $"Error saving settings: {ex.Message}";
                _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
            }
        }

        private void ResetSettings()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "Resetting settings to defaults...";

                // Reset to defaults
                _settingsService.ResetToDefaults();
                
                // Reload settings
                LoadSettings();

                IsBusy = false;
                StatusMessage = "Settings reset to defaults";
                _notificationService.ShowNotification("ThreadPilot", "Settings reset to defaults");
            }
            catch (Exception ex)
            {
                IsBusy = false;
                StatusMessage = $"Error resetting settings: {ex.Message}";
                _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
            }
        }
    }
}
