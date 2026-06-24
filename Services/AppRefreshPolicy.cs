namespace ThreadPilot.Services
{
    public enum AppActivityState
    {
        ForegroundProcessView,
        ForegroundDiagnosticsView,
        ForegroundOtherTab,
        Minimized,
        TrayHidden,
    }

    public sealed record AppRefreshDecision(
        bool ProcessUiRefreshEnabled,
        bool ImmediateProcessRefresh,
        bool VirtualizedPreloadEnabled,
        bool PerformanceUiMonitoringEnabled,
        bool PowerPlanUiRefreshEnabled,
        bool BackgroundAutomationEnabled);

    public static class AppRefreshPolicy
    {
        public static bool ShouldApplyTransition(AppActivityState? previousState, AppActivityState nextState)
        {
            return previousState != nextState;
        }

        public static AppRefreshDecision Evaluate(AppActivityState state)
        {
            return state switch
            {
                AppActivityState.ForegroundProcessView => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: true,
                    ImmediateProcessRefresh: true,
                    VirtualizedPreloadEnabled: true,
                    PerformanceUiMonitoringEnabled: false,
                    PowerPlanUiRefreshEnabled: true,
                    BackgroundAutomationEnabled: true),
                AppActivityState.ForegroundDiagnosticsView => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: false,
                    ImmediateProcessRefresh: false,
                    VirtualizedPreloadEnabled: false,
                    PerformanceUiMonitoringEnabled: true,
                    PowerPlanUiRefreshEnabled: true,
                    BackgroundAutomationEnabled: true),
                AppActivityState.ForegroundOtherTab => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: false,
                    ImmediateProcessRefresh: false,
                    VirtualizedPreloadEnabled: false,
                    PerformanceUiMonitoringEnabled: false,
                    PowerPlanUiRefreshEnabled: true,
                    BackgroundAutomationEnabled: true),
                AppActivityState.Minimized or AppActivityState.TrayHidden => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: false,
                    ImmediateProcessRefresh: false,
                    VirtualizedPreloadEnabled: false,
                    PerformanceUiMonitoringEnabled: false,
                    PowerPlanUiRefreshEnabled: false,
                    BackgroundAutomationEnabled: true),
                _ => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: false,
                    ImmediateProcessRefresh: false,
                    VirtualizedPreloadEnabled: false,
                    PerformanceUiMonitoringEnabled: false,
                    PowerPlanUiRefreshEnabled: false,
                    BackgroundAutomationEnabled: true),
            };
        }
    }
}
