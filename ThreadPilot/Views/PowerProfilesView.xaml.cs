using System.Windows;
using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for PowerProfilesView.xaml
    /// </summary>
    public partial class PowerProfilesView : UserControl
    {
        private PowerProfilesViewModel _viewModel;
        
        /// <summary>
        /// Constructor for PowerProfilesView
        /// </summary>
        public PowerProfilesView()
        {
            InitializeComponent();
            
            // Get the view model from the application service provider
            _viewModel = App.GetService<PowerProfilesViewModel>();
            
            // Set the DataContext
            DataContext = _viewModel;
        }
        
        /// <summary>
        /// Called when the view is loaded
        /// </summary>
        private async void PowerProfilesView_Loaded(object sender, RoutedEventArgs e)
        {
            // Load the power profiles
            await _viewModel.LoadProfilesAsync();
        }
    }
}