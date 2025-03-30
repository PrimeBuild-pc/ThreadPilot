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
    /// View model for processes tab
    /// </summary>
    public class ProcessesViewModel : ViewModelBase
    {
        private readonly INotificationService _notificationService;
        private readonly IProcessService _processService;
        
        private ObservableCollection<ProcessInfo> _processes = new ObservableCollection<ProcessInfo>();
        private ICollectionView _filteredProcesses;
        private ProcessInfo? _selectedProcess;
        private string _filterText = string.Empty;
        private bool _showSystemProcesses;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            // Get services
            _notificationService = ServiceLocator.Get<INotificationService>();
            _processService = ServiceLocator.Get<IProcessService>();
            
            // Initialize commands
            RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
            SetProcessPriorityCommand = new RelayCommand<ProcessPriority>(SetProcessPriority, CanManageProcess);
            SetProcessAffinityCommand = new RelayCommand(SetProcessAffinity, CanManageProcess);
            
            // Initialize collection view
            _filteredProcesses = CollectionViewSource.GetDefaultView(_processes);
            _filteredProcesses.Filter = ProcessFilter;
            
            // Load data
            RefreshProcesses();
        }
        
        /// <summary>
        /// All processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes
        {
            get => _processes;
            set
            {
                if (SetProperty(ref _processes, value))
                {
                    _filteredProcesses = CollectionViewSource.GetDefaultView(_processes);
                    _filteredProcesses.Filter = ProcessFilter;
                    OnPropertyChanged(nameof(FilteredProcesses));
                }
            }
        }
        
        /// <summary>
        /// Filtered processes (based on filter text and system process flag)
        /// </summary>
        public ICollectionView FilteredProcesses => _filteredProcesses;
        
        /// <summary>
        /// Selected process
        /// </summary>
        public ProcessInfo? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    // Update command states
                    (SetProcessPriorityCommand as RelayCommand<ProcessPriority>)?.RaiseCanExecuteChanged();
                    (SetProcessAffinityCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Filter text
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    FilteredProcesses.Refresh();
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
                    FilteredProcesses.Refresh();
                }
            }
        }
        
        /// <summary>
        /// Refresh processes command
        /// </summary>
        public RelayCommand RefreshProcessesCommand { get; }
        
        /// <summary>
        /// Set process priority command
        /// </summary>
        public RelayCommand<ProcessPriority> SetProcessPriorityCommand { get; }
        
        /// <summary>
        /// Set process affinity command
        /// </summary>
        public RelayCommand SetProcessAffinityCommand { get; }
        
        /// <summary>
        /// Refresh processes
        /// </summary>
        public async void RefreshProcesses()
        {
            try
            {
                // Get processes on background thread
                var processes = await Task.Run(() => _processService.GetProcesses());
                
                // Update collection on UI thread
                App.Current.Dispatcher.Invoke(() =>
                {
                    Processes.Clear();
                    foreach (var process in processes)
                    {
                        Processes.Add(process);
                    }
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set process priority
        /// </summary>
        /// <param name="priority">New priority</param>
        private void SetProcessPriority(ProcessPriority priority)
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                if (_processService.SetProcessPriority(SelectedProcess.Id, priority))
                {
                    SelectedProcess.Priority = priority;
                    _notificationService.ShowSuccess(
                        $"Process '{SelectedProcess.Name}' priority set to {priority}.");
                }
                else
                {
                    _notificationService.ShowError(
                        $"Failed to set priority for process '{SelectedProcess.Name}'.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError(
                    $"Error setting process priority: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set process affinity
        /// </summary>
        private void SetProcessAffinity()
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            // TODO: Implement affinity dialog
            _notificationService.ShowInfo("Affinity dialog will be implemented in a future version.");
        }
        
        /// <summary>
        /// Check if process can be managed
        /// </summary>
        /// <param name="parameter">Parameter</param>
        /// <returns>True if can manage</returns>
        private bool CanManageProcess(object? parameter)
        {
            return SelectedProcess != null;
        }
        
        /// <summary>
        /// Process filter
        /// </summary>
        /// <param name="item">Process to filter</param>
        /// <returns>True if process should be shown</returns>
        private bool ProcessFilter(object item)
        {
            if (item is not ProcessInfo process)
            {
                return false;
            }
            
            // Filter by system process flag
            if (!ShowSystemProcesses && process.IsSystemProcess)
            {
                return false;
            }
            
            // Filter by text
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                return true;
            }
            
            // Check if process name or description contains filter text
            return process.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                   process.Description.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }
    }
}