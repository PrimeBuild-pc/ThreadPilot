namespace ThreadPilot.Services
{
    using System.Threading.Tasks;

    public interface IGameModeService
    {
        Task<bool> IsGameModeEnabledAsync();

        Task<bool> SetGameModeAsync(bool enabled);

        Task<bool> DisableGameModeForAffinityAsync();
    }
}

