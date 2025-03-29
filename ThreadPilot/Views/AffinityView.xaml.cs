using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThreadPilot.Models;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class AffinityView : UserControl
    {
        private AffinityViewModel _viewModel;

        public AffinityView()
        {
            InitializeComponent();
            
            // Get the view model from DI
            _viewModel = App.GetService<AffinityViewModel>();
            
            // Set the data context
            DataContext = _viewModel;
            
            // Add command for toggling core selection
            _viewModel.ToggleCoreCommand = new Helpers.RelayCommand<CoreInfo>(ToggleCore);
        }

        private void ToggleCore(CoreInfo core)
        {
            if (core != null)
            {
                // Toggle the core selection
                core.IsSelected = !core.IsSelected;
                
                // Update the affinity mask
                _viewModel.UpdateAffinityMaskFromCores();
            }
        }
    }
}
