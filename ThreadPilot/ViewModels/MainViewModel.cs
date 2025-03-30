using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IPowerProfileService _powerProfileService;
        private readonly ISystemInfoService _systemInfoService;

        private SystemInfo _systemInfo = new SystemInfo();
        private object? _currentView;
        private NavigationItem? _selectedNavigationItem;
        private BundledPowerProfile? _selectedPowerProfile;
        private string _statusMessage = "Ready";
        private string _systemInfoText = string.Empty;

        /// <summary>
        /// List of navigation items
        /// </summary>
        public ObservableCollection<NavigationItem> NavigationItems { get; } = new ObservableCollection<NavigationItem>();

        /// <summary>
        /// List of power profiles
        /// </summary>
        public ObservableCollection<BundledPowerProfile> PowerProfiles { get; } = new ObservableCollection<BundledPowerProfile>();

        /// <summary>
        /// Current system information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }

        /// <summary>
        /// Currently selected navigation item
        /// </summary>
        public NavigationItem? SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value) && value != null)
                {
                    // Change the current view when navigation item changes
                    CurrentView = value.ViewModel;
                }
            }
        }

        /// <summary>
        /// Currently selected power profile
        /// </summary>
        public BundledPowerProfile? SelectedPowerProfile
        {
            get => _selectedPowerProfile;
            set
            {
                if (SetProperty(ref _selectedPowerProfile, value) && value != null)
                {
                    // Apply the profile when selected
                    ApplySelectedProfile();
                }
            }
        }

        /// <summary>
        /// Currently displayed view
        /// </summary>
        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// Status message displayed in the status bar
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// System information text displayed in the status bar
        /// </summary>
        public string SystemInfoText
        {
            get => _systemInfoText;
            set => SetProperty(ref _systemInfoText, value);
        }

        // Commands
        /// <summary>
        /// Command to open settings dialog
        /// </summary>
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Command to open about dialog
        /// </summary>
        public ICommand OpenAboutCommand { get; }

        /// <summary>
        /// Command to import a power profile
        /// </summary>
        public ICommand ImportProfileCommand { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.Get<INotificationService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();

            // Initialize commands
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenAboutCommand = new RelayCommand(OpenAbout);
            ImportProfileCommand = new RelayCommand(ImportProfile);
        }

        /// <summary>
        /// Initialize the view model
        /// </summary>
        public override void Initialize()
        {
            try
            {
                // Load system information
                SystemInfo = _systemInfoService.GetSystemInfo();
                UpdateSystemInfoText();

                // Load default navigation items
                LoadNavigationItems();

                // Select the first navigation item by default
                if (NavigationItems.Count > 0)
                {
                    SelectedNavigationItem = NavigationItems[0];
                }

                // Load power profiles
                LoadPowerProfiles();

                // Update status
                StatusMessage = "Application initialized successfully";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error initializing application: {ex.Message}");
                StatusMessage = "Error during initialization";
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public override void Cleanup()
        {
            // Clean up any resources before closing
            StatusMessage = "Shutting down...";
        }

        private void LoadNavigationItems()
        {
            NavigationItems.Clear();

            // Add dashboard view model
            NavigationItems.Add(new NavigationItem
            {
                Name = "Dashboard",
                ViewModel = new DashboardViewModel()
            });

            // Add processes view model
            NavigationItems.Add(new NavigationItem
            {
                Name = "Processes",
                ViewModel = new ProcessesViewModel()
            });

            // Add CPU cores view model
            NavigationItems.Add(new NavigationItem
            {
                Name = "CPU Cores",
                ViewModel = new CpuCoresViewModel()
            });

            // Add profile editor view model
            NavigationItems.Add(new NavigationItem
            {
                Name = "Profile Editor",
                ViewModel = new ProfileEditorViewModel()
            });
        }

        private void LoadPowerProfiles()
        {
            try
            {
                PowerProfiles.Clear();

                // Load profiles from the power profile service
                var profiles = _powerProfileService.GetAvailableProfiles();
                foreach (var profile in profiles)
                {
                    PowerProfiles.Add(profile);
                }

                StatusMessage = $"Loaded {profiles.Count} power profiles";
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error loading power profiles: {ex.Message}");
                StatusMessage = "Error loading profiles";
            }
        }

        private void ApplySelectedProfile()
        {
            if (SelectedPowerProfile == null) return;

            try
            {
                // Apply the selected profile
                var result = _powerProfileService.ApplyProfile(SelectedPowerProfile);
                if (result)
                {
                    // Mark this profile as active and others as inactive
                    foreach (var profile in PowerProfiles)
                    {
                        profile.IsActive = profile == SelectedPowerProfile;
                    }

                    StatusMessage = $"Applied profile: {SelectedPowerProfile.Name}";
                    _notificationService.ShowSuccess($"Profile '{SelectedPowerProfile.Name}' applied successfully");
                }
                else
                {
                    StatusMessage = $"Failed to apply profile: {SelectedPowerProfile.Name}";
                    _notificationService.ShowError($"Failed to apply profile '{SelectedPowerProfile.Name}'");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying profile: {ex.Message}");
                StatusMessage = "Error applying profile";
            }
        }

        private void ImportProfile(object? parameter)
        {
            try
            {
                // Show file dialog to select a profile file
                var filePath = _fileDialogService.OpenFile("Power Profiles (*.pow)|*.pow", "Import Power Profile");
                if (string.IsNullOrEmpty(filePath)) return;

                // Import the profile
                var profile = _powerProfileService.ImportProfile(filePath);
                if (profile != null)
                {
                    PowerProfiles.Add(profile);
                    SelectedPowerProfile = profile;

                    StatusMessage = $"Imported profile: {profile.Name}";
                    _notificationService.ShowSuccess($"Profile '{profile.Name}' imported successfully");
                }
                else
                {
                    StatusMessage = "Failed to import profile";
                    _notificationService.ShowError("Failed to import profile");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error importing profile: {ex.Message}");
                StatusMessage = "Error importing profile";
            }
        }

        private void OpenSettings(object? parameter)
        {
            // TODO: Implement settings dialog
            MessageBox.Show("Settings dialog not implemented yet.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenAbout(object? parameter)
        {
            // TODO: Implement about dialog
            MessageBox.Show("ThreadPilot 1.0\nAdvanced CPU Thread Optimizer\n© 2023", "About ThreadPilot", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateSystemInfoText()
        {
            // Create system info text for status bar
            SystemInfoText = $"{SystemInfo.CpuName} | {SystemInfo.CoreCount} Cores | {SystemInfo.ProcessorCount} Threads | {SystemInfo.TotalRam:F1} GB RAM";
        }
    }

    /// <summary>
    /// Represents a navigation item in the main window
    /// </summary>
    public class NavigationItem
    {
        /// <summary>
        /// Name of the navigation item
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// View model associated with this navigation item
        /// </summary>
        public ViewModelBase? ViewModel { get; set; }
    }
}