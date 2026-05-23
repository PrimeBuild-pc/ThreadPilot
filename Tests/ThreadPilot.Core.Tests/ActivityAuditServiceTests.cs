namespace ThreadPilot.Core.Tests
{
    using Microsoft.Extensions.Logging.Abstractions;
    using ThreadPilot.Services;

    public sealed class ActivityAuditServiceTests
    {
        [Theory]
        [InlineData("ThemeChanged", "Theme changed to Dark", "Settings", ActivityAuditSeverity.Success)]
        [InlineData("SystemTweakApplied", "Core Parking enabled", "Tweaks", ActivityAuditSeverity.Success)]
        [InlineData("SystemTweakFailed", "Failed to enable Core Parking", "Tweaks", ActivityAuditSeverity.Error)]
        [InlineData("OptimizationMonitoringStarted", "Performance monitoring started", "Optimization", ActivityAuditSeverity.Success)]
        [InlineData("OptimizationActionFailed", "Failed to start performance monitoring: unavailable", "Optimization", ActivityAuditSeverity.Error)]
        [InlineData("PowerPlanApplied", "Applied power plan Gaming", "Power Plans", ActivityAuditSeverity.Success)]
        [InlineData("PowerPlanDeleted", "Deleted power plan Gaming", "Power Plans", ActivityAuditSeverity.Success)]
        [InlineData("PowerPlansRefreshed", "Refreshed power plan list", "Power Plans", ActivityAuditSeverity.Success)]
        [InlineData("ProcessPriorityChanged", "CPU priority changed for Game.exe: High", "Priority", ActivityAuditSeverity.Success)]
        [InlineData("ProcessPriorityChangeFailed", "Windows denied this change.", "Priority", ActivityAuditSeverity.Warning)]
        [InlineData("ProcessPriorityBlocked", "Realtime priority is blocked by ThreadPilot.", "Priority", ActivityAuditSeverity.Warning)]
        [InlineData("ProcessMemoryPriorityChanged", "Memory priority changed for Game.exe: Low", "Memory Priority", ActivityAuditSeverity.Success)]
        [InlineData("ProcessMemoryPriorityFailed", "The process appears protected by anti-cheat or process protection.", "Memory Priority", ActivityAuditSeverity.Warning)]
        [InlineData("CpuSetsCleared", "CPU Sets cleared for Game.exe", "Affinity", ActivityAuditSeverity.Success)]
        [InlineData("CpuSetsClearFailed", "The process exited before ThreadPilot could apply the change.", "Affinity", ActivityAuditSeverity.Error)]
        [InlineData("ProcessAffinityApplied", "Affinity applied successfully to Game.exe", "Affinity", ActivityAuditSeverity.Success)]
        [InlineData("ProcessAffinityFailed", "The process appears protected by anti-cheat or process protection.", "Affinity", ActivityAuditSeverity.Warning)]
        [InlineData("PersistentRuleSaved", "Saved rule for Game.exe.", "Rules", ActivityAuditSeverity.Success)]
        [InlineData("PersistentRuleSaveFailed", "Failed to save rule for Game.exe.", "Rules", ActivityAuditSeverity.Error)]
        [InlineData("PersistentRuleAutoApplied", "Auto-applied saved rule for Game.exe.", "Rules", ActivityAuditSeverity.Success)]
        [InlineData("PersistentRuleAutoApplyFailed", "Failed to auto-apply saved rule for Game.exe: protected process.", "Rules", ActivityAuditSeverity.Warning)]
        public async Task LogUserActionAsync_CreatesVisibleActivityEntry(
            string action,
            string details,
            string expectedCategory,
            ActivityAuditSeverity expectedSeverity)
        {
            var service = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);

            await service.LogUserActionAsync(action, details, "PID: 42");

            var entry = Assert.Single(await service.GetEntriesAsync());
            Assert.Equal(expectedCategory, entry.Category);
            Assert.Equal(expectedSeverity, entry.Severity);
            Assert.Equal(details, entry.Message);
            Assert.Equal("PID: 42", entry.Details);
        }

        [Fact]
        public async Task GetEntriesAsync_ReturnsMostRecentFirstAndPreservesTimestamp()
        {
            var service = new ActivityAuditService(NullLogger<ActivityAuditService>.Instance);

            await service.LogInfoAsync("Diagnostics", "First");
            await Task.Delay(5);
            await service.LogSuccessAsync("Power Plans", "Second");

            var entries = await service.GetEntriesAsync();

            Assert.Collection(
                entries,
                entry => Assert.Equal("Second", entry.Message),
                entry => Assert.Equal("First", entry.Message));
            Assert.All(entries, entry => Assert.NotEqual(default, entry.Timestamp));
        }
    }
}
