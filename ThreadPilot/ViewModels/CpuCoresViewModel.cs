using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ThreadPilot.Helpers;
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
        
        private SystemInfo _systemInfo = new SystemInfo();
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// CPU cores collection
        /// </summary>
        public ObservableCollection<CpuCoreViewModel> Cores { get; } = new ObservableCollection<CpuCoreViewModel>();
        
        /// <summary>
        /// Command to refresh core information
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshCoreInfo());
        }
        
        /// <summary>
        /// Initialize the view model
        /// </summary>
        public override void Initialize()
        {
            RefreshCoreInfo();
            
            // TODO: Start a timer to periodically refresh core information
        }
        
        /// <summary>
        /// Refresh CPU core information
        /// </summary>
        private void RefreshCoreInfo()
        {
            try
            {
                // Update system information
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update cores
                UpdateCores();
            }
            catch (Exception)
            {
                // Handle errors
            }
        }
        
        /// <summary>
        /// Update the CPU core list
        /// </summary>
        private void UpdateCores()
        {
            Cores.Clear();
            
            // Add each core
            foreach (var core in SystemInfo.Cores)
            {
                Cores.Add(new CpuCoreViewModel(core));
            }
        }
    }
    
    /// <summary>
    /// View model for a single CPU core
    /// </summary>
    public class CpuCoreViewModel : ViewModelBase
    {
        private CpuCore _core;
        
        /// <summary>
        /// Core index
        /// </summary>
        public int Index => _core.Index;
        
        /// <summary>
        /// Whether this is a logical or physical core
        /// </summary>
        public bool IsLogical => _core.IsLogical;
        
        /// <summary>
        /// Core usage percentage
        /// </summary>
        public double Usage => _core.Usage;
        
        /// <summary>
        /// Core temperature in Celsius (if available)
        /// </summary>
        public double? Temperature => _core.Temperature;
        
        /// <summary>
        /// Core frequency in MHz
        /// </summary>
        public int Frequency => _core.Frequency;
        
        /// <summary>
        /// Core type display text
        /// </summary>
        public string CoreType => IsLogical ? "Logical" : "Physical";
        
        /// <summary>
        /// Formatted frequency display
        /// </summary>
        public string FrequencyFormatted => $"{Frequency} MHz";
        
        /// <summary>
        /// Formatted usage display
        /// </summary>
        public string UsageFormatted => $"{Usage:F1}%";
        
        /// <summary>
        /// Formatted temperature display
        /// </summary>
        public string TemperatureFormatted => Temperature.HasValue ? $"{Temperature:F1}°C" : "N/A";
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="core">CPU core data</param>
        public CpuCoreViewModel(CpuCore core)
        {
            _core = core;
        }
        
        /// <summary>
        /// Update the core information
        /// </summary>
        /// <param name="core">Updated CPU core data</param>
        public void Update(CpuCore core)
        {
            _core = core;
            OnPropertyChanged(string.Empty); // Notify all properties changed
        }
    }
}