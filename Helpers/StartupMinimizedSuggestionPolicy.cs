namespace ThreadPilot.Helpers
{
    using System;
    using ThreadPilot.Models;

    public static class StartupMinimizedSuggestionPolicy
    {
        public static bool ShouldShow(ApplicationSettingsModel settings, StartupWindowBehavior behavior)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return behavior.ShouldShowWindow
                && !settings.StartMinimized
                && !settings.HasSeenStartupMinimizedSuggestion;
        }
    }
}
