/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ThreadPilot.Services
{
    public static class GitHubUpdateChecker
    {
        private record LatestRelease(string tag_name, bool prerelease, bool draft, string html_url);

        public static async Task<(Version? latest, string? releaseUrl)> GetLatestVersionAsync(string owner, string repo)
        {
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner is required", nameof(owner));
            if (string.IsNullOrWhiteSpace(repo)) throw new ArgumentException("Repository is required", nameof(repo));

            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ThreadPilot", "1.0"));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var json = await http.GetStringAsync(url);
            var release = JsonSerializer.Deserialize<LatestRelease>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (release is null || release.draft || release.prerelease || string.IsNullOrWhiteSpace(release.tag_name))
            {
                return (null, null);
            }

            var tag = release.tag_name.Trim();
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                tag = tag[1..];
            }

            var sanitized = tag.Split('-', '+')[0];

            return Version.TryParse(sanitized, out var version)
                ? (version, release.html_url)
                : (null, release.html_url);
        }
    }
}

