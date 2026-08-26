namespace ThreadPilot.Helpers
{
    using System;
    using ThreadPilot.Models;

    /// <summary>
    /// v1.7.1 changed the shipped default CPU assignment mode from <see cref="CpuAssignmentMode.Automatic"/>
    /// to <see cref="CpuAssignmentMode.AffinityMask"/>, because Automatic applies only Windows CPU Sets:
    /// a soft scheduling preference that leaves the affinity mask shown in Task Manager unchanged.
    /// A persisted setting always wins over a new default, so everyone upgrading kept the mode that
    /// made the affinity feature look like it did nothing. Move them once, and tell them it happened.
    /// </summary>
    public static class CpuAssignmentModeMigrationPolicy
    {
        /// <summary>
        /// Runs the one-time migration. Returns true when the profile was modified and must be
        /// persisted - including the case where only the completion flags changed. Persisting those
        /// matters: without them a profile is migrated again on the next launch, which would undo a
        /// deliberate return to <see cref="CpuAssignmentMode.Automatic"/>.
        /// </summary>
        public static bool Apply(ApplicationSettingsModel settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (settings.HasMigratedCpuAssignmentModeDefault)
            {
                return false;
            }

            settings.HasMigratedCpuAssignmentModeDefault = true;

            if (settings.DefaultCpuAssignmentMode != CpuAssignmentMode.Automatic)
            {
                // Nothing changed for this user, so there is nothing to tell them about - but the
                // completion flags still have to reach disk.
                settings.HasSeenCpuAssignmentModeChangeNotice = true;
                return true;
            }

            settings.DefaultCpuAssignmentMode = CpuAssignmentMode.AffinityMask;
            settings.HasSeenCpuAssignmentModeChangeNotice = false;
            return true;
        }

        /// <summary>
        /// True while the migration has moved this profile but the user has not acknowledged it yet.
        /// Surviving a restart matters here: the notice is the only thing telling them their mode changed.
        /// </summary>
        public static bool ShouldShowNotice(ApplicationSettingsModel settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return settings.HasMigratedCpuAssignmentModeDefault
                && !settings.HasSeenCpuAssignmentModeChangeNotice;
        }
    }
}
