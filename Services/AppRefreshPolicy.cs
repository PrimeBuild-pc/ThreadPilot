/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    /// <summary>
    /// Represents the current UI activity state used to decide refresh work.
    /// </summary>
    public enum AppActivityState
    {
        ForegroundProcessView,
        ForegroundOtherTab,
        Minimized,
        TrayHidden,
    }

    /// <summary>
    /// Describes refresh and monitoring work allowed for a UI activity state.
    /// </summary>
    public sealed record AppRefreshDecision(
        bool ProcessUiRefreshEnabled,
        bool ImmediateProcessRefresh,
        bool VirtualizedPreloadEnabled,
        bool PerformanceUiMonitoringEnabled,
        bool PowerPlanUiRefreshEnabled,
        bool BackgroundAutomationEnabled);

    /// <summary>
    /// Central policy for foreground/background refresh decisions.
    /// </summary>
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
                    PerformanceUiMonitoringEnabled: true,
                    PowerPlanUiRefreshEnabled: true,
                    BackgroundAutomationEnabled: true),
                AppActivityState.ForegroundOtherTab => new AppRefreshDecision(
                    ProcessUiRefreshEnabled: false,
                    ImmediateProcessRefresh: false,
                    VirtualizedPreloadEnabled: false,
                    PerformanceUiMonitoringEnabled: true,
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
