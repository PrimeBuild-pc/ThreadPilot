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
    /// Processes view model
    /// </summary>
    public class ProcessesViewModel : ViewModelBase
    {
        private readonly IProcessService _processService;
        private ProcessInfo _selectedProcess;
        private string _searchText;
        private bool _showAllProcesses;

        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            // Get services
            _processService = ServiceLocator.Resolve<IProcessService>();
            
            // Initialize properties
            Processes = new ObservableCollection<ProcessInfo>();
            AvailablePriorities = new ObservableCollection<ProcessPriority>(Enum.GetValues(typeof(ProcessPriority)).Cast<ProcessPriority>());
            
            // Initialize commands
            RefreshCommand = new RelayCommand(_ => RefreshProcesses());
            SetPriorityCommand = new RelayCommand<ProcessPriority>(priority => SetProcessPriority(priority), _ => SelectedProcess != null);
            SetAffinityCommand = new RelayCommand<int[]>(coreIndices => SetProcessAffinity(coreIndices), _ => SelectedProcess != null);
            TerminateProcessCommand = new RelayCommand(_ => TerminateProcess(), _ => SelectedProcess != null);
            
            // Initial load
            RefreshProcesses();
        }
        
        /// <summary>
        /// Processes collection
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; }
        
        /// <summary>
        /// Available process priorities
        /// </summary>
        public ObservableCollection<ProcessPriority> AvailablePriorities { get; }
        
        /// <summary>
        /// Selected process
        /// </summary>
        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
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
                    // Refresh with filter
                    RefreshProcesses();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets a value indicating whether to show all processes
        /// </summary>
        public bool ShowAllProcesses
        {
            get => _showAllProcesses;
            set
            {
                if (SetProperty(ref _showAllProcesses, value))
                {
                    // Refresh with updated setting
                    RefreshProcesses();
                }
            }
        }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Set priority command
        /// </summary>
        public ICommand SetPriorityCommand { get; }
        
        /// <summary>
        /// Set affinity command
        /// </summary>
        public ICommand SetAffinityCommand { get; }
        
        /// <summary>
        /// Terminate process command
        /// </summary>
        public ICommand TerminateProcessCommand { get; }
        
        /// <summary>
        /// Refresh processes
        /// </summary>
        private void RefreshProcesses()
        {
            if (_processService == null)
            {
                return;
            }
            
            Processes.Clear();
            
            var processes = _processService.GetProcesses();
            
            // Apply filters
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                processes = processes.Where(p => 
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                    p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            
            if (!ShowAllProcesses)
            {
                // Show only processes that use CPU
                processes = processes.Where(p => p.CpuUsagePercentage > 0.1f).ToArray();
            }
            
            foreach (var process in processes)
            {
                Processes.Add(process);
            }
            
            // Reset selection
            SelectedProcess = null;
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="priority">Priority to set</param>
        private void SetProcessPriority(ProcessPriority priority)
        {
            if (SelectedProcess == null || _processService == null)
            {
                return;
            }
            
            bool success = _processService.SetProcessPriority(SelectedProcess.Id, priority);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                SelectedProcess.Priority = priority;
                OnPropertyChanged(nameof(SelectedProcess));
                
                notification?.ShowSuccess($"Priority set to {priority} for process '{SelectedProcess.Name}'.", "Priority Set");
            }
            else
            {
                notification?.ShowError($"Failed to set priority for process '{SelectedProcess.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        /// <param name="coreIndices">Core indices</param>
        private void SetProcessAffinity(int[] coreIndices)
        {
            if (SelectedProcess == null || _processService == null || coreIndices == null || coreIndices.Length == 0)
            {
                return;
            }
            
            bool success = _processService.SetProcessAffinity(SelectedProcess.Id, coreIndices);
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            if (success)
            {
                SelectedProcess.SetAffinity(coreIndices);
                OnPropertyChanged(nameof(SelectedProcess));
                
                notification?.ShowSuccess($"Affinity set for process '{SelectedProcess.Name}'.", "Affinity Set");
            }
            else
            {
                notification?.ShowError($"Failed to set affinity for process '{SelectedProcess.Name}'.", "Error");
            }
        }
        
        /// <summary>
        /// Terminate process
        /// </summary>
        private void TerminateProcess()
        {
            if (SelectedProcess == null || _processService == null)
            {
                return;
            }
            
            var notification = ServiceLocator.Resolve<INotificationService>();
            bool confirm = notification?.ShowConfirmation($"Are you sure you want to terminate process '{SelectedProcess.Name}'?", "Confirm Termination") ?? false;
            
            if (!confirm)
            {
                return;
            }
            
            bool success = _processService.TerminateProcess(SelectedProcess.Id);
            
            if (success)
            {
                Processes.Remove(SelectedProcess);
                SelectedProcess = null;
                
                notification?.ShowSuccess("Process terminated successfully.", "Process Terminated");
            }
            else
            {
                notification?.ShowError("Failed to terminate process.", "Error");
            }
        }
    }
}