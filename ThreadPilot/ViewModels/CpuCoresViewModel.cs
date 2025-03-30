using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using ThreadPilot.Commands;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for CPU cores tab
    /// </summary>
    public class CpuCoresViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private readonly ISystemInfoService _systemInfoService;
        
        private ObservableCollection<CpuCore> _cores = new ObservableCollection<CpuCore>();
        private ICollectionView _filteredCores;
        private CpuCore? _selectedCore;
        private bool _showPerformanceCores = true;
        private bool _showEfficiencyCores = true;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.Get<INotificationService>();
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            
            // Initialize commands
            ResetAllCoresCommand = new RelayCommand(ResetAllCores);
            OptimizeCommand = new RelayCommand(Optimize);
            ParkCoreCommand = new RelayCommand<bool>(ParkCore, CanParkCore);
            
            // Initialize collection view
            _filteredCores = CollectionViewSource.GetDefaultView(_cores);
            _filteredCores.Filter = CoreFilter;
            
            // Load data
            RefreshCores();
        }
        
        /// <summary>
        /// All cores
        /// </summary>
        public ObservableCollection<CpuCore> Cores
        {
            get => _cores;
            set
            {
                if (SetProperty(ref _cores, value))
                {
                    _filteredCores = CollectionViewSource.GetDefaultView(_cores);
                    _filteredCores.Filter = CoreFilter;
                    OnPropertyChanged(nameof(FilteredCores));
                }
            }
        }
        
        /// <summary>
        /// Filtered cores (based on core type filters)
        /// </summary>
        public ICollectionView FilteredCores => _filteredCores;
        
        /// <summary>
        /// Selected core
        /// </summary>
        public CpuCore? SelectedCore
        {
            get => _selectedCore;
            set
            {
                if (SetProperty(ref _selectedCore, value))
                {
                    // Update command states
                    (ParkCoreCommand as RelayCommand<bool>)?.RaiseCanExecuteChanged();
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
                    FilteredCores.Refresh();
                }
            }
        }
        
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
                    FilteredCores.Refresh();
                }
            }
        }
        
        /// <summary>
        /// Reset all cores command
        /// </summary>
        public RelayCommand ResetAllCoresCommand { get; }
        
        /// <summary>
        /// Optimize command
        /// </summary>
        public RelayCommand OptimizeCommand { get; }
        
        /// <summary>
        /// Park core command
        /// </summary>
        public RelayCommand<bool> ParkCoreCommand { get; }
        
        /// <summary>
        /// Refresh cores
        /// </summary>
        public async void RefreshCores()
        {
            try
            {
                // Get cores on background thread
                var cores = await Task.Run(() => _systemInfoService.GetCpuCores());
                
                // Update collection on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    Cores.Clear();
                    foreach (var core in cores)
                    {
                        Cores.Add(core);
                    }
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reset all cores
        /// </summary>
        private void ResetAllCores()
        {
            try
            {
                if (_systemInfoService.ResetCpuCores())
                {
                    _notificationService.ShowSuccess("CPU cores reset to default settings.");
                    RefreshCores();
                }
                else
                {
                    _notificationService.ShowError("Failed to reset CPU cores.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error resetting CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Optimize CPU cores
        /// </summary>
        private void Optimize()
        {
            try
            {
                if (_systemInfoService.OptimizeCpuCores())
                {
                    _notificationService.ShowSuccess("CPU cores optimized for best performance.");
                    RefreshCores();
                }
                else
                {
                    _notificationService.ShowError("Failed to optimize CPU cores.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error optimizing CPU cores: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Park or unpark a core
        /// </summary>
        /// <param name="park">Whether to park or unpark</param>
        private void ParkCore(bool park)
        {
            if (SelectedCore == null)
            {
                return;
            }
            
            // TODO: Implement core parking
            _notificationService.ShowInfo("Core parking will be implemented in a future version.");
        }
        
        /// <summary>
        /// Check if core can be parked
        /// </summary>
        /// <param name="parameter">Parameter</param>
        /// <returns>True if can park</returns>
        private bool CanParkCore(object? parameter)
        {
            return SelectedCore != null;
        }
        
        /// <summary>
        /// Core filter
        /// </summary>
        /// <param name="item">Core to filter</param>
        /// <returns>True if core should be shown</returns>
        private bool CoreFilter(object item)
        {
            if (item is not CpuCore core)
            {
                return false;
            }
            
            // Filter by core type
            if (core.IsEfficiencyCore)
            {
                return ShowEfficiencyCores;
            }
            else
            {
                return ShowPerformanceCores;
            }
        }
    }
}