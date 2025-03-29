using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class SettingsView : UserControl
    {
        private SettingsViewModel _viewModel;

        public SettingsView()
        {
            InitializeComponent();
            
            // Get the view model from DI
            _viewModel = App.GetService<SettingsViewModel>();
            
            // Set the data context
            DataContext = _viewModel;
        }
    }
}
