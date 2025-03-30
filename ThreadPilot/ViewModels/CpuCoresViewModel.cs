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
    /// CPU cores view model
    /// </summary>
    public class CpuCoresViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private CpuCore _selectedCore;
        private bool _showOnlyPerformanceCores;
        private bool _showOnlyEfficiencyCores;
        private string _searchText;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CpuCoresViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Resolve<ISystemInfoService>();
            
            // Initialize properties
            CpuCores = new ObservableCollection<CpuCore>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshCores());
            
            // Initial load
            RefreshCores();
        }
        
        /// <summary>
        /// CPU cores collection
        /// </summary>
        public ObservableCollection<CpuCore> CpuCores { get; }
        
        /// <summary>
        /// Selected core
        /// </summary>
        public CpuCore SelectedCore
        {
            get => _selectedCore;
            set => SetProperty(ref _selectedCore, value);
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether to show only performance cores
        /// </summary>
        public bool ShowOnlyPerformanceCores
        {
            get => _showOnlyPerformanceCores;
            set
            {
                if (SetProperty(ref _showOnlyPerformanceCores, value))
                {
                    // Can't have both filters enabled
                    if (value && ShowOnlyEfficiencyCores)
                    {
                        ShowOnlyEfficiencyCores = false;
                    }
                    
                    RefreshCores();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether to show only efficiency cores
        /// </summary>
        public bool ShowOnlyEfficiencyCores
        {
            get => _showOnlyEfficiencyCores;
            set
            {
                if (SetProperty(ref _showOnlyEfficiencyCores, value))
                {
                    // Can't have both filters enabled
                    if (value && ShowOnlyPerformanceCores)
                    {
                        ShowOnlyPerformanceCores = false;
                    }
                    
                    RefreshCores();
                }
            }
        }
        
        /// <summary>
        /// Search text
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    RefreshCores();
                }
            }
        }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Refresh cores
        /// </summary>
        private void RefreshCores()
        {
            if (_systemInfoService == null)
            {
                return;
            }
            
            CpuCores.Clear();
            
            var systemInfo = _systemInfoService.GetSystemInfo();
            var cores = systemInfo?.CpuCores?.ToArray() ?? Array.Empty<CpuCore>();
            
            // Apply filters
            if (ShowOnlyPerformanceCores)
            {
                cores = cores.Where(c => c.IsPerformanceCore).ToArray();
            }
            else if (ShowOnlyEfficiencyCores)
            {
                cores = cores.Where(c => !c.IsPerformanceCore).ToArray();
            }
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                cores = cores.Where(c => c.CoreName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            
            foreach (var core in cores)
            {
                CpuCores.Add(core);
            }
            
            // Reset selection
            SelectedCore = null;
        }
    }
}