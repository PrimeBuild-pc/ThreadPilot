namespace ThreadPilot.Services.Abstractions
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IGitHubReleaseClient
    {
        Task<string> GetReleasesJsonAsync(string owner, string repo, CancellationToken cancellationToken = default);
    }
}
