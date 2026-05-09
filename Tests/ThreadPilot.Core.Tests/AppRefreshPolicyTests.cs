namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class AppRefreshPolicyTests
    {
        [Theory]
        [InlineData(AppActivityState.ForegroundProcessView, true, true, true, true, true, true)]
        [InlineData(AppActivityState.ForegroundOtherTab, false, false, false, true, true, true)]
        [InlineData(AppActivityState.Minimized, false, false, false, false, false, true)]
        [InlineData(AppActivityState.TrayHidden, false, false, false, false, false, true)]
        public void Evaluate_ReturnsExpectedRefreshDecision(
            AppActivityState state,
            bool processUiRefreshEnabled,
            bool immediateProcessRefresh,
            bool virtualizedPreloadEnabled,
            bool performanceUiMonitoringEnabled,
            bool powerPlanUiRefreshEnabled,
            bool backgroundAutomationEnabled)
        {
            var decision = AppRefreshPolicy.Evaluate(state);

            Assert.Equal(processUiRefreshEnabled, decision.ProcessUiRefreshEnabled);
            Assert.Equal(immediateProcessRefresh, decision.ImmediateProcessRefresh);
            Assert.Equal(virtualizedPreloadEnabled, decision.VirtualizedPreloadEnabled);
            Assert.Equal(performanceUiMonitoringEnabled, decision.PerformanceUiMonitoringEnabled);
            Assert.Equal(powerPlanUiRefreshEnabled, decision.PowerPlanUiRefreshEnabled);
            Assert.Equal(backgroundAutomationEnabled, decision.BackgroundAutomationEnabled);
        }
    }
}
