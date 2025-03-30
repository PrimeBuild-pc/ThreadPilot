using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// View model for the dashboard view
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly ISystemInfoService _systemInfoService;
        private readonly IProcessService _processService;
        
        private SystemInfo _systemInfo = new SystemInfo();
        private double _cpuUsage;
        private double _memoryUsage;
        private string _cpuName = string.Empty;
        private int _coreCount;
        private int _threadCount;
        private double _ramTotal;
        
        /// <summary>
        /// System information
        /// </summary>
        public SystemInfo SystemInfo
        {
            get => _systemInfo;
            set => SetProperty(ref _systemInfo, value);
        }
        
        /// <summary>
        /// Current CPU usage percentage
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }
        
        /// <summary>
        /// Current memory usage percentage
        /// </summary>
        public double MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }
        
        /// <summary>
        /// CPU model name
        /// </summary>
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }
        
        /// <summary>
        /// Number of physical CPU cores
        /// </summary>
        public int CoreCount
        {
            get => _coreCount;
            set => SetProperty(ref _coreCount, value);
        }
        
        /// <summary>
        /// Number of logical CPU threads
        /// </summary>
        public int ThreadCount
        {
            get => _threadCount;
            set => SetProperty(ref _threadCount, value);
        }
        
        /// <summary>
        /// Total RAM in GB
        /// </summary>
        public double RamTotal
        {
            get => _ramTotal;
            set => SetProperty(ref _ramTotal, value);
        }
        
        /// <summary>
        /// Top processes by CPU usage
        /// </summary>
        public ObservableCollection<ProcessInfo> TopProcesses { get; } = new ObservableCollection<ProcessInfo>();
        
        /// <summary>
        /// Command to refresh dashboard data
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public DashboardViewModel()
        {
            // Get services
            _systemInfoService = ServiceLocator.Get<ISystemInfoService>();
            _processService = ServiceLocator.Get<IProcessService>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshData());
        }
        
        /// <summary>
        /// Initialize the view model
        /// </summary>
        public override void Initialize()
        {
            RefreshData();
            
            // TODO: Start a timer to periodically refresh data
        }
        
        /// <summary>
        /// Refresh dashboard data
        /// </summary>
        private void RefreshData()
        {
            try
            {
                // Update system information
                SystemInfo = _systemInfoService.GetSystemInfo();
                
                // Update CPU and memory usage
                CpuUsage = _systemInfoService.GetCpuUsage();
                MemoryUsage = _systemInfoService.GetMemoryUsage();
                
                // Update CPU info
                CpuName = SystemInfo.CpuName;
                CoreCount = SystemInfo.CoreCount;
                ThreadCount = SystemInfo.ProcessorCount;
                RamTotal = SystemInfo.TotalRam;
                
                // Update top processes
                UpdateTopProcesses();
            }
            catch (Exception)
            {
                // Handle errors
            }
        }
        
        /// <summary>
        /// Update the list of top processes by CPU usage
        /// </summary>
        private void UpdateTopProcesses()
        {
            TopProcesses.Clear();
            
            var processes = _processService.GetRunningProcesses();
            
            // Sort by CPU usage and take top 5
            var topProcesses = processes
                .OrderByDescending(p => p.CpuUsage)
                .Take(5);
                
            foreach (var process in topProcesses)
            {
                TopProcesses.Add(process);
            }
        }
    }
}