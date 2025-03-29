using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class ProcessListViewModel : ViewModelBase
    {
        private readonly ProcessService _processService;
        private readonly AffinityService _affinityService;
        private readonly NotificationService _notificationService;
        private readonly DispatcherTimer _refreshTimer;
        private string _searchText = string.Empty;
        private ProcessInfo _selectedProcess;
        private bool _isBusy;
        private string _statusMessage = "Ready";

        public ObservableCollection<ProcessInfo> Processes { get; } = new ObservableCollection<ProcessInfo>();
        public ICollectionView ProcessesView { get; private set; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ProcessesView.Refresh();
                }
            }
        }

        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    // When a process is selected, update its real-time info
                    if (_selectedProcess != null)
                    {
                        UpdateSelectedProcessInfo();
                    }
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand ViewAffinityCommand { get; }

        public ProcessListViewModel(
            ProcessService processService,
            AffinityService affinityService,
            NotificationService notificationService)
        {
            _processService = processService;
            _affinityService = affinityService;
            _notificationService = notificationService;

            // Set up commands
            RefreshCommand = new RelayCommand(_ => RefreshProcessList());
            SetPriorityCommand = new RelayCommand<string>(priority => SetProcessPriority(priority));
            ViewAffinityCommand = new RelayCommand(_ => ViewProcessAffinity(), _ => SelectedProcess != null);

            // Initialize the collection view with filter
            ProcessesView = CollectionViewSource.GetDefaultView(Processes);
            ProcessesView.Filter = ProcessFilter;

            // Load processes initially
            RefreshProcessList();

            // Set up timer for auto-refresh
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _refreshTimer.Tick += (s, e) => RefreshProcessList();
            _refreshTimer.Start();
        }

        private bool ProcessFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            if (obj is ProcessInfo process)
            {
                return process.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || process.Pid.ToString().Contains(SearchText);
            }

            return false;
        }

        public void RefreshProcessList()
        {
            Task.Run(() =>
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Refreshing process list...";

                    var processes = _processService.GetAllProcesses();
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Save the currently selected process ID
                        int? selectedPid = SelectedProcess?.Pid;

                        // Update the collection
                        Processes.Clear();
                        foreach (var process in processes)
                        {
                            Processes.Add(process);
                        }

                        // Try to reselect the previously selected process
                        if (selectedPid.HasValue)
                        {
                            SelectedProcess = Processes.FirstOrDefault(p => p.Pid == selectedPid.Value);
                        }

                        IsBusy = false;
                        StatusMessage = $"Loaded {processes.Count} processes";
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error refreshing processes: {ex.Message}";
                    });
                }
            });
        }

        private void UpdateSelectedProcessInfo()
        {
            if (SelectedProcess == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    var updatedProcess = _processService.GetProcessById(SelectedProcess.Pid);
                    if (updatedProcess != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Update the properties of the selected process
                            SelectedProcess.Priority = updatedProcess.Priority;
                            SelectedProcess.AffinityMask = updatedProcess.AffinityMask;
                            SelectedProcess.CpuUsage = updatedProcess.CpuUsage;
                            SelectedProcess.MemoryUsage = updatedProcess.MemoryUsage;
                        });
                    }
                }
                catch (Exception)
                {
                    // Process might have terminated, we'll handle it in the next refresh
                }
            });
        }

        private void SetProcessPriority(string priorityStr)
        {
            if (SelectedProcess == null)
                return;

            try
            {
                ProcessPriorityClass priority = ProcessPriorityClass.Normal;
                
                switch (priorityStr)
                {
                    case "Idle":
                        priority = ProcessPriorityClass.Idle;
                        break;
                    case "Below Normal":
                        priority = ProcessPriorityClass.BelowNormal;
                        break;
                    case "Normal":
                        priority = ProcessPriorityClass.Normal;
                        break;
                    case "Above Normal":
                        priority = ProcessPriorityClass.AboveNormal;
                        break;
                    case "High":
                        priority = ProcessPriorityClass.High;
                        break;
                    case "Realtime":
                        priority = ProcessPriorityClass.RealTime;
                        break;
                }

                Task.Run(() =>
                {
                    try
                    {
                        _processService.SetProcessPriority(SelectedProcess.Pid, priority);
                        
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SelectedProcess.Priority = priority;
                            StatusMessage = $"Set {SelectedProcess.Name} priority to {priorityStr}";
                            _notificationService.ShowNotification("ThreadPilot", $"Set {SelectedProcess.Name} priority to {priorityStr}");
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"Error setting priority: {ex.Message}";
                            _notificationService.ShowNotification("ThreadPilot", $"Error setting priority: {ex.Message}", NotificationType.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error setting priority: {ex.Message}";
            }
        }

        private void ViewProcessAffinity()
        {
            if (SelectedProcess == null)
                return;

            // The main application will handle switching to the Affinity tab
            // and setting the selected process
            var mainViewModel = App.GetService<MainViewModel>();
            mainViewModel.SelectedTabIndex = 1; // Index of the Affinity tab

            // Get the affinity view model and set the selected process
            var affinityViewModel = App.GetService<AffinityViewModel>();
            affinityViewModel.LoadProcessAffinity(SelectedProcess);
        }
    }
}
