using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    public partial class ProcessListView : UserControl
    {
        private ProcessListViewModel _viewModel;

        public ProcessListView()
        {
            InitializeComponent();
            
            // Get the view model from DI
            _viewModel = App.GetService<ProcessListViewModel>();
            
            // Set the data context
            DataContext = _viewModel;
        }

        private void PriorityComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBoxItem item && _viewModel.SelectedProcess != null)
            {
                // Execute the command to set the priority
                string priority = item.Content.ToString();
                _viewModel.SetPriorityCommand.Execute(priority);
                
                // Close the ComboBox dropdown
                if (item.Parent is ComboBox comboBox)
                {
                    comboBox.IsDropDownOpen = false;
                }
                
                // Mark the event as handled to prevent the normal selection behavior
                e.Handled = true;
            }
        }
    }
}
