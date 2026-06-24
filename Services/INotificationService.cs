namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ThreadPilot.Models;

    public interface INotificationService
    {
        event EventHandler<NotificationEventArgs>? NotificationShown;

        event EventHandler<NotificationEventArgs>? NotificationDismissed;

        event EventHandler<NotificationActionEventArgs>? NotificationActionClicked;

        IReadOnlyList<NotificationModel> NotificationHistory { get; }

        Task ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Information);

        Task ShowNotificationAsync(NotificationModel notification);

        Task ShowBalloonTipAsync(string title, string message, NotificationType type = NotificationType.Information, int timeoutMs = 3000);

        Task ShowToastNotificationAsync(string title, string message, NotificationType type = NotificationType.Information);

        Task ShowPowerPlanChangeNotificationAsync(string oldPlan, string newPlan, string processName = "");

        Task ShowProcessMonitoringNotificationAsync(string message, bool isEnabled);

        Task ShowCpuAffinityNotificationAsync(string processName, string affinityInfo);

        Task ShowErrorNotificationAsync(string title, string message, Exception? exception = null);

        Task ShowSuccessNotificationAsync(string title, string message);

        Task DismissNotificationAsync(string notificationId);

        Task DismissAllNotificationsAsync();

        Task ClearNotificationHistoryAsync();

        int GetUnreadNotificationCount();

        Task MarkAllNotificationsAsReadAsync();

        bool AreNotificationsEnabled(NotificationType type);

        void UpdateSettings(ApplicationSettingsModel settings);

        Task InitializeAsync();

        void Dispose();
    }

    public class NotificationEventArgs : EventArgs
    {
        public NotificationModel Notification { get; }

        public NotificationEventArgs(NotificationModel notification)
        {
            this.Notification = notification;
        }
    }

    public class NotificationActionEventArgs : EventArgs
    {
        public NotificationModel Notification { get; }

        public string ActionCommand { get; }

        public NotificationActionEventArgs(NotificationModel notification, string actionCommand)
        {
            this.Notification = notification;
            this.ActionCommand = actionCommand;
        }
    }
}

