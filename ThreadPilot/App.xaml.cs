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
        /// Constructor
        /// </summary>
        public App()
        {
            // Register process exit event
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
        
        /// <summary>
        /// On process exit
        /// </summary>
        private void OnProcessExit(object? sender, EventArgs e)
        {
            try
            {
                // Cleanup services
                ServiceLocator.Cleanup();
            }
            catch
            {
                // Ignore
            }
        }
    }
}