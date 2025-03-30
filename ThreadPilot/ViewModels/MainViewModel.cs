using System;
using System.Windows.Input;
using ThreadPilot.Commands;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Main view model
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly ProcessesViewModel _processesViewModel;
        private readonly CpuCoresViewModel _cpuCoresViewModel;
        private readonly ProfileEditorViewModel _profileEditorViewModel;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            // Initialize view models
            _dashboardViewModel = new DashboardViewModel();
            _processesViewModel = new ProcessesViewModel();
            _cpuCoresViewModel = new CpuCoresViewModel();
            _profileEditorViewModel = new ProfileEditorViewModel();
            
            // Set default view model
            CurrentViewModel = _dashboardViewModel;
            
            // Initialize commands
            NavigateDashboardCommand = new RelayCommand(_ => NavigateTo(_dashboardViewModel));
            NavigateProcessesCommand = new RelayCommand(_ => NavigateTo(_processesViewModel));
            NavigateCpuCoresCommand = new RelayCommand(_ => NavigateTo(_cpuCoresViewModel));
            NavigateProfileEditorCommand = new RelayCommand(_ => NavigateTo(_profileEditorViewModel));
            RefreshCommand = new RelayCommand(_ => Refresh());
            ExitCommand = new RelayCommand(_ => Exit());
            AboutCommand = new RelayCommand(_ => ShowAbout());
        }
        
        /// <summary>
        /// Current view model
        /// </summary>
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
        
        /// <summary>
        /// Navigate to Dashboard command
        /// </summary>
        public ICommand NavigateDashboardCommand { get; }
        
        /// <summary>
        /// Navigate to Processes command
        /// </summary>
        public ICommand NavigateProcessesCommand { get; }
        
        /// <summary>
        /// Navigate to CPU Cores command
        /// </summary>
        public ICommand NavigateCpuCoresCommand { get; }
        
        /// <summary>
        /// Navigate to Profile Editor command
        /// </summary>
        public ICommand NavigateProfileEditorCommand { get; }
        
        /// <summary>
        /// Refresh command
        /// </summary>
        public ICommand RefreshCommand { get; }
        
        /// <summary>
        /// Exit command
        /// </summary>
        public ICommand ExitCommand { get; }
        
        /// <summary>
        /// About command
        /// </summary>
        public ICommand AboutCommand { get; }
        
        /// <summary>
        /// Navigate to the specified view model
        /// </summary>
        /// <param name="viewModel">View model to navigate to</param>
        private void NavigateTo(ViewModelBase viewModel)
        {
            CurrentViewModel = viewModel;
        }
        
        /// <summary>
        /// Refresh current view model
        /// </summary>
        private void Refresh()
        {
            // Each view model has its own refresh mechanism
            // We might want to implement a common interface for refreshable view models
            
            if (CurrentViewModel == _dashboardViewModel)
            {
                // Refresh dashboard
            }
            else if (CurrentViewModel == _processesViewModel)
            {
                // Refresh processes
            }
            else if (CurrentViewModel == _cpuCoresViewModel)
            {
                // Refresh CPU cores
            }
            else if (CurrentViewModel == _profileEditorViewModel)
            {
                // Refresh profile editor
            }
        }
        
        /// <summary>
        /// Exit application
        /// </summary>
        private void Exit()
        {
            var notification = ServiceLocator.Resolve<INotificationService>();
            var confirmed = notification?.ShowConfirmation("Are you sure you want to exit?", "Exit") ?? false;
            
            if (confirmed)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
        
        /// <summary>
        /// Show about dialog
        /// </summary>
        private void ShowAbout()
        {
            var notification = ServiceLocator.Resolve<INotificationService>();
            notification?.ShowInformation($"ThreadPilot\nVersion {App.Version}\n\nA Windows desktop application for advanced system performance optimization.", "About ThreadPilot");
        }
    }
}