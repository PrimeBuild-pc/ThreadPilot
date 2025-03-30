using System;
using System.Windows;
using ThreadPilot.Services;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Application startup event handler
        /// </summary>
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Register services
            RegisterServices();
            
            // Create and initialize the main view model
            var mainViewModel = new MainViewModel();
            mainViewModel.Initialize();
            
            // Create and show the main window
            var mainWindow = new MainWindow()
            {
                DataContext = mainViewModel
            };
            
            mainWindow.Show();
        }
        
        /// <summary>
        /// Application exit event handler
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Clean up resources
            if (Current.MainWindow?.DataContext is MainViewModel viewModel)
            {
                viewModel.Cleanup();
            }
        }
        
        /// <summary>
        /// Register all application services
        /// </summary>
        private void RegisterServices()
        {
            // Register system services
            ServiceLocator.Register<ISystemInfoService, SystemInfoService>();
            ServiceLocator.Register<IProcessService, ProcessService>();
            ServiceLocator.Register<IPowerProfileService, PowerProfileService>();
            
            // Register UI services
            ServiceLocator.Register<INotificationService, NotificationService>();
            ServiceLocator.Register<IFileDialogService, FileDialogService>();
        }
    }
}