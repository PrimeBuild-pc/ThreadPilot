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
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            _processService = ServiceLocator.Get<IProcessService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize commands
            RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
            SetPriorityCommand = new RelayCommand<ProcessPriority>(SetPriority);
            SetAffinityCommand = new RelayCommand<long>(SetAffinity);
            
            // Initial load of processes
            RefreshProcesses();
        }
        
        /// <summary>
        /// Collection of processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; } = new ObservableCollection<ProcessInfo>();
        
        /// <summary>
        /// Collection of processes after filtering
        /// </summary>
        public ObservableCollection<ProcessInfo> FilteredProcesses { get; } = new ObservableCollection<ProcessInfo>();
        
        /// <summary>
        /// Currently selected process
        /// </summary>
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    OnPropertyChanged(nameof(IsProcessSelected));
                }
            }
        }
        
        /// <summary>
        /// Whether a process is currently selected
        /// </summary>
        public bool IsProcessSelected => _selectedProcess != null;
        
        /// <summary>
        /// Text used to filter the process list
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
        
        /// <summary>
        /// Command to refresh the process list
        /// </summary>
        public ICommand RefreshProcessesCommand { get; }
        
        /// <summary>
        /// Command to set process priority
        /// </summary>
        public ICommand SetPriorityCommand { get; }
        
        /// <summary>
        /// Command to set process affinity
        /// </summary>
        public ICommand SetAffinityCommand { get; }
        
        /// <summary>
        /// Refresh the process list
        /// </summary>
        public void RefreshProcesses()
        {
            try
            {
                // Get all processes
                var processes = _processService.GetProcesses();
                
                // Update the collection
                Processes.Clear();
                foreach (var process in processes)
                {
                    Processes.Add(process);
                }
                
                // Apply filter
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Apply filter to the process list
        /// </summary>
        private void ApplyFilter()
        {
            try
            {
                // Filter processes
                var filtered = Processes.AsEnumerable();
                
                // Apply text filter if specified
                if (!string.IsNullOrWhiteSpace(_filterText))
                {
                    filtered = filtered.Where(p =>
                        p.Name.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
                }
                
                // Filter system processes if requested
                if (!_showSystemProcesses)
                {
                    filtered = filtered.Where(p => !p.IsSystemProcess);
                }
                
                // Sort by CPU usage
                filtered = filtered.OrderByDescending(p => p.CpuUsage);
                
                // Update the filtered collection
                FilteredProcesses.Clear();
                foreach (var process in filtered)
                {
                    FilteredProcesses.Add(process);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error applying filter: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set the priority of the selected process
        /// </summary>
        /// <param name="priority">New priority</param>
        private void SetPriority(ProcessPriority priority)
        {
            if (_selectedProcess == null)
            {
                return;
            }
            
            try
            {
                if (_processService.SetProcessPriority(_selectedProcess.Id, priority))
                {
                    _notificationService.ShowSuccess($"Priority for {_selectedProcess.Name} set to {priority}");
                    
                    // Update the process info
                    _selectedProcess.Priority = priority;
                    OnPropertyChanged(nameof(SelectedProcess));
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting priority: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set the affinity of the selected process
        /// </summary>
        /// <param name="affinityMask">New affinity mask</param>
        private void SetAffinity(long affinityMask)
        {
            if (_selectedProcess == null)
            {
                return;
            }
            
            try
            {
                if (_processService.SetProcessAffinity(_selectedProcess.Id, affinityMask))
                {
                    _notificationService.ShowSuccess($"Affinity for {_selectedProcess.Name} updated");
                    
                    // Update the process info
                    _selectedProcess.AffinityMask = affinityMask;
                    OnPropertyChanged(nameof(SelectedProcess));
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting affinity: {ex.Message}");
            }
        }
    }
}