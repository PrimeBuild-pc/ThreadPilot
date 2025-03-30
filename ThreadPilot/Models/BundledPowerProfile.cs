using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ThreadPilot.Models
{
    /// <summary>
    /// A bundled power profile that includes process affinity settings, 
    /// Windows power plan settings, and other optimization settings
    /// </summary>
    public class BundledPowerProfile : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _author = string.Empty;
        private DateTime _creationDate;
        private Guid _id = Guid.NewGuid();
        private bool _isReadOnly;
        private bool _isActive;
        private byte[] _powerPlanData = Array.Empty<byte>();
        private List<ProcessAffinityRule> _processRules = new List<ProcessAffinityRule>();
        private OptimizationSettings _optimizationSettings = new OptimizationSettings();

        /// <summary>
        /// Event that fires when a property changes value
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Unique identifier for the profile
        /// </summary>
        public Guid Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        /// <summary>
        /// Profile name
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        /// <summary>
        /// Profile description
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        /// <summary>
        /// Profile author
        /// </summary>
        public string Author
        {
            get => _author;
            set
            {
                _author = value;
                OnPropertyChanged(nameof(Author));
            }
        }

        /// <summary>
        /// Profile creation date
        /// </summary>
        public DateTime CreationDate
        {
            get => _creationDate;
            set
            {
                _creationDate = value;
                OnPropertyChanged(nameof(CreationDate));
            }
        }

        /// <summary>
        /// Whether the profile is read-only (system profile)
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                _isReadOnly = value;
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }

        /// <summary>
        /// Whether the profile is currently active
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }

        /// <summary>
        /// Raw data for Windows power plan
        /// </summary>
        public byte[] PowerPlanData
        {
            get => _powerPlanData;
            set
            {
                _powerPlanData = value;
                OnPropertyChanged(nameof(PowerPlanData));
            }
        }

        /// <summary>
        /// Process affinity and priority rules
        /// </summary>
        public List<ProcessAffinityRule> ProcessRules
        {
            get => _processRules;
            set
            {
                _processRules = value;
                OnPropertyChanged(nameof(ProcessRules));
            }
        }

        /// <summary>
        /// Various optimization settings
        /// </summary>
        public OptimizationSettings OptimizationSettings
        {
            get => _optimizationSettings;
            set
            {
                _optimizationSettings = value;
                OnPropertyChanged(nameof(OptimizationSettings));
            }
        }

        /// <summary>
        /// Fire the property changed event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// A rule for setting process affinity and priority
    /// </summary>
    public class ProcessAffinityRule
    {
        /// <summary>
        /// Process name pattern
        /// </summary>
        public string ProcessNamePattern { get; set; } = string.Empty;

        /// <summary>
        /// CPU affinity mask
        /// </summary>
        public long AffinityMask { get; set; }

        /// <summary>
        /// Process priority
        /// </summary>
        public ProcessPriorityClass Priority { get; set; } = ProcessPriorityClass.Normal;

        /// <summary>
        /// Whether to automatically apply the rule when a matching process starts
        /// </summary>
        public bool AutoApply { get; set; } = true;
    }

    /// <summary>
    /// General optimization settings
    /// </summary>
    public class OptimizationSettings
    {
        /// <summary>
        /// Whether to disable unnecessary background services
        /// </summary>
        public bool DisableBackgroundServices { get; set; }

        /// <summary>
        /// Whether to optimize Windows Defender
        /// </summary>
        public bool OptimizeDefender { get; set; }

        /// <summary>
        /// Whether to disable hardware acceleration in browsers
        /// </summary>
        public bool DisableHardwareAcceleration { get; set; }

        /// <summary>
        /// Whether to optimize network settings
        /// </summary>
        public bool OptimizeNetwork { get; set; }

        /// <summary>
        /// Whether to customize NVIDIA driver settings
        /// </summary>
        public bool OptimizeNvidiaDrivers { get; set; }

        /// <summary>
        /// Whether to customize AMD driver settings
        /// </summary>
        public bool OptimizeAmdDrivers { get; set; }

        /// <summary>
        /// Whether to enable gaming mode
        /// </summary>
        public bool EnableGamingMode { get; set; }

        /// <summary>
        /// Whether to disable Windows Update during gaming
        /// </summary>
        public bool DisableUpdates { get; set; }
    }
}