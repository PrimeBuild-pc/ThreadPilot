using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class AffinityViewModel : ViewModelBase
    {
        private readonly AffinityService _affinityService;
        private readonly NotificationService _notificationService;

        private ProcessInfo _selectedProcess;
        private bool _isBusy;
        private string _statusMessage = "Select a process from the Processes tab to manage its affinity";
        private long _newAffinityMask;
        private int _processorCount;

        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
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

        public long NewAffinityMask
        {
            get => _newAffinityMask;
            set => SetProperty(ref _newAffinityMask, value);
        }

        public int ProcessorCount
        {
            get => _processorCount;
            set => SetProperty(ref _processorCount, value);
        }

        public ObservableCollection<CoreInfo> Cores { get; } = new ObservableCollection<CoreInfo>();

        public ICommand ApplyAffinityCommand { get; }
        public ICommand SelectAllCoresCommand { get; }
        public ICommand DeselectAllCoresCommand { get; }
        public ICommand SelectEvenCoresCommand { get; }
        public ICommand SelectOddCoresCommand { get; }
        public ICommand SelectFirstHalfCommand { get; }
        public ICommand SelectSecondHalfCommand { get; }

        public AffinityViewModel(
            AffinityService affinityService,
            NotificationService notificationService)
        {
            _affinityService = affinityService;
            _notificationService = notificationService;

            // Set up commands
            ApplyAffinityCommand = new RelayCommand(_ => ApplyAffinity(), _ => SelectedProcess != null);
            SelectAllCoresCommand = new RelayCommand(_ => SelectAllCores());
            DeselectAllCoresCommand = new RelayCommand(_ => DeselectAllCores());
            SelectEvenCoresCommand = new RelayCommand(_ => SelectEvenCores());
            SelectOddCoresCommand = new RelayCommand(_ => SelectOddCores());
            SelectFirstHalfCommand = new RelayCommand(_ => SelectFirstHalf());
            SelectSecondHalfCommand = new RelayCommand(_ => SelectSecondHalf());

            // Get processor count
            ProcessorCount = Environment.ProcessorCount;
            InitializeCores();
        }

        private void InitializeCores()
        {
            Cores.Clear();
            for (int i = 0; i < ProcessorCount; i++)
            {
                Cores.Add(new CoreInfo
                {
                    CoreNumber = i,
                    IsSelected = false
                });
            }
        }

        public void LoadProcessAffinity(ProcessInfo process)
        {
            if (process == null)
                return;

            SelectedProcess = process;
            NewAffinityMask = process.AffinityMask;
            StatusMessage = $"Managing affinity for {process.Name} (PID: {process.Pid})";

            // Update core selection based on current affinity mask
            UpdateCoreSelection();
        }

        private void UpdateCoreSelection()
        {
            for (int i = 0; i < Cores.Count; i++)
            {
                // Check if the bit at position i is set in the affinity mask
                Cores[i].IsSelected = ((NewAffinityMask >> i) & 1) == 1;
            }
        }

        private void UpdateAffinityMaskFromCores()
        {
            long mask = 0;
            for (int i = 0; i < Cores.Count; i++)
            {
                if (Cores[i].IsSelected)
                {
                    // Set the bit at position i
                    mask |= (1L << i);
                }
            }
            NewAffinityMask = mask;
        }

        private void ApplyAffinity()
        {
            if (SelectedProcess == null)
                return;

            IsBusy = true;
            StatusMessage = "Applying CPU affinity...";

            Task.Run(() =>
            {
                try
                {
                    // Update the affinity mask based on current core selection
                    UpdateAffinityMaskFromCores();

                    // Ensure at least one core is selected
                    if (NewAffinityMask == 0)
                    {
                        throw new InvalidOperationException("At least one CPU core must be selected.");
                    }

                    // Apply the new affinity mask
                    _affinityService.SetAffinity(SelectedProcess.Pid, NewAffinityMask);

                    // Update the selected process with the new affinity mask
                    SelectedProcess.AffinityMask = NewAffinityMask;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Affinity applied for {SelectedProcess.Name}";
                        _notificationService.ShowNotification("ThreadPilot", $"CPU affinity updated for {SelectedProcess.Name}");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsBusy = false;
                        StatusMessage = $"Error setting affinity: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        private void SelectAllCores()
        {
            foreach (var core in Cores)
            {
                core.IsSelected = true;
            }
            UpdateAffinityMaskFromCores();
        }

        private void DeselectAllCores()
        {
            foreach (var core in Cores)
            {
                core.IsSelected = false;
            }
            UpdateAffinityMaskFromCores();
        }

        private void SelectEvenCores()
        {
            for (int i = 0; i < Cores.Count; i++)
            {
                Cores[i].IsSelected = i % 2 == 0;
            }
            UpdateAffinityMaskFromCores();
        }

        private void SelectOddCores()
        {
            for (int i = 0; i < Cores.Count; i++)
            {
                Cores[i].IsSelected = i % 2 != 0;
            }
            UpdateAffinityMaskFromCores();
        }

        private void SelectFirstHalf()
        {
            int halfCount = Cores.Count / 2;
            for (int i = 0; i < Cores.Count; i++)
            {
                Cores[i].IsSelected = i < halfCount;
            }
            UpdateAffinityMaskFromCores();
        }

        private void SelectSecondHalf()
        {
            int halfCount = Cores.Count / 2;
            for (int i = 0; i < Cores.Count; i++)
            {
                Cores[i].IsSelected = i >= halfCount;
            }
            UpdateAffinityMaskFromCores();
        }
    }
}
