namespace ThreadPilot.Services
{
    public enum ActivityAuditSeverity
    {
        Info,
        Success,
        Warning,
        Error,
    }

    public sealed record ActivityAuditEntry
    {
        public DateTime Timestamp { get; init; }

        public string Category { get; init; } = string.Empty;

        public ActivityAuditSeverity Severity { get; init; }

        public string Message { get; init; } = string.Empty;

        public string? Details { get; init; }
    }

    public interface IActivityAuditService
    {
        event EventHandler<ActivityAuditEntry>? EntryAdded;

        Task LogInfoAsync(string category, string message, string? details = null);

        Task LogSuccessAsync(string category, string message, string? details = null);

        Task LogWarningAsync(string category, string message, string? details = null);

        Task LogErrorAsync(string category, string message, string? details = null);

        Task LogUserActionAsync(string action, string details, string? context = null);

        Task<IReadOnlyList<ActivityAuditEntry>> GetEntriesAsync(DateTime? fromDate = null, DateTime? toDate = null);

        Task ClearDisplayAsync();
    }
}
