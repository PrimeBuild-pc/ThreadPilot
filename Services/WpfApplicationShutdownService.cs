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

            // The graceful path restores Game Boost, process changes and the power plan, then always exits.
            application.Dispatcher.InvokeAsync(async () =>
            {
                if (application.MainWindow is MainWindow window)
                {
                    await window.ShutdownForUpdateAsync();
                    return;
                }

                application.Shutdown();
            });
        }
    }
}
