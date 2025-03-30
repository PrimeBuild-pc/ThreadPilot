using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        private readonly ISystemInfoService _systemInfoService;
        private readonly INotificationService _notificationService;
        private CpuCore? _selectedCore;
        private bool _showEfficiencyCores = true;
        private bool _showPerformanceCores = true;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize commands
            UpdateCoresCommand = new RelayCommand(UpdateCores);
            ResetAllCoresCommand = new RelayCommand(ResetAllCores);
            OptimizeCommand = new RelayCommand(OptimizeCores);
            
            // Load cores
            UpdateCores();
        }
        
        /// <summary>
        /// Collection of CPU cores
        /// </summary>
        public ObservableCollection<CpuCore> Cores { get; } = new ObservableCollection<CpuCore>();
        
        /// <summary>
        /// Collection of CPU cores after filtering
        /// </summary>
        public ObservableCollection<CpuCore> FilteredCores { get; } = new ObservableCollection<CpuCore>();
        
        /// <summary>
        /// Selected CPU core
        /// </summary>
        public CpuCore? SelectedCore
        {
            get => _selectedCore;
            set
            {
                if (SetProperty(ref _selectedCore, value))
                {
                    OnPropertyChanged(nameof(IsCoreSelected));
                }
            }
        }
        
        /// <summary>
        /// Whether a core is selected
        /// </summary>
        public bool IsCoreSelected => _selectedCore != null;
        
        /// <summary>
        /// Whether to show efficiency cores
        /// </summary>
        public bool ShowEfficiencyCores
        {
            get => _showEfficiencyCores;
            set
            {
                if (SetProperty(ref _showEfficiencyCores, value))
                {
                    ApplyFilter();
                }
            }
        }
        
        /// <summary>
        /// Whether to show performance cores
        /// </summary>
        public bool ShowPerformanceCores
        {
            get => _showPerformanceCores;
            set
            {
                if (SetProperty(ref _showPerformanceCores, value))
                {
                    ApplyFilter();
                }
            }
        }
        
        /// <summary>
        /// Command to update cores
        /// </summary>
        public ICommand UpdateCoresCommand { get; }
        
        /// <summary>
        /// Command to reset all cores
        /// </summary>
        public ICommand ResetAllCoresCommand { get; }
        
        /// <summary>
        /// Command to optimize cores
        /// </summary>
        public ICommand OptimizeCommand { get; }
        
        /// <summary>
        /// Update the CPU cores
        /// </summary>
        public void UpdateCores()
        {
            try
            {
                var cores = _systemInfoService.GetCpuCores();
                UpdateCores(cores);
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error updating CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update the CPU cores with the provided collection
        /// </summary>
        /// <param name="cores">Collection of CPU cores</param>
        public void UpdateCores(System.Collections.Generic.IEnumerable<CpuCore> cores)
        {
            try
            {
                // Update the collection
                Cores.Clear();
                foreach (var core in cores)
                {
                    Cores.Add(core);
                }
                
                // Apply filter
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error updating CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply filter to the cores list
        /// </summary>
        private void ApplyFilter()
        {
            try
            {
                // Filter cores
                var filtered = Cores.AsEnumerable();
                
                // Filter by core type
                if (!_showEfficiencyCores)
                {
                    filtered = filtered.Where(c => !c.IsEfficiencyCore);
                }
                
                if (!_showPerformanceCores)
                {
                    filtered = filtered.Where(c => c.IsEfficiencyCore);
                }
                
                // Sort by index
                filtered = filtered.OrderBy(c => c.Index);
                
                // Update the filtered collection
                FilteredCores.Clear();
                foreach (var core in filtered)
                {
                    FilteredCores.Add(core);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying filter: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reset all CPU cores to default settings
        /// </summary>
        private void ResetAllCores()
        {
            try
            {
                var result = _systemInfoService.ResetCpuCores();
                
                if (result)
                {
                    _notificationService.ShowSuccess("All CPU cores reset to default settings");
                    UpdateCores();
                }
                else
                {
                    _notificationService.ShowError("Failed to reset CPU cores");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error resetting CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimize the CPU cores
        /// </summary>
        private void OptimizeCores()
        {
            try
            {
                var result = _systemInfoService.OptimizeCpuCores();
                
                if (result)
                {
                    _notificationService.ShowSuccess("CPU cores optimized for best performance");
                    UpdateCores();
                }
                else
                {
                    _notificationService.ShowError("Failed to optimize CPU cores");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error optimizing CPU cores: {ex.Message}");
            }
        }
    }
}