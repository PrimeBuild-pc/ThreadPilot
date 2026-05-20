/*
 * ThreadPilot - persistent process rule models.
 */
namespace ThreadPilot.Models
{
    using System;
    using System.Diagnostics;

    public sealed record PersistentProcessRule
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N");

        public string Name { get; init; } = string.Empty;

        public bool IsEnabled { get; init; }

        public string? ProcessName { get; init; }

        public string? ExecutablePath { get; init; }

        public CpuSelection? CpuSelection { get; init; }

        public long? LegacyAffinityMask { get; init; }

        public ProcessPriorityClass? Priority { get; init; }

        public bool ApplyAffinityOnStart { get; init; }

        public bool ApplyPriorityOnStart { get; init; }

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;

        public string? Description { get; init; }
    }

    public sealed record PersistentRuleApplyResult
    {
        public bool Success { get; init; }

        public string RuleId { get; init; } = string.Empty;

        public int ProcessId { get; init; }

        public string ProcessName { get; init; } = string.Empty;

        public bool AffinityApplied { get; init; }

        public bool PriorityApplied { get; init; }

        public string? ErrorCode { get; init; }

        public string UserMessage { get; init; } = string.Empty;

        public string TechnicalMessage { get; init; } = string.Empty;

        public bool IsAccessDenied { get; init; }

        public bool IsAntiCheatLikely { get; init; }

        public bool IsProcessExited { get; init; }
    }
}
