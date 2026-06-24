/*
 * ThreadPilot - graceful shutdown hook after updater launch.
 */
namespace ThreadPilot.Services
{
    using System.Windows;

    public sealed class WpfApplicationShutdownService : IApplicationShutdownService
    {
        public void RequestShutdownForUpdate()
        {
            var application = Application.Current;
            if (application == null)
            {
                return;
            }

            application.Dispatcher.InvokeAsync(application.Shutdown);
        }
    }
}
