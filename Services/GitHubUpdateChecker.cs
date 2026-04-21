/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Services.Abstractions;

    public sealed class GitHubUpdateChecker
    {
        private readonly IGitHubReleaseClient gitHubReleaseClient;

        private record LatestRelease(string Tag_name, bool Prerelease, bool Draft, string Html_url);

        public GitHubUpdateChecker(IGitHubReleaseClient gitHubReleaseClient)
        {
            this.gitHubReleaseClient = gitHubReleaseClient ?? throw new ArgumentNullException(nameof(gitHubReleaseClient));
        }

        public async Task<(Version? latest, string? releaseUrl)> GetLatestVersionAsync(
            string owner,
            string repo,
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

            var json = await this.gitHubReleaseClient.GetLatestReleaseJsonAsync(owner, repo, cancellationToken);
            var release = JsonSerializer.Deserialize<LatestRelease>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (release is null || release.Draft || release.Prerelease || string.IsNullOrWhiteSpace(release.Tag_name))
            {
                return (null, null);
            }

            var tag = release.Tag_name.Trim();
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                tag = tag[1..];
            }

            var sanitized = tag.Split('-', '+')[0];

            return Version.TryParse(sanitized, out var version)
                ? (version, release.Html_url)
                : (null, release.Html_url);
        }
    }
}

