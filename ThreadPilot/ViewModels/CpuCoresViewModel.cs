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
    /// CPU cores view model
    /// </summary>
    public class CpuCoresViewModel : ViewModelBase
    {
        // Selected core
        private CpuCore? _selectedCore;
        
        // System info
        private SystemInfo? _systemInfo;
        
        // System info service
        private readonly ISystemInfoService _systemInfoService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        // Timer for updating system info
        private readonly Timer _updateTimer;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize collections
            CpuCores = new ObservableCollection<CpuCore>();
            
            // Create commands
            RefreshCommand = new RelayCommand(Refresh);
            UnparkAllCoresCommand = new RelayCommand(UnparkAllCores);
            
            // Create timer
            _updateTimer = new Timer(3000);
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
        /// Unpark all cores command
        /// </summary>
        public ICommand UnparkAllCoresCommand { get; }
        
        /// <summary>
        /// CPU cores
        /// </summary>
        public ObservableCollection<CpuCore> CpuCores { get; }
        
        /// <summary>
        /// System info
        /// </summary>
        public SystemInfo? SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Selected core
        /// </summary>
        public CpuCore? SelectedCore
        {
            get => _selectedCore;
            set => SetProperty(ref _selectedCore, value);
        }
        
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
                var selectedCoreId = SelectedCore?.Id;
                
                // Get system info
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update cores
                CpuCores.Clear();
                foreach (var core in SystemInfo.CpuCores)
                {
                    CpuCores.Add(core);
                }
                
                // Restore selected core
                if (selectedCoreId.HasValue)
                {
                    SelectedCore = CpuCores.FirstOrDefault(c => c.Id == selectedCoreId);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Unpark all cores
        /// </summary>
        private void UnparkAllCores(object? parameter)
        {
            try
            {
                _systemInfoService.UnparkAllCores();
                _notificationService.ShowSuccess("All CPU cores unparked successfully");
                
                // Refresh to show the changes
                Refresh(null);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error unparking cores: {ex.Message}");
            }
        }
    }
}