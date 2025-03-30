using System;
using System.Windows;
using ThreadPilot.ViewModels;

namespace ThreadPilot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            _viewModel = DataContext as MainViewModel;
            
            // Refresh data initially
            if (_viewModel != null)
            {
                _viewModel.RefreshDataCommand.Execute(null);
                
                // Set up timer to refresh data periodically
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                
                timer.Tick += (s, e) => _viewModel.RefreshDataCommand.Execute(null);
                timer.Start();
            }
        }
    }
}