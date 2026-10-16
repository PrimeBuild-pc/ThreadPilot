namespace ThreadPilot.Services
{
    using ThreadPilot.Helpers;
    using ThreadPilot.Models;

    public static class PersistentRuleResolution
    {
        // Apply broad rules first; more specific and then more recently edited rules win per setting.
        public static IReadOnlyList<PersistentProcessRule> MatchInApplyOrder(
            IEnumerable<PersistentProcessRule> rules, ProcessModel process, IPersistentProcessRuleMatcher matcher) =>
            rules.Where(rule => matcher.IsMatch(rule, process))
                .OrderBy(rule => string.IsNullOrWhiteSpace(rule.ExecutablePath) ? 0 : PathPatternMatcher.HasWildcard(rule.ExecutablePath) ? 1 : 2)
                .ThenBy(rule => rule.ExecutablePath?.Count(character => character is not ('*' or '?')) ?? 0)
                .ThenBy(rule => rule.UpdatedAt)
                .ThenBy(rule => rule.Id, StringComparer.Ordinal)
                .ToArray();

        public static bool HasConflicts(IReadOnlyList<PersistentProcessRule> rules) =>
            rules.Where(r => r.ApplyPriorityOnStart).Select(r => r.Priority).Distinct().Skip(1).Any() ||
            rules.Where(r => r.ApplyMemoryPriorityOnStart).Select(r => r.MemoryPriority).Distinct().Skip(1).Any() ||
            rules.Where(r => r.ApplyIoPriorityOnStart).Select(r => r.IoPriority).Distinct().Skip(1).Any() ||
            rules.Where(r => r.ApplyEfficiencyModeOnStart).Select(r => r.EfficiencyMode).Distinct().Skip(1).Any() ||
            rules.Where(r => r.ApplyAffinityOnStart)
                .Select(r => $"{r.CpuAssignmentMode}:{r.LegacyAffinityMask}:{string.Join(",", (r.CpuSelection?.GlobalLogicalProcessorIndexes ?? []).Order())}")
                .Distinct().Skip(1).Any();
    }
}
