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
    /// View model for the processes view
    /// </summary>
    public class ProcessesViewModel : ViewModelBase
    {
        private readonly IProcessService _processService;
        private readonly INotificationService _notificationService;
        
        private ProcessInfo? _selectedProcess;
        private string _filterText = string.Empty;
        private bool _showSystemProcesses = false;

        /// <summary>
        /// List of running processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; } = new ObservableCollection<ProcessInfo>();

        /// <summary>
        /// Filtered processes based on search text and filter options
        /// </summary>
        public ObservableCollection<ProcessInfo> FilteredProcesses { get; } = new ObservableCollection<ProcessInfo>();
        
        /// <summary>
        /// Currently selected process
        /// </summary>
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
        }
        
        /// <summary>
        /// Text to filter processes
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }
        
        /// <summary>
        /// Whether to show system processes
        /// </summary>
        public bool ShowSystemProcesses
        {
            get => _showSystemProcesses;
            set
            {
                if (SetProperty(ref _showSystemProcesses, value))
                {
                    ApplyFilter();
                }
            }
        }

        // Commands
        /// <summary>
        /// Command to refresh the process list
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Command to set process affinity
        /// </summary>
        public ICommand SetAffinityCommand { get; }
        
        /// <summary>
        /// Command to set process priority
        /// </summary>
        public ICommand SetPriorityCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            // Get services
            _processService = ServiceLocator.Get<IProcessService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshProcesses());
            SetAffinityCommand = new RelayCommand(SetAffinity, CanModifyProcess);
            SetPriorityCommand = new RelayCommand(SetPriority, CanModifyProcess);
        }
        
        /// <summary>
        /// Initialize the view model
        /// </summary>
        public override void Initialize()
        {
            RefreshProcesses();
            
            // TODO: Start a timer to periodically refresh process information
        }
        
        /// <summary>
        /// Refresh the process list
        /// </summary>
        private void RefreshProcesses()
        {
            try
            {
                Processes.Clear();
                
                var processes = _processService.GetRunningProcesses();
                foreach (var process in processes)
                {
                    Processes.Add(process);
                }
                
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply the current filter to the process list
        /// </summary>
        private void ApplyFilter()
        {
            FilteredProcesses.Clear();
            
            var filtered = Processes.AsEnumerable();
            
            // Apply text filter if provided
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var filterLower = FilterText.ToLowerInvariant();
                filtered = filtered.Where(p => p.Name.ToLowerInvariant().Contains(filterLower));
            }
            
            // Apply system process filter
            if (!ShowSystemProcesses)
            {
                filtered = filtered.Where(p => !p.IsSystemProcess);
            }
            
            // Add filtered processes
            foreach (var process in filtered)
            {
                FilteredProcesses.Add(process);
            }
        }
        
        /// <summary>
        /// Check if the selected process can be modified
        /// </summary>
        private bool CanModifyProcess(object? parameter)
        {
            return SelectedProcess != null && SelectedProcess.IsOptimizable;
        }
        
        /// <summary>
        /// Set the CPU affinity for the selected process
        /// </summary>
        private void SetAffinity(object? parameter)
        {
            if (SelectedProcess == null) return;
            
            try
            {
                // TODO: Show affinity dialog and apply settings
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting affinity: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set the priority for the selected process
        /// </summary>
        private void SetPriority(object? parameter)
        {
            if (SelectedProcess == null) return;
            
            try
            {
                if (parameter is not ProcessPriorityClass priority) return;
                
                var result = _processService.SetProcessPriority(SelectedProcess.Id, priority);
                if (result)
                {
                    SelectedProcess.Priority = priority;
                    _notificationService.ShowSuccess($"Priority for {SelectedProcess.Name} set to {priority}");
                }
                else
                {
                    _notificationService.ShowError("Failed to set process priority");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting priority: {ex.Message}");
            }
        }
    }
}