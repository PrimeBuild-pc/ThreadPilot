namespace ThreadPilot.Views
{
    using ThreadPilot.Helpers;
    using ThreadPilot.ViewModels;

    public partial class MasksView : System.Windows.Controls.UserControl
    {
        public MasksView()
        {
            this.InitializeComponent();
            this.DataContext = ServiceProviderExtensions.GetService<MasksViewModel>();
        }
    }
}

