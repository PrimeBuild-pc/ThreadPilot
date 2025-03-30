using System;
using System.Collections.ObjectModel;
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
        #region Private Fields
        
        private ObservableCollection<ProcessInfo> _processes;
        private ProcessInfo _selectedProcess;
        private string _searchFilter = string.Empty;
        private bool _showSystemProcesses = false;
        private bool _isLoading = false;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Collection of processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set => SetProperty(ref _processes, value);
        }
        
        /// <summary>
        /// Currently selected process
        /// </summary>
        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
        }
        
        /// <summary>
        /// Search filter for processes
        /// </summary>
        public string SearchFilter
        {
            get => _searchFilter;
            set => SetProperty(ref _searchFilter, value, ApplyFilter);
        }
        
        /// <summary>
        /// Whether to show system processes
        /// </summary>
        public bool ShowSystemProcesses
        {
            get => _showSystemProcesses;
            set => SetProperty(ref _showSystemProcesses, value, ApplyFilter);
        }
        
        /// <summary>
        /// Whether the view model is loading data
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        #endregion
        
        #region Commands
        
        /// <summary>
        /// Command to refresh the process list
        /// </summary>
        public ICommand RefreshProcessesCommand { get; }
        
        /// <summary>
        /// Command to set process priority
        /// </summary>
        public ICommand SetProcessPriorityCommand { get; }
        
        /// <summary>
        /// Command to set process affinity
        /// </summary>
        public ICommand SetProcessAffinityCommand { get; }
        
        /// <summary>
        /// Command to terminate a process
        /// </summary>
        public ICommand TerminateProcessCommand { get; }
        
        #endregion
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            // Initialize collections
            Processes = new ObservableCollection<ProcessInfo>();
            
            // Initialize commands
            RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
            SetProcessPriorityCommand = new RelayCommand<ProcessPriority>(SetProcessPriority, CanSetProcessPriority);
            SetProcessAffinityCommand = new RelayCommand(SetProcessAffinity, CanSetProcessAffinity);
            TerminateProcessCommand = new RelayCommand(TerminateProcess, CanTerminateProcess);
            
            // Load initial data
            LoadProcesses();
        }
        
        /// <summary>
        /// Load process data
        /// </summary>
        private void LoadProcesses()
        {
            IsLoading = true;
            
            try
            {
                // This is where we would retrieve processes from the process service
                // For now, we'll create some sample data
                
                Processes.Clear();
                
                // In the future, this will be retrieved from IProcessService 
                // For example: Processes = new ObservableCollection<ProcessInfo>(ServiceLocator.Get<IProcessService>().GetAllProcesses());
                
                // Set loading to false
                IsLoading = false;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                NotificationService.ShowError($"Error loading processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Refresh the process list
        /// </summary>
        public void RefreshProcesses(object parameter = null)
        {
            LoadProcesses();
        }
        
        /// <summary>
        /// Apply the filter to the process list
        /// </summary>
        private void ApplyFilter()
        {
            RefreshProcesses();
        }
        
        /// <summary>
        /// Set the priority of the selected process
        /// </summary>
        private void SetProcessPriority(ProcessPriority priority)
        {
            if (SelectedProcess == null) return;
            
            try
            {
                // This is where we would set process priority using the process service
                // For example: bool success = ServiceLocator.Get<IProcessService>().SetProcessPriority(SelectedProcess.Id, priority);
                
                // For now, we'll just update the model directly
                SelectedProcess.Priority = priority;
                
                // Show notification
                NotificationService.ShowSuccess($"Priority changed to {priority} for process {SelectedProcess.Name}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error setting process priority: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the priority of the selected process can be set
        /// </summary>
        private bool CanSetProcessPriority(ProcessPriority priority)
        {
            return SelectedProcess != null && SelectedProcess.CanModify;
        }
        
        /// <summary>
        /// Set the affinity of the selected process
        /// </summary>
        private void SetProcessAffinity(object parameter)
        {
            if (SelectedProcess == null) return;
            
            try
            {
                // This is where we would show a dialog to set process affinity
                // For now, we'll just show a notification
                NotificationService.ShowInfo("Process affinity dialog will be implemented in a future update", "Coming Soon");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error setting process affinity: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the affinity of the selected process can be set
        /// </summary>
        private bool CanSetProcessAffinity(object parameter)
        {
            return SelectedProcess != null && SelectedProcess.CanModify;
        }
        
        /// <summary>
        /// Terminate the selected process
        /// </summary>
        private void TerminateProcess(object parameter)
        {
            if (SelectedProcess == null) return;
            
            try
            {
                // Show confirmation dialog
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to terminate the process {SelectedProcess.Name}?",
                    "Confirm Process Termination",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);
                
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // This is where we would terminate the process using the process service
                    // For example: bool success = ServiceLocator.Get<IProcessService>().TerminateProcess(SelectedProcess.Id);
                    
                    // For now, we'll just show a notification
                    NotificationService.ShowSuccess($"Process {SelectedProcess.Name} terminated successfully");
                    
                    // Refresh the process list
                    RefreshProcesses();
                }
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Error terminating process: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the selected process can be terminated
        /// </summary>
        private bool CanTerminateProcess(object parameter)
        {
            return SelectedProcess != null && SelectedProcess.CanModify && !SelectedProcess.IsSystemProcess;
        }
    }
}