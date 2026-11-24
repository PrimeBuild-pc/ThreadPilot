namespace ThreadPilot.Services
{
    using System.Threading.Tasks;

    public interface IElevatedTaskService
    {
        string LaunchTaskName { get; }

        string AutostartTaskName { get; }

        Task<bool> EnsureLaunchTaskAsync();

        Task<bool> TryRunLaunchTaskAsync();

        /// <summary>Deletes the launch task (and any ThreadPilot task outside Program Files); true when it is gone.</summary>
        Task<bool> RemoveLaunchTaskAsync();

        /// <summary>Executable path the registered autostart task runs, or null when the task does not exist.</summary>
        Task<string?> GetAutostartTaskExecutablePathAsync();

        Task<bool> EnsureAutostartTaskAsync(string executablePath, string arguments);

        Task<bool> RemoveAutostartTaskAsync();

        Task<bool> IsAutostartTaskRegisteredAsync();
    }
}
