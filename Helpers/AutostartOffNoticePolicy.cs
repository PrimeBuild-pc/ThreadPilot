namespace ThreadPilot.Helpers
{
    using System;
    using ThreadPilot.Models;

    /// <summary>
    /// Autostart is opt-in: a new install shows one notice saying it is off, with a way to turn it on.
    /// </summary>
    public static class AutostartOffNoticePolicy
    {
        public static bool ShouldShow(ApplicationSettingsModel settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return !settings.AutostartWithWindows && !settings.HasSeenAutostartOffNotice;
        }
    }
}
