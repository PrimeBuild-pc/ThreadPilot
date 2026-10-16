namespace ThreadPilot.Core.Tests
{
    using System.Diagnostics;
    using ThreadPilot.Models;
    using ThreadPilot.Services;

    public sealed class PersistentRuleResolutionTests
    {
        private static readonly ProcessModel Process = new() { Name = "game.exe", ExecutablePath = @"C:\Games\Foo\game.exe" };

        [Fact]
        public void MatchInApplyOrder_PutsMoreSpecificRulesLast()
        {
            var byName = Rule("name", processName: "game.exe");
            var exact = Rule("exact", path: @"C:\Games\Foo\game.exe");
            var wildcard = Rule("wildcard", path: @"C:\Games\*\game.exe");
            var other = Rule("other", processName: "other.exe");

            var order = PersistentRuleResolution.MatchInApplyOrder([exact, other, wildcard, byName], Process, new PersistentProcessRuleMatcher());

            Assert.Equal(["name", "wildcard", "exact"], order.Select(rule => rule.Id));
        }

        [Fact]
        public void HasConflicts_OnlyWhenAppliedValuesDiffer()
        {
            var high = Rule("a", processName: "game.exe") with { Priority = ProcessPriorityClass.High, ApplyPriorityOnStart = true };
            var low = Rule("b", processName: "game.exe") with { Priority = ProcessPriorityClass.BelowNormal, ApplyPriorityOnStart = true };
            var unapplied = low with { Id = "c", ApplyPriorityOnStart = false };

            Assert.True(PersistentRuleResolution.HasConflicts([high, low]));
            Assert.False(PersistentRuleResolution.HasConflicts([high, unapplied]));
        }

        private static PersistentProcessRule Rule(string id, string? processName = null, string? path = null) =>
            new() { Id = id, IsEnabled = true, ProcessName = processName, ExecutablePath = path };
    }
}
