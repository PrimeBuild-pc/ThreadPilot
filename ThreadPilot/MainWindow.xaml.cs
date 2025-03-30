using System;
using System.Windows;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // Register closing event
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // You can add confirmation dialog here or cleanup code
            // If minimizing to tray is enabled, can cancel closing and hide to tray instead
            
            // For now, just cleanup resources as needed
            try
            {
                // Cleanup any resources, notify services, etc.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during application shutdown: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}