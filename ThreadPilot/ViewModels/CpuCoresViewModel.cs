using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the CPU cores view
    /// </summary>
    public class CpuCoresViewModel : ViewModelBase
    {
        #region Private Fields
        
        private ObservableCollection<CpuCore> _cores;
        private CpuCore _selectedCore;
        private bool _isLoading = false;
        private string _cpuName;
        private int _totalCores;
        private int _totalThreads;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Collection of CPU cores
        /// </summary>
        public ObservableCollection<CpuCore> Cores
        {
            get => _cores;
            set => SetProperty(ref _cores, value);
        }
        
        /// <summary>
        /// Currently selected CPU core
        /// </summary>
        public CpuCore SelectedCore
        {
            get => _selectedCore;
            set => SetProperty(ref _selectedCore, value);
        }
        
        /// <summary>
        /// Whether the view model is loading data
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        /// <summary>
        /// CPU name
        /// </summary>
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }
        
        /// <summary>
        /// Total number of physical cores
        /// </summary>
        public int TotalCores
        {
            get => _totalCores;
            set => SetProperty(ref _totalCores, value);
        }
        
        /// <summary>
        /// Total number of logical processors (threads)
        /// </summary>
        public int TotalThreads
        {
            get => _totalThreads;
            set => SetProperty(ref _totalThreads, value);
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to refresh core data
        /// </summary>
        public ICommand RefreshCoresCommand { get; }
        
        /// <summary>
        /// Command to optimize core settings
        /// </summary>
        public ICommand OptimizeCoresCommand { get; }
        
        /// <summary>
        /// Command to reset core settings to default
        /// </summary>
        public ICommand ResetCoresCommand { get; }
        
        #endregion
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            // Initialize collections
            Cores = new ObservableCollection<CpuCore>();
            
            // Initialize commands
            RefreshCoresCommand = new RelayCommand(RefreshCores);
            OptimizeCoresCommand = new RelayCommand(OptimizeCores);
            ResetCoresCommand = new RelayCommand(ResetCores);
            
            // Load initial data
            LoadCores();
        }
        
        /// <summary>
        /// Load CPU core data
        /// </summary>
        private void LoadCores()
        {
            IsLoading = true;
            
            try
            {
                // This is where we would retrieve core information from the system info service
                // For now, we'll create some sample data for demonstration
                
                // Clear existing cores
                Cores.Clear();
                
                // Set CPU information
                CpuName = "Intel Core i7-10700K";
                TotalCores = 8;
                TotalThreads = 16;
                
                // In the future, this will be retrieved from ISystemInfoService
                // For example: 
                // var cpuInfo = ServiceLocator.Get<ISystemInfoService>().GetCpuInfo();
                // CpuName = cpuInfo.Name;
                // TotalCores = cpuInfo.PhysicalCores;
                // TotalThreads = cpuInfo.LogicalProcessors;
                // Cores = new ObservableCollection<CpuCore>(cpuInfo.Cores);
                
                IsLoading = false;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                NotificationService.ShowError($"Error loading CPU core information: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh the core data
        /// </summary>
        public void RefreshCores(object parameter = null)
        {
            LoadCores();
        }
        
        /// <summary>
        /// Optimize core settings
        /// </summary>
        private void OptimizeCores(object parameter)
        {
            try
            {
                // This is where we would apply optimized core settings
                // For now, we'll just show a notification
                
                NotificationService.ShowInfo("Core optimization will be implemented in a future update", "Coming Soon");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error optimizing cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reset core settings to default
        /// </summary>
        private void ResetCores(object parameter)
        {
            try
            {
                // This is where we would reset core settings to default
                // For now, we'll just show a notification
                
                NotificationService.ShowInfo("Core reset will be implemented in a future update", "Coming Soon");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error resetting cores: {ex.Message}");
            }
        }
    }
}