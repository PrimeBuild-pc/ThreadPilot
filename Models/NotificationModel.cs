namespace ThreadPilot.Models
{
    using System;
    using CommunityToolkit.Mvvm.ComponentModel;

    public partial class NotificationModel : ObservableObject
    {
        [ObservableProperty]
        private string id = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private NotificationType type = NotificationType.Information;

        [ObservableProperty]
        private DateTime timestamp = DateTime.Now;

        [ObservableProperty]
        private int durationMs = 3000;

        [ObservableProperty]
        private bool isRead = false;

        [ObservableProperty]
        private bool isPersistent = false;

        [ObservableProperty]
        private string? actionText;

        [ObservableProperty]
        private string? actionCommand;

        [ObservableProperty]
        private string? iconPath;

        [ObservableProperty]
        private NotificationPriority priority = NotificationPriority.Normal;

        [ObservableProperty]
        private string? category;

        [ObservableProperty]
        private string? sourceService;

        public NotificationModel()
        {
        }

        public NotificationModel(string title, string message, NotificationType type = NotificationType.Information)
        {
            this.Title = title;
            this.Message = message;
            this.Type = type;
        }

        public NotificationModel(string title, string message, NotificationType type, int durationMs, bool isPersistent = false)
        {
            this.Title = title;
            this.Message = message;
            this.Type = type;
            this.DurationMs = durationMs;
            this.IsPersistent = isPersistent;
        }

        public void MarkAsRead()
        {
            this.IsRead = true;
        }

        public string TypeDisplayText => this.Type switch
        {
            NotificationType.Information => "Info",
            NotificationType.Success => "Success",
            NotificationType.Warning => "Warning",
            NotificationType.Error => "Error",
            NotificationType.PowerPlanChange => "Power Plan",
            NotificationType.ProcessMonitoring => "Process Monitor",
            NotificationType.CpuAffinity => "CPU Affinity",
            _ => "Unknown",
        };

        public string FormattedTimestamp => this.Timestamp.ToString("HH:mm:ss");

        public string FormattedDateTime => this.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public enum NotificationType
    {
        Information,
        Success,
        Warning,
        Error,
        PowerPlanChange,
        ProcessMonitoring,
        CpuAffinity,
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Critical,
    }
}

