namespace ThreadPilot.Views
{
    using System.Windows;

    public partial class MonitoringDisableDialog : Window
    {
        public MonitoringDisableDialog()
        {
            this.InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
