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
