using ThreadPilot.Helpers;
using ThreadPilot.ViewModels;

namespace ThreadPilot.Views
{
    /// <summary>
    /// Interaction logic for MasksView.xaml
    /// Based on CPUSetSetter's data-binding pattern
    /// </summary>
    public partial class MasksView : System.Windows.Controls.UserControl
    {
        public MasksView()
        {
            InitializeComponent();
            DataContext = ServiceProviderExtensions.GetService<MasksViewModel>();
        }
    }
}
