using System;
using System.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// System information model
    /// </summary>
    public class SystemInfo : INotifyPropertyChanged
    {
        private string _operatingSystem = string.Empty;
        private string _osVersion = string.Empty;
        private string _processorName = string.Empty;
        private int _processorCores;
        private int _processorLogicalCores;
        private int _performanceCoreCount;
        private int _efficiencyCoreCount;
        private bool _supportsThreadDirector;
        private double _cpuUsage;
        private double _totalMemoryGB;
        private double _availableMemoryGB;
        private TimeSpan _upTime;
        private bool _isOnBattery;
        private double? _batteryChargePercent;
        private string _currentPowerProfile = string.Empty;
        private string _gpuName = string.Empty;
        private double _gpuUsage;
        
        /// <summary>
        /// Operating system name
        /// </summary>
        public string OperatingSystem
        {
            get => _operatingSystem;
            set
            {
                if (_operatingSystem != value)
                {
                    _operatingSystem = value;
                    OnPropertyChanged(nameof(OperatingSystem));
                }
            }
        }
        
        /// <summary>
        /// OS version
        /// </summary>
        public string OsVersion
        {
            get => _osVersion;
            set
            {
                if (_osVersion != value)
                {
                    _osVersion = value;
                    OnPropertyChanged(nameof(OsVersion));
                }
            }
        }
        
        /// <summary>
        /// Processor name
        /// </summary>
        public string ProcessorName
        {
            get => _processorName;
            set
            {
                if (_processorName != value)
                {
                    _processorName = value;
                    OnPropertyChanged(nameof(ProcessorName));
                }
            }
        }
        
        /// <summary>
        /// Number of processor cores
        /// </summary>
        public int ProcessorCores
        {
            get => _processorCores;
            set
            {
                if (_processorCores != value)
                {
                    _processorCores = value;
                    OnPropertyChanged(nameof(ProcessorCores));
                    OnPropertyChanged(nameof(ProcessorDescription));
                }
            }
        }
        
        /// <summary>
        /// Number of logical processor cores
        /// </summary>
        public int ProcessorLogicalCores
        {
            get => _processorLogicalCores;
            set
            {
                if (_processorLogicalCores != value)
                {
                    _processorLogicalCores = value;
                    OnPropertyChanged(nameof(ProcessorLogicalCores));
                    OnPropertyChanged(nameof(ProcessorDescription));
                }
            }
        }
        
        /// <summary>
        /// Number of performance cores
        /// </summary>
        public int PerformanceCoreCount
        {
            get => _performanceCoreCount;
            set
            {
                if (_performanceCoreCount != value)
                {
                    _performanceCoreCount = value;
                    OnPropertyChanged(nameof(PerformanceCoreCount));
                    OnPropertyChanged(nameof(IsHybridProcessor));
                    OnPropertyChanged(nameof(ProcessorDescription));
                }
            }
        }
        
        /// <summary>
        /// Number of efficiency cores
        /// </summary>
        public int EfficiencyCoreCount
        {
            get => _efficiencyCoreCount;
            set
            {
                if (_efficiencyCoreCount != value)
                {
                    _efficiencyCoreCount = value;
                    OnPropertyChanged(nameof(EfficiencyCoreCount));
                    OnPropertyChanged(nameof(IsHybridProcessor));
                    OnPropertyChanged(nameof(ProcessorDescription));
                }
            }
        }
        
        /// <summary>
        /// Whether the processor supports thread director
        /// </summary>
        public bool SupportsThreadDirector
        {
            get => _supportsThreadDirector;
            set
            {
                if (_supportsThreadDirector != value)
                {
                    _supportsThreadDirector = value;
                    OnPropertyChanged(nameof(SupportsThreadDirector));
                }
            }
        }
        
        /// <summary>
        /// CPU usage percentage
        /// </summary>
        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (Math.Abs(_cpuUsage - value) > 0.1)
                {
                    _cpuUsage = value;
                    OnPropertyChanged(nameof(CpuUsage));
                }
            }
        }
        
        /// <summary>
        /// Total memory in GB
        /// </summary>
        public double TotalMemoryGB
        {
            get => _totalMemoryGB;
            set
            {
                if (Math.Abs(_totalMemoryGB - value) > 0.1)
                {
                    _totalMemoryGB = value;
                    OnPropertyChanged(nameof(TotalMemoryGB));
                    OnPropertyChanged(nameof(MemoryUsagePercent));
                    OnPropertyChanged(nameof(MemoryDescription));
                }
            }
        }
        
        /// <summary>
        /// Available memory in GB
        /// </summary>
        public double AvailableMemoryGB
        {
            get => _availableMemoryGB;
            set
            {
                if (Math.Abs(_availableMemoryGB - value) > 0.1)
                {
                    _availableMemoryGB = value;
                    OnPropertyChanged(nameof(AvailableMemoryGB));
                    OnPropertyChanged(nameof(MemoryUsagePercent));
                    OnPropertyChanged(nameof(MemoryDescription));
                }
            }
        }
        
        /// <summary>
        /// Memory usage percent
        /// </summary>
        public double MemoryUsagePercent
        {
            get
            {
                if (TotalMemoryGB > 0)
                {
                    return 100.0 * (TotalMemoryGB - AvailableMemoryGB) / TotalMemoryGB;
                }
                
                return 0;
            }
        }
        
        /// <summary>
        /// Memory usage in GB
        /// </summary>
        public double MemoryUsageGB => TotalMemoryGB - AvailableMemoryGB;
        
        /// <summary>
        /// Memory description
        /// </summary>
        public string MemoryDescription
        {
            get
            {
                return $"{MemoryUsageGB:F1} GB / {TotalMemoryGB:F1} GB";
            }
        }
        
        /// <summary>
        /// System uptime
        /// </summary>
        public TimeSpan UpTime
        {
            get => _upTime;
            set
            {
                if (_upTime != value)
                {
                    _upTime = value;
                    OnPropertyChanged(nameof(UpTime));
                }
            }
        }
        
        /// <summary>
        /// Whether the system is running on battery
        /// </summary>
        public bool IsOnBattery
        {
            get => _isOnBattery;
            set
            {
                if (_isOnBattery != value)
                {
                    _isOnBattery = value;
                    OnPropertyChanged(nameof(IsOnBattery));
                }
            }
        }
        
        /// <summary>
        /// Battery charge percent
        /// </summary>
        public double? BatteryChargePercent
        {
            get => _batteryChargePercent;
            set
            {
                if (_batteryChargePercent != value)
                {
                    _batteryChargePercent = value;
                    OnPropertyChanged(nameof(BatteryChargePercent));
                }
            }
        }
        
        /// <summary>
        /// Current power profile
        /// </summary>
        public string CurrentPowerProfile
        {
            get => _currentPowerProfile;
            set
            {
                if (_currentPowerProfile != value)
                {
                    _currentPowerProfile = value;
                    OnPropertyChanged(nameof(CurrentPowerProfile));
                }
            }
        }
        
        /// <summary>
        /// GPU name
        /// </summary>
        public string GpuName
        {
            get => _gpuName;
            set
            {
                if (_gpuName != value)
                {
                    _gpuName = value;
                    OnPropertyChanged(nameof(GpuName));
                }
            }
        }
        
        /// <summary>
        /// GPU usage percentage
        /// </summary>
        public double GpuUsage
        {
            get => _gpuUsage;
            set
            {
                if (Math.Abs(_gpuUsage - value) > 0.1)
                {
                    _gpuUsage = value;
                    OnPropertyChanged(nameof(GpuUsage));
                }
            }
        }
        
        /// <summary>
        /// Whether the processor is a hybrid processor
        /// </summary>
        public bool IsHybridProcessor => EfficiencyCoreCount > 0;
        
        /// <summary>
        /// Processor description
        /// </summary>
        public string ProcessorDescription
        {
            get
            {
                if (IsHybridProcessor)
                {
                    return $"{ProcessorCores} cores ({PerformanceCoreCount}P + {EfficiencyCoreCount}E), {ProcessorLogicalCores} threads";
                }
                else
                {
                    return $"{ProcessorCores} cores, {ProcessorLogicalCores} threads";
                }
            }
        }
        
        /// <summary>
        /// PropertyChanged event
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;
        
        /// <summary>
        /// Raise PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Property name</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}