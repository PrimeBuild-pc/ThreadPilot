namespace ThreadPilot.Services.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Retrieves GitHub release payloads for update checks.
    /// </summary>
    public interface IGitHubReleaseClient
    {
        Task<string> GetLatestReleaseJsonAsync(string owner, string repo, CancellationToken cancellationToken = default);
    }
}
