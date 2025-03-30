using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
        #region Private Fields
        
        private int _selectedTabIndex;
        private string _statusMessage;
        private Brush _statusColor;
        private int _cpuUsage;
        private int _memoryUsage;
        private string _systemInfo;
        private ProcessesViewModel _processesViewModel;
        private CpuCoresViewModel _cpuCoresViewModel;
        private ProfileEditorViewModel _profileEditorViewModel;
        private bool _isBackgroundMode = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Current selected tab index
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }
        
        /// <summary>
        /// Status message shown in the status bar
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// Status bar message color
        /// </summary>
        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }
        
        /// <summary>
        /// Current CPU usage percentage
        /// </summary>
        public int CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }
        
        /// <summary>
        /// Current memory usage percentage
        /// </summary>
        public int MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }
        
        /// <summary>
        /// Basic system information displayed in the status bar
        /// </summary>
        public string SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Whether the application is running in background mode
        /// </summary>
        public bool IsBackgroundMode
        {
            get => _isBackgroundMode;
            set => SetProperty(ref _isBackgroundMode, value);
        }
        
        /// <summary>
        /// View model for the processes view
        /// </summary>
        public ProcessesViewModel ProcessesViewModel
        {
            get => _processesViewModel;
            set => SetProperty(ref _processesViewModel, value);
        }
        
        /// <summary>
        /// View model for the CPU cores view
        /// </summary>
        public CpuCoresViewModel CpuCoresViewModel
        {
            get => _cpuCoresViewModel;
            set => SetProperty(ref _cpuCoresViewModel, value);
        }
        
        /// <summary>
        /// View model for the profile editor view
        /// </summary>
        public ProfileEditorViewModel ProfileEditorViewModel
        {
            get => _profileEditorViewModel;
            set => SetProperty(ref _profileEditorViewModel, value);
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to minimize the application to the system tray
        /// </summary>
        public ICommand MinimizeToTrayCommand { get; }
        
        /// <summary>
        /// Command to show the settings dialog
        /// </summary>
        public ICommand ShowSettingsCommand { get; }
        
        /// <summary>
        /// Command to exit the application
        /// </summary>
        public ICommand ExitApplicationCommand { get; }
        
        /// <summary>
        /// Command to refresh data
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        #endregion
        
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Initialize commands
            MinimizeToTrayCommand = new RelayCommand(MinimizeToTray);
            ShowSettingsCommand = new RelayCommand(ShowSettings);
            ExitApplicationCommand = new RelayCommand(ExitApplication);
            RefreshCommand = new RelayCommand(RefreshData);
            
            // Initialize view models
            ProcessesViewModel = new ProcessesViewModel();
            CpuCoresViewModel = new CpuCoresViewModel();
            ProfileEditorViewModel = new ProfileEditorViewModel();
            
            // Set default values
            StatusMessage = "Ready";
            StatusColor = Brushes.Green;
            CpuUsage = 0;
            MemoryUsage = 0;
            SystemInfo = "Loading system information...";
            
            // Start data update timer
            StartDataUpdateTimer();
        }
        
        /// <summary>
        /// Start the timer for updating data
        /// </summary>
        private void StartDataUpdateTimer()
        {
            // In a real implementation, this would use a timer to update system info and status
            // For now, we'll just set some placeholder values
            
            // This is temporary sample data as we don't have a real system info service yet
            SystemInfo = "Intel Core i7-10700K | 16 Threads | 32 GB RAM | Windows 11";
            
            // Note: in a real implementation, we would create a System.Threading.Timer to refresh data periodically
        }
        
        /// <summary>
        /// Refresh all data
        /// </summary>
        private void RefreshData(object parameter)
        {
            StatusMessage = "Refreshing data...";
            
            try
            {
                // Update system information and usage statistics
                // This will be implemented when we have actual service implementations
                
                // Update view models
                ProcessesViewModel.RefreshProcesses();
                CpuCoresViewModel.RefreshCores();
                
                StatusMessage = "Data refreshed successfully";
                StatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error refreshing data: {ex.Message}";
                StatusColor = Brushes.Red;
            }
        }
        
        /// <summary>
        /// Minimize the application to the system tray
        /// </summary>
        private void MinimizeToTray(object parameter)
        {
            try
            {
                IsBackgroundMode = true;
                
                // Hide the main window
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Hide();
                }
                
                StatusMessage = "Application minimized to tray";
                StatusColor = Brushes.Green;
                
                NotificationService.ShowInfo("ThreadPilot is now running in the background", "Application Minimized");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error minimizing to tray: {ex.Message}";
                StatusColor = Brushes.Red;
            }
        }
        
        /// <summary>
        /// Show the settings dialog
        /// </summary>
        private void ShowSettings(object parameter)
        {
            // This will be implemented later
            NotificationService.ShowInfo("Settings dialog will be implemented in a future update", "Coming Soon");
        }
        
        /// <summary>
        /// Exit the application
        /// </summary>
        private void ExitApplication(object parameter)
        {
            try
            {
                // Show confirmation dialog
                MessageBoxResult result = MessageBox.Show(
                    "Are you sure you want to exit ThreadPilot?",
                    "Exit Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error exiting application: {ex.Message}";
                StatusColor = Brushes.Red;
            }
        }
    }
}