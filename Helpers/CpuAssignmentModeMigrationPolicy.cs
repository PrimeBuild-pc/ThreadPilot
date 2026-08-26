namespace ThreadPilot.Helpers
{
    using System;
    using System.Collections.Generic;
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
        /// The profile migration above does not reach rules already written to persistent_rules.json:
        /// a rule stores the mode it was saved with, so a rule captured before 1.7.1 keeps applying
        /// CPU Sets on every process start - invisible in Task Manager, and indistinguishable from a
        /// rule that never runs. Move those once too. Only rules carrying a CpuSelection are affected;
        /// with a legacy mask, Automatic already applies a real affinity.
        /// Returns the migrated rules, or null when nothing needed changing.
        /// </summary>
        public static IReadOnlyList<PersistentProcessRule>? ApplyToRules(
            IReadOnlyList<PersistentProcessRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            var migrated = new List<PersistentProcessRule>(rules.Count);
            var changed = false;
            var now = DateTime.UtcNow;

            foreach (var rule in rules)
            {
                if (rule.CpuAssignmentMode != CpuAssignmentMode.Automatic ||
                    !rule.ApplyAffinityOnStart ||
                    !HasSelection(rule.CpuSelection))
                {
                    migrated.Add(rule);
                    continue;
                }

                migrated.Add(rule with
                {
                    CpuAssignmentMode = CpuAssignmentMode.AffinityMask,
                    UpdatedAt = now,
                });
                changed = true;
            }

            return changed ? migrated : null;
        }

        private static bool HasSelection(CpuSelection? selection) =>
            selection != null &&
            (selection.CpuSetIds.Count > 0 || selection.LogicalProcessors.Count > 0);

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
