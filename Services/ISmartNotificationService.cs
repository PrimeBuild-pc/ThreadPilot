namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public enum NotificationCategory
    {
        System,
        Process,
        Performance,
        PowerPlan,
        Error,
        Warning,
        Information,
        UserAction,
    }

    public class SmartNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        public NotificationCategory Category { get; set; } = NotificationCategory.Information;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ScheduledFor { get; set; }

        public TimeSpan? ExpiresAfter { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();

        public string DeduplicationKey { get; set; } = string.Empty;

        public bool IsPersistent { get; set; } = false;

        public int RetryCount { get; set; } = 0;

        public int MaxRetries { get; set; } = 3;
    }

    public class NotificationThrottleConfig
    {
        public NotificationCategory Category { get; set; }

        public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(30);

        public int MaxPerHour { get; set; } = 10;

        public int MaxPerDay { get; set; } = 50;

        public bool EnableDeduplication { get; set; } = true;

        public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(5);
    }

    public class NotificationPreferences
    {
        public bool IsEnabled { get; set; } = true;

        public bool DoNotDisturbMode { get; set; } = false;

        public TimeSpan DoNotDisturbStart { get; set; } = TimeSpan.FromHours(22); // 10 PM

        public TimeSpan DoNotDisturbEnd { get; set; } = TimeSpan.FromHours(8);   // 8 AM

        public NotificationPriority MinimumPriority { get; set; } = NotificationPriority.Normal;

        public Dictionary<NotificationCategory, bool> CategoryEnabled { get; set; } = new();

        public Dictionary<NotificationCategory, NotificationThrottleConfig> ThrottleConfigs { get; set; } = new();

        public bool ShowOnlyWhenMinimized { get; set; } = false;

        public bool PlaySounds { get; set; } = true;

        public int DefaultDisplayDuration { get; set; } = 5000; // milliseconds
    }

    public class SmartNotificationEventArgs : EventArgs
    {
        public SmartNotification Notification { get; set; } = new();

        public string Reason { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public interface ISmartNotificationService
    {
        Task InitializeAsync();

        Task<bool> SendNotificationAsync(SmartNotification notification);

        Task<bool> SendNotificationAsync(string title, string message,
            NotificationPriority priority = NotificationPriority.Normal,
            NotificationCategory category = NotificationCategory.Information);

        Task<bool> ScheduleNotificationAsync(SmartNotification notification, DateTime deliveryTime);

        Task<bool> CancelNotificationAsync(string notificationId);

        Task<List<SmartNotification>> GetPendingNotificationsAsync();

        Task<List<SmartNotification>> GetNotificationHistoryAsync(TimeSpan? period = null);

        Task ClearHistoryAsync();

        Task UpdatePreferencesAsync(NotificationPreferences preferences);

        Task<NotificationPreferences> GetPreferencesAsync();

        Task SetDoNotDisturbAsync(bool enabled, TimeSpan? duration = null);

        bool IsDoNotDisturbActive();

        Task<Dictionary<string, object>> GetStatisticsAsync();

        Task<bool> TestNotificationAsync();

        event EventHandler<SmartNotificationEventArgs>? NotificationSent;

        event EventHandler<SmartNotificationEventArgs>? NotificationThrottled;

        event EventHandler<SmartNotificationEventArgs>? NotificationDeduplicated;

        event EventHandler<bool>? DoNotDisturbChanged;
    }
}

