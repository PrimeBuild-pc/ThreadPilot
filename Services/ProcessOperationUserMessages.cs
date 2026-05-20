namespace ThreadPilot.Services
{
    using System.Diagnostics;

    internal static class ProcessOperationUserMessages
    {
        public const string AccessDenied =
            "Windows denied this change. The process may require administrator rights or may be protected.";

        public const string AntiCheatProtectedLikely =
            "The process appears protected by anti-cheat or process protection. ThreadPilot will not try to override it.";

        public const string AdminClarification =
            "Administrator mode may help with normal permission issues, but cannot bypass anti-cheat or protected process restrictions.";

        public const string LegacyFallbackBlocked =
            "This CPU selection cannot be safely represented by legacy affinity APIs on this topology. CPU Sets are required for this selection.";

        public const string InvalidTopology =
            "This CPU selection does not match the current CPU topology. Review or recreate the preset.";

        public const string ProcessExited =
            "The process exited before ThreadPilot could apply the change.";

        public const string CpuSetsUnavailable =
            "Windows CPU Sets are unavailable or rejected this selection. ThreadPilot will use a safe fallback only when possible.";

        public const string HighPriorityWarning =
            "High priority can improve responsiveness for some workloads but may reduce system responsiveness.";

        public const string RealtimePriorityBlocked =
            "Realtime priority is blocked by ThreadPilot because it can make Windows unstable or unresponsive.";

        public const string PersistentLaunchTimePriorityNotice =
            "Persistent launch-time priority may be supported for normal processes, but it does not bypass protected process or anti-cheat restrictions.";

        public const string PersistentRulesDescription =
            "Applies saved rules when a matching process starts. Some protected or anti-cheat processes may reject changes. Administrator mode can help with normal permission issues but cannot bypass protection.";

        public const string PersistentRulesProtectedProcessWarning =
            "The process appears protected by anti-cheat or process protection. ThreadPilot will not try to override it.";
    }

    internal static class ProcessPriorityGuardrails
    {
        public static string? GetWarning(ProcessPriorityClass priority) =>
            priority == ProcessPriorityClass.High
                ? ProcessOperationUserMessages.HighPriorityWarning
                : null;

        public static bool IsBlocked(ProcessPriorityClass priority) =>
            priority == ProcessPriorityClass.RealTime;

        public static void ThrowIfBlocked(ProcessPriorityClass priority)
        {
            if (IsBlocked(priority))
            {
                throw new InvalidOperationException(ProcessOperationUserMessages.RealtimePriorityBlocked);
            }
        }
    }
}
