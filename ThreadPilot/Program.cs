
using System;

Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

using System;
using System.Windows;
using ThreadPilot.Services;

namespace ThreadPilot
{
    /// <summary>
    /// Program entry point
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Application entry point
        /// </summary>
        [STAThread]
        public static void Main()
        {
            try
            {
                // Initialize services
                ServiceLocator.Initialize();
                
                // Create and run application
                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unhandled exception occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}