namespace ThreadPilot.Views
{
    using System.Windows.Controls;
    using ThreadPilot.ViewModels;

    public partial class PerformanceView : System.Windows.Controls.UserControl
    {
        public PerformanceView()
        {
            this.InitializeComponent();
        }

        public PerformanceView(PerformanceViewModel viewModel)
            : this()
        {
            this.DataContext = viewModel;
        }
    }
}

