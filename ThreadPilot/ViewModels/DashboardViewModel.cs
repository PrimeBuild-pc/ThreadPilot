using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the dashboard page
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        // Dependencies
        private readonly ISystemInfoService _systemInfoService;
        private readonly IPowerProfileService _powerProfileService;
        private readonly IProcessService _processService;
        
        // Timer for refreshing system info
        private readonly Timer _refreshTimer;
        
        // Backing fields
        private SystemInfo? _systemInfo;
        private BundledPowerProfile? _activePowerProfile;
        private ObservableCollection<ProcessInfo> _topProcesses = new ObservableCollection<ProcessInfo>();
        private bool _isBusy;
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo? SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Active power profile
        /// </summary>
        public BundledPowerProfile? ActivePowerProfile
        {
            get => _activePowerProfile;
            set => SetProperty(ref _activePowerProfile, value);
        }
        
        /// <summary>
        /// Top processes by CPU usage
        /// </summary>
        public ObservableCollection<ProcessInfo> TopProcesses
        {
            get => _topProcesses;
            set => SetProperty(ref _topProcesses, value);
        }
        
        /// <summary>
        /// Indicates if an operation is in progress
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }
        
        /// <summary>
        /// Command to refresh system information
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Command to optimize system performance
        /// </summary>
        public ICommand OptimizeCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            // Get dependencies
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _powerProfileService = ServiceLocator.Get<IPowerProfileService>();
            _processService = ServiceLocator.Get<IProcessService>();
            
            // Create commands
            RefreshCommand = new RelayCommand(param => RefreshSystemInfo(), param => !IsBusy);
            OptimizeCommand = new RelayCommand(param => OptimizeSystem(), param => !IsBusy);
            
            // Setup refresh timer
            _refreshTimer = new Timer(3000); // 3 seconds
            _refreshTimer.Elapsed += OnRefreshTimerElapsed;
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
            
            // Initial refresh
            RefreshSystemInfo();
        }
        
        /// <summary>
        /// Refresh system information
        /// </summary>
        private void RefreshSystemInfo()
        {
            try
            {
                IsBusy = true;
                
                // Get system info
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Get active power profile
                ActivePowerProfile = _powerProfileService.GetActiveProfile();
                
                // Get top processes
                var processes = _processService.GetProcesses();
                
                // Sort by CPU usage and take top 5
                var topProcesses = processes
                    .OrderByDescending(p => p.CpuUsage)
                    .Take(5)
                    .ToList();
                    
                // Update collection
                TopProcesses.Clear();
                foreach (var process in topProcesses)
                {
                    TopProcesses.Add(process);
                }
            }
            catch (Exception ex)
            {
                // Log or handle error
                Console.WriteLine($"Error refreshing system info: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Optimize system performance
        /// </summary>
        private async void OptimizeSystem()
        {
            try
            {
                IsBusy = true;
                
                // Get all available profiles
                var profiles = _powerProfileService.GetAvailableProfiles();
                
                // Find the "Performance" profile (or similar)
                var performanceProfile = profiles.FirstOrDefault(p => 
                    p.Name.Contains("Performance", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("High Performance", StringComparison.OrdinalIgnoreCase));
                    
                if (performanceProfile != null)
                {
                    // Apply the performance profile
                    bool success = await _powerProfileService.ApplyProfileAsync(performanceProfile);
                    
                    if (success)
                    {
                        // Update active profile
                        ActivePowerProfile = performanceProfile;
                    }
                }
                
                // Apply process optimizations
                _processService.OptimizeProcesses();
                
                // Refresh system info
                RefreshSystemInfo();
            }
            catch (Exception ex)
            {
                // Log or handle error
                Console.WriteLine($"Error optimizing system: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Event handler for the refresh timer
        /// </summary>
        private void OnRefreshTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            // Invoke on the UI thread
            App.Current.Dispatcher.Invoke(() =>
            {
                if (!IsBusy)
                {
                    RefreshSystemInfo();
                }
            });
        }
    }
}