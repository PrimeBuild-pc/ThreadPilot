namespace ThreadPilot.Services
{
    using System;
    using System.Threading.Tasks;

    internal static class TaskSafety
    {
        public static void FireAndForget(Task task, Action<Exception> onError)
        {
            _ = ObserveAsync(task, onError);
        }

        private static async Task ObserveAsync(Task task, Action<Exception> onError)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected in shutdown paths.
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }
    }
}
