namespace ThreadPilot.Services
{
    using System.Threading.Tasks;

    public interface IElevatedTaskService
    {
        string LaunchTaskName { get; }

        string AutostartTaskName { get; }

        Task<bool> EnsureLaunchTaskAsync();

        Task<bool> TryRunLaunchTaskAsync();

        Task<bool> EnsureAutostartTaskAsync(string executablePath, string arguments);

        Task<bool> RemoveAutostartTaskAsync();

        Task<bool> IsAutostartTaskRegisteredAsync();
    }
}
