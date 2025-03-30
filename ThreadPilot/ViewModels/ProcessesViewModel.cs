using System;
using System.Collections.ObjectModel;
using System.Timers;
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
        // Selected process
        private ProcessInfo? _selectedProcess;
        
        // Process service
        private readonly IProcessService _processService;
        
        // Notification service
        private readonly INotificationService _notificationService;
        
        // Timer for updating processes
        private readonly Timer _updateTimer;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessesViewModel()
        {
            // Get services
            _processService = ServiceLocator.Get<IProcessService>();
            _notificationService = ServiceLocator.Get<INotificationService>();
            
            // Initialize collections
            Processes = new ObservableCollection<ProcessInfo>();
            
            // Create commands
            RefreshCommand = new RelayCommand(Refresh);
            SetAffinityCommand = new RelayCommand(SetAffinity, CanModifyProcess);
            SetPriorityCommand = new RelayCommand<ProcessPriority>(SetPriority, CanModifyProcess);
            SuspendProcessCommand = new RelayCommand(SuspendProcess, CanSuspendProcess);
            ResumeProcessCommand = new RelayCommand(ResumeProcess, CanResumeProcess);
            
            // Create timer
            _updateTimer = new Timer(5000);
            _updateTimer.Elapsed += UpdateTimer_Elapsed;
            _updateTimer.Start();
            
            // Initial refresh
            Refresh(null);
        }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Set affinity command
        /// </summary>
        public ICommand SetAffinityCommand { get; }
        
        /// <summary>
        /// Set priority command
        /// </summary>
        public ICommand SetPriorityCommand { get; }
        
        /// <summary>
        /// Suspend process command
        /// </summary>
        public ICommand SuspendProcessCommand { get; }
        
        /// <summary>
        /// Resume process command
        /// </summary>
        public ICommand ResumeProcessCommand { get; }
        
        /// <summary>
        /// Processes
        /// </summary>
        public ObservableCollection<ProcessInfo> Processes { get; }
        
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
                    ((RelayCommand)SetAffinityCommand).RaiseCanExecuteChanged();
                    ((RelayCommand<ProcessPriority>)SetPriorityCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)SuspendProcessCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ResumeProcessCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Timer elapsed event handler
        /// </summary>
        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Refresh(null);
        }
        
        /// <summary>
        /// Refresh
        /// </summary>
        private void Refresh(object? parameter)
        {
            try
            {
                var selectedProcessId = SelectedProcess?.Id;
                
                // Get processes
                Processes.Clear();
                foreach (var process in _processService.GetAllProcesses())
                {
                    Processes.Add(process);
                }
                
                // Restore selected process
                if (selectedProcessId.HasValue)
                {
                    SelectedProcess = Processes.FirstOrDefault(p => p.Id == selectedProcessId);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error refreshing processes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set affinity
        /// </summary>
        private void SetAffinity(object? parameter)
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                // In a real application, we would show a dialog here
                var affinityMask = 0xFFFF; // Just for demo
                
                if (_processService.SetProcessAffinity(SelectedProcess.Id, affinityMask))
                {
                    _notificationService.ShowSuccess($"Process '{SelectedProcess.Name}' affinity set successfully");
                    
                    // Refresh to show the changes
                    Refresh(null);
                }
                else
                {
                    _notificationService.ShowError($"Failed to set process '{SelectedProcess.Name}' affinity");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting process affinity: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set priority
        /// </summary>
        private void SetPriority(ProcessPriority? priority)
        {
            if (SelectedProcess == null || priority == null)
            {
                return;
            }
            
            try
            {
                if (_processService.SetProcessPriority(SelectedProcess.Id, priority.Value))
                {
                    _notificationService.ShowSuccess($"Process '{SelectedProcess.Name}' priority set to {priority}");
                    
                    // Refresh to show the changes
                    Refresh(null);
                }
                else
                {
                    _notificationService.ShowError($"Failed to set process '{SelectedProcess.Name}' priority");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error setting process priority: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Suspend process
        /// </summary>
        private void SuspendProcess(object? parameter)
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                if (_processService.SuspendProcess(SelectedProcess.Id))
                {
                    _notificationService.ShowSuccess($"Process '{SelectedProcess.Name}' suspended successfully");
                    
                    // Refresh to show the changes
                    Refresh(null);
                }
                else
                {
                    _notificationService.ShowError($"Failed to suspend process '{SelectedProcess.Name}'");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error suspending process: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resume process
        /// </summary>
        private void ResumeProcess(object? parameter)
        {
            if (SelectedProcess == null)
            {
                return;
            }
            
            try
            {
                if (_processService.ResumeProcess(SelectedProcess.Id))
                {
                    _notificationService.ShowSuccess($"Process '{SelectedProcess.Name}' resumed successfully");
                    
                    // Refresh to show the changes
                    Refresh(null);
                }
                else
                {
                    _notificationService.ShowError($"Failed to resume process '{SelectedProcess.Name}'");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Error resuming process: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Can modify process
        /// </summary>
        private bool CanModifyProcess(object? parameter)
        {
            return SelectedProcess != null && !SelectedProcess.IsCritical;
        }
        
        /// <summary>
        /// Can suspend process
        /// </summary>
        private bool CanSuspendProcess(object? parameter)
        {
            return SelectedProcess != null && !SelectedProcess.IsSuspended && !SelectedProcess.IsCritical;
        }
        
        /// <summary>
        /// Can resume process
        /// </summary>
        private bool CanResumeProcess(object? parameter)
        {
            return SelectedProcess != null && SelectedProcess.IsSuspended && !SelectedProcess.IsCritical;
        }
    }
}