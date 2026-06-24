namespace ThreadPilot.Views
{
    using System.Windows.Controls;
    using System.Windows.Input;
    using ThreadPilot.Helpers;
    using ThreadPilot.ViewModels;

    public partial class ProcessView : System.Windows.Controls.UserControl
    {
        public ProcessView()
        {
            this.InitializeComponent();
            this.DataContext = ServiceProviderExtensions.GetService<ProcessViewModel>();
        }

        private void ProcessRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            row.IsSelected = true;
            row.Focus();
        }
    }
}
