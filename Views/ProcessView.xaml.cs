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

        private void ProcessWorkspace_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            var wide = e.NewSize.Width >= 960;
            this.DetailColumn.Width = new System.Windows.GridLength(wide ? 400 : 0);
            Grid.SetRow(this.ProcessDetails, wide ? 1 : 2);
            Grid.SetColumn(this.ProcessDetails, wide ? 1 : 0);
            this.ProcessDetails.MaxHeight = wide ? double.PositiveInfinity : Math.Max(160, e.NewSize.Height * 0.48);
            this.ProcessDetails.Padding = wide ? new System.Windows.Thickness(12, 0, 12, 0) : new System.Windows.Thickness(0, 12, 12, 0);
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
