using System;
using System.Windows;
using ThreadPilot.Services;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Application startup
        /// </summary>
        /// <param name="e">Startup event arguments</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Register services
            RegisterServices();
            
            // Register unhandled exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }
        
        /// <summary>
        /// Register application services
        /// </summary>
        private void RegisterServices()
        {
            // Create and register service implementations
            ServiceLocator.Register<INotificationService>(new NotificationService());
            ServiceLocator.Register<IFileDialogService>(new FileDialogService());
            ServiceLocator.Register<ISystemInfoService>(new SystemInfoService());
            ServiceLocator.Register<IProcessService>(new ProcessService());
            ServiceLocator.Register<IPowerProfileService>(new PowerProfileService());
        }
        
        /// <summary>
        /// Handle unhandled exceptions in the application domain
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            
            MessageBox.Show(
                $"An unhandled exception occurred: {exception?.Message}\n\n" +
                $"Details: {exception?.ToString()}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            if (e.IsTerminating)
            {
                // The application is terminating, perform any necessary cleanup
                Shutdown(-1);
            }
        }
        
        /// <summary>
        /// Handle unhandled exceptions in the dispatcher
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unhandled exception occurred: {e.Exception.Message}\n\n" +
                $"Details: {e.Exception}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            // Mark the exception as handled to prevent application crash
            e.Handled = true;
        }
    }
}