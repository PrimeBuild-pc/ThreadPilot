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
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly INotificationService _notificationService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IPowerProfileService _powerProfileService;
        
        private Timer _refreshTimer;
        private string _statusMessage = "Ready";
        private SystemInfo _systemInfo;
        private PowerProfile? _selectedPowerProfile;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Get services from service locator
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            _fileDialogService = ServiceLocator.Get<IFileDialogService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            
            // Initialize child view models
            ProcessesViewModel = new ProcessesViewModel();
            CpuCoresViewModel = new CpuCoresViewModel();
            ProfileEditorViewModel = new ProfileEditorViewModel();
            
            // Initialize commands
            RefreshDataCommand = new RelayCommand(RefreshData);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            ImportProfileCommand = new RelayCommand(ImportProfile);
            CreateProfileCommand = new RelayCommand(CreateProfile);
            ApplyProfileCommand = new RelayCommand<PowerProfile>(ApplyProfile);
            EditProfileCommand = new RelayCommand<PowerProfile>(EditProfile);
            
            // Initialize system info
            _systemInfo = _systemInfoService.GetSystemInfo();
            
            // Initialize power profiles
            LoadPowerProfiles();
            
            // Set up refresh timer (refresh every 2 seconds)
            _refreshTimer = new Timer(2000);
            _refreshTimer.Elapsed += (s, e) => RefreshData();
            _refreshTimer.Start();
            
            // Set initial status
            StatusMessage = "Application started";
        }
        
        /// <summary>
        /// Processes view model
        /// </summary>
        public ProcessesViewModel ProcessesViewModel { get; }
        
        /// <summary>
        /// CPU cores view model
        /// </summary>
        public CpuCoresViewModel CpuCoresViewModel { get; }
        
        /// <summary>
        /// Profile editor view model
        /// </summary>
        public ProfileEditorViewModel ProfileEditorViewModel { get; }
        
        /// <summary>
        /// Status message displayed in status bar
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            private set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Power profiles collection
        /// </summary>
        public ObservableCollection<PowerProfile> PowerProfiles { get; } = new ObservableCollection<PowerProfile>();
        
        /// <summary>
        /// Selected power profile
        /// </summary>
        public PowerProfile? SelectedPowerProfile
        {
            get => _selectedPowerProfile;
            set => SetProperty(ref _selectedPowerProfile, value);
        }
        
        /// <summary>
        /// Refresh data command
        /// </summary>
        public ICommand RefreshDataCommand { get; }
        
        /// <summary>
        /// Open settings command
        /// </summary>
        public ICommand OpenSettingsCommand { get; }
        
        /// <summary>
        /// Import profile command
        /// </summary>
        public ICommand ImportProfileCommand { get; }
        
        /// <summary>
        /// Create profile command
        /// </summary>
        public ICommand CreateProfileCommand { get; }
        
        /// <summary>
        /// Apply profile command
        /// </summary>
        public ICommand ApplyProfileCommand { get; }
        
        /// <summary>
        /// Edit profile command
        /// </summary>
        public ICommand EditProfileCommand { get; }
        
        /// <summary>
        /// Refresh data
        /// </summary>
        private void RefreshData()
        {
            // Update on UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                // Update system info
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update CPU cores
                CpuCoresViewModel.UpdateCores(_systemInfoService.GetCpuCores());
                
                // Update processes (less frequently)
                if (DateTime.Now.Second % 10 == 0)
                {
                    ProcessesViewModel.RefreshProcesses();
                }
            });
        }
        
        /// <summary>
        /// Open settings dialog
        /// </summary>
        private void OpenSettings()
        {
            StatusMessage = "Settings dialog opened";
            _notificationService.ShowInfo("Settings functionality not implemented in this version.");
        }
        
        /// <summary>
        /// Import a power profile
        /// </summary>
        private void ImportProfile()
        {
            string? filePath = _fileDialogService.ShowOpenFileDialog(
                "Import Power Profile",
                "Power Profiles (*.json;*.pow)|*.json;*.pow|All Files (*.*)|*.*",
                ".json");
            
            if (!string.IsNullOrEmpty(filePath))
            {
                StatusMessage = $"Importing profile from {filePath}";
                var profile = _powerProfileService.ImportProfile(filePath);
                
                if (profile != null)
                {
                    LoadPowerProfiles();
                    SelectedPowerProfile = profile;
                }
            }
        }
        
        /// <summary>
        /// Create a new power profile
        /// </summary>
        private void CreateProfile()
        {
            StatusMessage = "Creating new profile";
            
            // Create a new profile with default values
            var newProfile = new PowerProfile
            {
                Name = "New Profile",
                Description = "Custom power profile",
                Version = "1.0"
            };
            
            // Switch to profile editor and load the new profile
            ProfileEditorViewModel.LoadProfile(newProfile);
            
            // TODO: Switch to Profile Editor tab programmatically
        }
        
        /// <summary>
        /// Apply a power profile
        /// </summary>
        /// <param name="profile">Profile to apply</param>
        private void ApplyProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            
            StatusMessage = $"Applying profile: {profile.Name}";
            _powerProfileService.ApplyProfile(profile);
        }
        
        /// <summary>
        /// Edit a power profile
        /// </summary>
        /// <param name="profile">Profile to edit</param>
        private void EditProfile(PowerProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            
            StatusMessage = $"Editing profile: {profile.Name}";
            ProfileEditorViewModel.LoadProfile(profile);
            
            // TODO: Switch to Profile Editor tab programmatically
        }
        
        /// <summary>
        /// Load power profiles
        /// </summary>
        private void LoadPowerProfiles()
        {
            PowerProfiles.Clear();
            
            foreach (var profile in _powerProfileService.GetProfiles())
            {
                PowerProfiles.Add(profile);
            }
            
            StatusMessage = $"Loaded {PowerProfiles.Count} power profiles";
        }
    }
    
    /// <summary>
    /// Generic RelayCommand with parameter
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T>? _canExecute;
        
        /// <summary>
        /// Event raised when the ability to execute the command changes
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="execute">Delegate to execute when command is invoked</param>
        /// <param name="canExecute">Delegate to check if command can be executed (can be null)</param>
        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        /// <summary>
        /// Determines if command can execute in its current state
        /// </summary>
        /// <param name="parameter">Data used by the command</param>
        /// <returns>True if command can execute</returns>
        public bool CanExecute(object? parameter)
        {
            if (_canExecute == null)
            {
                return true;
            }
            
            if (parameter == null && typeof(T).IsValueType)
            {
                return _canExecute(default!);
            }
            
            if (parameter == null || parameter is T)
            {
                return _canExecute((T)parameter!);
            }
            
            return false;
        }
        
        /// <summary>
        /// Execute the command
        /// </summary>
        /// <param name="parameter">Data passed to the command</param>
        public void Execute(object? parameter)
        {
            if (parameter == null && typeof(T).IsValueType)
            {
                _execute(default!);
                return;
            }
            
            if (parameter == null || parameter is T)
            {
                _execute((T)parameter!);
            }
        }
    }
}