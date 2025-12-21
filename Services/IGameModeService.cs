using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    /// <summary>
    /// Service for managing Windows Game Mode settings
    /// Game Mode can interfere with CPU affinity settings, particularly on AMD systems
    /// </summary>
    public interface IGameModeService
    {
        /// <summary>
        /// Checks if Windows Game Mode is currently enabled
        /// </summary>
        /// <returns>True if Game Mode is enabled, false otherwise</returns>
        Task<bool> IsGameModeEnabledAsync();

        /// <summary>
        /// Sets Windows Game Mode to enabled or disabled
        /// </summary>
        /// <param name="enabled">True to enable Game Mode, false to disable</param>
        /// <returns>True if the operation succeeded, false otherwise</returns>
        Task<bool> SetGameModeAsync(bool enabled);

        /// <summary>
        /// Disables Game Mode for better CPU affinity control
        /// This is a non-intrusive operation that only disables if currently enabled
        /// </summary>
        /// <returns>True if Game Mode was disabled, false if it was already disabled or operation failed</returns>
        Task<bool> DisableGameModeForAffinityAsync();
    }
}
