namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Services.Abstractions;

    public sealed class GitHubUpdateChecker
    {
        private readonly IGitHubReleaseClient gitHubReleaseClient;

        private record LatestRelease(
            string Tag_name,
            bool Prerelease,
            bool Draft,
            string Html_url,
            IReadOnlyList<LatestReleaseAsset>? Assets);

        private record LatestReleaseAsset(string Name, string Browser_download_url, long Size);

        public GitHubUpdateChecker(IGitHubReleaseClient gitHubReleaseClient)
        {
            this.gitHubReleaseClient = gitHubReleaseClient ?? throw new ArgumentNullException(nameof(gitHubReleaseClient));
        }

        public async Task<UpdateReleaseInfo?> GetLatestReleaseInfoAsync(
            string owner,
            string repo,
            bool includePrereleases = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(owner))
            {
                throw new ArgumentException("Owner is required", nameof(owner));
            }

            if (string.IsNullOrWhiteSpace(repo))
            {
                throw new ArgumentException("Repository is required", nameof(repo));
            }

            var json = await this.gitHubReleaseClient.GetReleasesJsonAsync(owner, repo, cancellationToken).ConfigureAwait(false);
            var releases = JsonSerializer.Deserialize<List<LatestRelease>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (releases == null || releases.Count == 0)
            {
                return null;
            }

            return releases
                .Where(release => !release.Draft)
                .Where(release => includePrereleases || !release.Prerelease)
                .Select(TryMapRelease)
                .Where(release => release != null)
                .Cast<UpdateReleaseInfo>()
                .OrderByDescending(release => release.Version)
                .FirstOrDefault();
        }

        private static UpdateReleaseInfo? TryMapRelease(LatestRelease release)
        {
            if (!SemanticVersion.TryParse(release.Tag_name, out var version) ||
                string.IsNullOrWhiteSpace(release.Html_url) ||
                !Uri.TryCreate(release.Html_url, UriKind.Absolute, out var releasePageUrl))
            {
                return null;
            }

            var assets = (release.Assets ?? Array.Empty<LatestReleaseAsset>())
                .Where(asset => !string.IsNullOrWhiteSpace(asset.Name))
                .Where(asset => Uri.TryCreate(asset.Browser_download_url, UriKind.Absolute, out _))
                .Select(asset => new UpdateAsset(
                    asset.Name,
                    new Uri(asset.Browser_download_url, UriKind.Absolute),
                    asset.Size))
                .ToArray();

            return new UpdateReleaseInfo(version, release.Tag_name, releasePageUrl, release.Prerelease, assets);
        }
    }
}

