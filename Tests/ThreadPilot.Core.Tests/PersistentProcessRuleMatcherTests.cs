/*
 * ThreadPilot - persistent process rule matcher tests.
 */
namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class PersistentProcessRuleMatcherTests
    {
        private readonly PersistentProcessRuleMatcher matcher = new();

        [Fact]
        public void IsMatch_WithProcessName_MatchesCaseInsensitive()
        {
            var rule = CreateRule(processName: "GAME.EXE");
            var process = CreateProcess(name: "game.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        [Fact]
        public void IsMatch_WithExecutablePath_MatchesCaseInsensitive()
        {
            var rule = CreateRule(executablePath: @"C:\Games\App\Game.exe");
            var process = CreateProcess(executablePath: @"c:\games\app\game.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        [Fact]
        public void IsMatch_WithNameAndPath_UsesExecutablePathPriority()
        {
            var rule = CreateRule(processName: "game.exe", executablePath: @"C:\Games\App\Game.exe");
            var process = CreateProcess(name: "game.exe", executablePath: @"C:\Other\Game.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.False(result);
        }

        [Fact]
        public void IsMatch_WithDisabledRule_ReturnsFalse()
        {
            var rule = CreateRule(processName: "game.exe") with { IsEnabled = false };
            var process = CreateProcess(name: "game.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.False(result);
        }

        [Fact]
        public void IsMatch_WithProcessWithoutExecutablePath_CanMatchProcessName()
        {
            var rule = CreateRule(processName: "game.exe");
            var process = CreateProcess(name: "GAME.EXE", executablePath: string.Empty);

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        [Fact]
        public void IsMatch_WithNullPaths_DoesNotThrow()
        {
            var rule = CreateRule(processName: null, executablePath: null);
            var process = CreateProcess(name: "game.exe", executablePath: null);

            var exception = Record.Exception(() => this.matcher.IsMatch(rule, process));

            Assert.Null(exception);
        }

        [Fact]
        public void IsMatch_WithWildcardExecutablePath_MatchesPattern()
        {
            var rule = CreateRule(executablePath: @"*Raycast*\backend\node.exe");
            var process = CreateProcess(name: "node.exe", executablePath: @"C:\Program Files\WindowsApps\Raycast.Raycast_2.5.3.0_x64__qypenmj9wpt2a\Raycast\backend\node.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        [Fact]
        public void IsMatch_WithWildcardExecutablePath_RejectsNonMatchingPath()
        {
            var rule = CreateRule(executablePath: @"*Raycast*\backend\node.exe");
            var process = CreateProcess(name: "node.exe", executablePath: @"C:\nvm4w\nodejs\node.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.False(result);
        }

        [Fact]
        public void IsMatch_WithWildcardExecutablePath_MatchesVersionGlob()
        {
            var rule = CreateRule(executablePath: @"C:\Program Files\WindowsApps\Raycast.Raycast_*_x64__*\Raycast\backend\node.exe");
            var process = CreateProcess(name: "node.exe", executablePath: @"C:\Program Files\WindowsApps\Raycast.Raycast_2.5.3.0_x64__qypenmj9wpt2a\Raycast\backend\node.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        [Fact]
        public void IsMatch_WithQuestionMarkWildcard_MatchesSingleCharacter()
        {
            var rule = CreateRule(executablePath: @"C:\Games\App?\Game.exe");
            var matchProcess = CreateProcess(name: "game.exe", executablePath: @"C:\Games\App1\Game.exe");
            var noMatchProcess = CreateProcess(name: "game.exe", executablePath: @"C:\Games\App12\Game.exe");

            Assert.True(this.matcher.IsMatch(rule, matchProcess));
            Assert.False(this.matcher.IsMatch(rule, noMatchProcess));
        }

        [Fact]
        public void IsMatch_WithForwardSlashWildcard_MatchesNormalizedBackslashes()
        {
            var rule = CreateRule(executablePath: @"*/Raycast/backend/node.exe");
            var process = CreateProcess(name: "node.exe", executablePath: @"C:\Program Files\WindowsApps\Raycast.Raycast_2.5.3.0_x64__qypenmj9wpt2a\Raycast\backend\node.exe");

            var result = this.matcher.IsMatch(rule, process);

            Assert.True(result);
        }

        private static PersistentProcessRule CreateRule(string? processName = null, string? executablePath = null) =>
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "Rule",
                IsEnabled = true,
                ProcessName = processName,
                ExecutablePath = executablePath,
            };

        private static ProcessModel CreateProcess(string name = "game.exe", string? executablePath = @"C:\Games\Game.exe") =>
            new()
            {
                ProcessId = 42,
                Name = name,
                ExecutablePath = executablePath ?? string.Empty,
            };
    }
}
