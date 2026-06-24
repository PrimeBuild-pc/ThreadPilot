namespace ThreadPilot.Services
{
    using System.Threading.Tasks;

    public interface IElevationService
    {
        bool IsRunningAsAdministrator();

        Task<bool> RequestElevationIfNeeded();

        Task<bool> RestartWithElevation(string[]? arguments = null);

        bool ValidateElevationForOperation(string operation);

        string GetElevationStatus();
    }
}

