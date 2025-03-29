using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using ThreadPilot.Helpers;
using ThreadPilot.Models;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ProcessService _processService;
        private readonly AffinityService _affinityService;
        private readonly SystemOptimizationService _systemOptimizationService;
        private readonly PowerProfileService _powerProfileService;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private readonly DispatcherTimer _performanceTimer;

        private string _statusMessage = "Ready";
        private double _cpuUsage = 0;
        private double _memoryUsage = 0;
        private int _selectedTabIndex = 0;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set => SetProperty(ref _cpuUsage, value);
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set => SetProperty(ref _memoryUsage, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public ObservableCollection<Profile> SavedProfiles { get; } = new ObservableCollection<Profile>();

        public MainViewModel(
            ProcessService processService,
            AffinityService affinityService,
            SystemOptimizationService systemOptimizationService,
            PowerProfileService powerProfileService,
            SettingsService settingsService,
            NotificationService notificationService)
        {
            _processService = processService;
            _affinityService = affinityService;
            _systemOptimizationService = systemOptimizationService;
            _powerProfileService = powerProfileService;
            _settingsService = settingsService;
            _notificationService = notificationService;

            // Initialize performance counters
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to initialize performance counters: {ex.Message}";
            }

            // Load profiles
            LoadProfiles();

            // Set up timer for updating performance metrics
            _performanceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _performanceTimer.Tick += PerformanceTimer_Tick;
            _performanceTimer.Start();
        }

        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_cpuCounter != null)
                {
                    CpuUsage = Math.Round(_cpuCounter.NextValue(), 1);
                }

                if (_ramCounter != null)
                {
                    MemoryUsage = Math.Round(_ramCounter.NextValue(), 1);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error updating performance metrics: {ex.Message}";
                _performanceTimer.Stop();
            }
        }

        private void LoadProfiles()
        {
            try
            {
                var profiles = _settingsService.LoadProfiles();
                SavedProfiles.Clear();
                
                foreach (var profile in profiles)
                {
                    SavedProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load profiles: {ex.Message}";
            }
        }

        public void ApplyProfile(Profile profile)
        {
            StatusMessage = $"Applying profile: {profile.Name}...";
            
            Task.Run(() =>
            {
                try
                {
                    // Apply process settings
                    foreach (var processSettings in profile.ProcessSettings)
                    {
                        var process = _processService.GetProcessByName(processSettings.ProcessName);
                        if (process != null)
                        {
                            if (processSettings.AffinityMask > 0)
                            {
                                _affinityService.SetAffinity(process, processSettings.AffinityMask);
                            }

                            if (processSettings.Priority != ProcessPriorityClass.Normal)
                            {
                                process.PriorityClass = processSettings.Priority;
                            }
                        }
                    }

                    // Apply system optimizations if specified
                    if (profile.SystemOptimizations != null)
                    {
                        _systemOptimizationService.ApplySystemOptimizations(profile.SystemOptimizations);
                    }

                    // Apply power profile if specified
                    if (!string.IsNullOrEmpty(profile.PowerProfileName))
                    {
                        _powerProfileService.ApplyPowerProfile(profile.PowerProfileName);
                    }

                    // Update status and show notification
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Profile '{profile.Name}' applied successfully";
                        _notificationService.ShowNotification("ThreadPilot", $"Profile '{profile.Name}' applied successfully");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"Error applying profile: {ex.Message}";
                        _notificationService.ShowNotification("ThreadPilot", $"Error applying profile: {ex.Message}", NotificationType.Error);
                    });
                }
            });
        }

        public void SaveProfile(Profile profile)
        {
            // Add to collection if it doesn't exist
            if (!SavedProfiles.Contains(profile))
            {
                SavedProfiles.Add(profile);
            }

            // Save to settings
            _settingsService.SaveProfiles(new System.Collections.Generic.List<Profile>(SavedProfiles));
            StatusMessage = $"Profile '{profile.Name}' saved";
        }

        public void DeleteProfile(Profile profile)
        {
            SavedProfiles.Remove(profile);
            _settingsService.SaveProfiles(new System.Collections.Generic.List<Profile>(SavedProfiles));
            StatusMessage = $"Profile '{profile.Name}' deleted";
        }
    }
}
