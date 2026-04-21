namespace ThreadPilot.Core.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    public sealed class GitHubUpdateCheckerTests
    {
        [Fact]
        public async Task GetLatestVersionAsync_ReturnsStableRelease_WhenTagIsSemver()
        {
            var client = new FakeGitHubReleaseClient(
                """
                {
                  "tag_name": "v1.2.3",
                  "prerelease": false,
                  "draft": false,
                  "html_url": "https://example.test/releases/v1.2.3"
                }
                """);
            var checker = new GitHubUpdateChecker(client);

            var (latest, releaseUrl) = await checker.GetLatestVersionAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.Equal(new System.Version(1, 2, 3), latest);
            Assert.Equal("https://example.test/releases/v1.2.3", releaseUrl);
        }

        [Fact]
        public async Task GetLatestVersionAsync_IgnoresDraftOrPrerelease()
        {
            var client = new FakeGitHubReleaseClient(
                """
                {
                  "tag_name": "v1.2.3-beta1",
                  "prerelease": true,
                  "draft": false,
                  "html_url": "https://example.test/releases/v1.2.3-beta1"
                }
                """);
            var checker = new GitHubUpdateChecker(client);

            var (latest, releaseUrl) = await checker.GetLatestVersionAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.Null(latest);
            Assert.Null(releaseUrl);
        }

        [Fact]
        public async Task GetLatestVersionAsync_ReturnsNull_WhenTagCannotBeParsed()
        {
            var client = new FakeGitHubReleaseClient(
                """
                {
                  "tag_name": "release-main",
                  "prerelease": false,
                  "draft": false,
                  "html_url": "https://example.test/releases/release-main"
                }
                """);
            var checker = new GitHubUpdateChecker(client);

            var (latest, releaseUrl) = await checker.GetLatestVersionAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.Null(latest);
            Assert.Equal("https://example.test/releases/release-main", releaseUrl);
        }

        private sealed class FakeGitHubReleaseClient : IGitHubReleaseClient
        {
            private readonly string responseJson;

            public FakeGitHubReleaseClient(string responseJson)
            {
                this.responseJson = responseJson;
            }

            public Task<string> GetLatestReleaseJsonAsync(string owner, string repo, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.responseJson);
            }
        }
    }
}
