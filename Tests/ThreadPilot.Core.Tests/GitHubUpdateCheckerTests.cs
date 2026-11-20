namespace ThreadPilot.Core.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using ThreadPilot.Services;
    using ThreadPilot.Services.Abstractions;

    public sealed class GitHubUpdateCheckerTests
    {
        [Fact]
        public async Task GetLatestReleaseInfoAsync_ReturnsStableRelease_WhenTagIsSemver()
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

            var release = await checker.GetLatestReleaseInfoAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.NotNull(release);
            Assert.Equal(new SemanticVersion(1, 2, 3), release.Version);
            Assert.Equal("https://example.test/releases/v1.2.3", release.ReleasePageUrl.AbsoluteUri);
        }

        [Theory]
        [InlineData("true", "false")]
        [InlineData("false", "true")]
        public async Task GetLatestReleaseInfoAsync_IgnoresDraftOrPrerelease(string draft, string prerelease)
        {
            var client = new FakeGitHubReleaseClient(
                $$"""
                {
                  "tag_name": "v1.2.3",
                  "prerelease": {{prerelease}},
                  "draft": {{draft}},
                  "html_url": "https://example.test/releases/v1.2.3"
                }
                """);
            var checker = new GitHubUpdateChecker(client);

            var release = await checker.GetLatestReleaseInfoAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.Null(release);
        }

        [Fact]
        public async Task GetLatestReleaseInfoAsync_ReturnsNull_WhenTagCannotBeParsed()
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

            var release = await checker.GetLatestReleaseInfoAsync("PrimeBuild-pc", "ThreadPilot");

            Assert.Null(release);
        }

        private sealed class FakeGitHubReleaseClient : IGitHubReleaseClient
        {
            private readonly string responseJson;

            public FakeGitHubReleaseClient(string responseJson)
            {
                this.responseJson = responseJson;
            }

            public Task<string> GetReleasesJsonAsync(string owner, string repo, CancellationToken cancellationToken = default)
            {
                return Task.FromResult($"[{this.responseJson}]");
            }
        }
    }
}
