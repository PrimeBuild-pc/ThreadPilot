namespace ThreadPilot.Services
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Services.Abstractions;

    /// <summary>
    /// HTTP client wrapper for GitHub release metadata.
    /// </summary>
    public sealed class GitHubReleaseClient : IGitHubReleaseClient
    {
        private readonly HttpClient httpClient;

        public GitHubReleaseClient(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<string> GetLatestReleaseJsonAsync(string owner, string repo, CancellationToken cancellationToken = default)
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            return this.httpClient.GetStringAsync(url, cancellationToken);
        }
    }
}
