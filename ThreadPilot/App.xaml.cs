using System;
using System.Threading;
using System.Windows;
using ThreadPilot.Services;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly Mutex SingleInstanceMutex = new Mutex(true, "ThreadPilot_SingleInstance");
        
        protected override void OnStartup(StartupEventArgs e)
        {
            // Ensure only one instance of the application is running
            if (!SingleInstanceMutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("ThreadPilot is already running.", "ThreadPilot", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            
            // Initialize the service locator
            ServiceLocator.Initialize();
            
            base.OnStartup(e);
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            // Release the mutex
            SingleInstanceMutex.ReleaseMutex();
            
            base.OnExit(e);
        }
    }
}