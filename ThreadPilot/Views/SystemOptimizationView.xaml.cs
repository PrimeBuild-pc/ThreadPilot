using System.Windows.Controls;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class SystemOptimizationView : UserControl
    {
        private SystemOptimizationViewModel _viewModel;

        public SystemOptimizationView()
        {
            InitializeComponent();
            
            // Get the view model from DI
            _viewModel = App.GetService<SystemOptimizationViewModel>();
            
            // Set the data context
            DataContext = _viewModel;
        }
    }
}
