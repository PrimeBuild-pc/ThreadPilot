namespace ThreadPilot.Core.Tests
{
    using ThreadPilot.Services;

    public sealed class AppRefreshPolicyTests
    {
        [Theory]
        [InlineData(AppActivityState.ForegroundProcessView, true, true, true, false, true, true)]
        [InlineData(AppActivityState.ForegroundDiagnosticsView, false, false, false, true, true, true)]
        [InlineData(AppActivityState.ForegroundOtherTab, false, false, false, false, true, true)]
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

        [Fact]
        public void Evaluate_WhenStateIsUnknown_KeepsBackgroundAutomationOnly()
        {
            var decision = AppRefreshPolicy.Evaluate((AppActivityState)999);

            Assert.False(decision.ProcessUiRefreshEnabled);
            Assert.False(decision.ImmediateProcessRefresh);
            Assert.False(decision.VirtualizedPreloadEnabled);
            Assert.False(decision.PerformanceUiMonitoringEnabled);
            Assert.False(decision.PowerPlanUiRefreshEnabled);
            Assert.True(decision.BackgroundAutomationEnabled);
        }

        [Theory]
        [InlineData(null, AppActivityState.ForegroundProcessView, true)]
        [InlineData(AppActivityState.ForegroundProcessView, AppActivityState.ForegroundProcessView, false)]
        [InlineData(AppActivityState.ForegroundDiagnosticsView, AppActivityState.ForegroundDiagnosticsView, false)]
        [InlineData(AppActivityState.ForegroundOtherTab, AppActivityState.ForegroundOtherTab, false)]
        [InlineData(AppActivityState.Minimized, AppActivityState.Minimized, false)]
        [InlineData(AppActivityState.TrayHidden, AppActivityState.TrayHidden, false)]
        [InlineData(AppActivityState.TrayHidden, AppActivityState.ForegroundProcessView, true)]
        [InlineData(AppActivityState.ForegroundProcessView, AppActivityState.ForegroundOtherTab, true)]
        [InlineData(AppActivityState.ForegroundOtherTab, AppActivityState.ForegroundDiagnosticsView, true)]
        public void ShouldApplyTransition_SkipsRedundantStateTransitions(
            AppActivityState? previousState,
            AppActivityState nextState,
            bool expected)
        {
            Assert.Equal(expected, AppRefreshPolicy.ShouldApplyTransition(previousState, nextState));
        }
    }
}
