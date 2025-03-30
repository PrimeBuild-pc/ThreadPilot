using System;
using System.Windows;
using ThreadPilot.Services;

namespace ThreadPilot.ViewModels
{
    /// <summary>
    /// Main view model for the application
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        // Backing fields
        private object? _currentPageViewModel;
        private int _selectedPageIndex;
        private string _statusMessage = "Ready";
        
        // Page view models
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly ProfileEditorViewModel _profileEditorViewModel;
        
        /// <summary>
        /// Current page view model
        /// </summary>
        public object? CurrentPageViewModel
        {
            get => _currentPageViewModel;
            set => SetProperty(ref _currentPageViewModel, value);
        }
        
        /// <summary>
        /// Selected page index
        /// </summary>
        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set
            {
                if (SetProperty(ref _selectedPageIndex, value))
                {
                    // Update the current page view model
                    UpdateCurrentPageViewModel();
                }
            }
        }
        
        /// <summary>
        /// Status message
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            try
            {
                // Initialize page view models
                _dashboardViewModel = new DashboardViewModel();
                _profileEditorViewModel = new ProfileEditorViewModel();
                
                // Set the initial page
                SelectedPageIndex = 0;
                UpdateCurrentPageViewModel();
                
                // Setup handler for notification service
                var notificationService = ServiceLocator.Get<INotificationService>();
                notificationService.StatusMessageUpdated += OnStatusMessageUpdated;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing application: {ex.Message}", "ThreadPilot Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Update the current page view model based on the selected index
        /// </summary>
        private void UpdateCurrentPageViewModel()
        {
            switch (SelectedPageIndex)
            {
                case 0:
                    CurrentPageViewModel = _dashboardViewModel;
                    break;
                    
                case 3:
                    CurrentPageViewModel = _profileEditorViewModel;
                    break;
                    
                default:
                    // Default to dashboard for now
                    CurrentPageViewModel = _dashboardViewModel;
                    break;
            }
        }
        
        /// <summary>
        /// Event handler for status message updates
        /// </summary>
        private void OnStatusMessageUpdated(object? sender, string message)
        {
            StatusMessage = message;
        }
    }
}