/*
 * ThreadPilot - Advanced Windows Process and Power Plan Manager
 * Copyright (C) 2025 Prime Build
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, version 3 only.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using ThreadPilot.Models;

    /// <summary>
    /// Implementation of smart notification service with throttling and priority queuing.
    /// </summary>
    public class SmartNotificationService : ISmartNotificationService, IDisposable
    {
        private readonly ILogger<SmartNotificationService> logger;
        private readonly INotificationService baseNotificationService;
        private readonly ConcurrentQueue<SmartNotification> notificationQueue = new();
        private readonly ConcurrentDictionary<string, SmartNotification> scheduledNotifications = new();
        private readonly ConcurrentDictionary<string, DateTime> lastNotificationTimes = new();
        private readonly ConcurrentDictionary<string, List<DateTime>> notificationHistory = new();
        private readonly List<SmartNotification> sentNotifications = new();
        private readonly System.Threading.Timer processingTimer;
        private readonly System.Threading.Timer cleanupTimer;
        private readonly SemaphoreSlim processingLock = new(1, 1);

        private NotificationPreferences preferences = new();
        private DateTime? doNotDisturbUntil;
        private bool disposed;

        public event EventHandler<SmartNotificationEventArgs>? NotificationSent;

        public event EventHandler<SmartNotificationEventArgs>? NotificationThrottled;

        public event EventHandler<SmartNotificationEventArgs>? NotificationDeduplicated;

        public event EventHandler<bool>? DoNotDisturbChanged;

        public SmartNotificationService(
            ILogger<SmartNotificationService> logger,
            INotificationService baseNotificationService)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.baseNotificationService = baseNotificationService ?? throw new ArgumentNullException(nameof(baseNotificationService));

            // Set up processing timer (process queue every 2 seconds)
            this.processingTimer = new System.Threading.Timer(this.ProcessQueueCallback, null,
                TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            // Set up cleanup timer (clean history every hour)
            this.cleanupTimer = new System.Threading.Timer(this.CleanupCallback, null,
                TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        public async Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing SmartNotificationService");

            // Initialize default preferences
            this.preferences = this.CreateDefaultPreferences();

            // Load preferences from storage (simplified)
            await this.LoadPreferencesAsync();
        }

        public async Task<bool> SendNotificationAsync(SmartNotification notification)
        {
            try
            {
                // Validate notification
                if (string.IsNullOrWhiteSpace(notification.Title) && string.IsNullOrWhiteSpace(notification.Message))
                {
                    this.logger.LogWarning("Attempted to send notification with empty title and message");
                    return false;
                }

                // Check if notifications are enabled
                if (!this.preferences.IsEnabled)
                {
                    this.logger.LogDebug("Notifications are disabled, skipping notification: {Title}", notification.Title);
                    return false;
                }

                // Check category preferences
                if (this.preferences.CategoryEnabled.TryGetValue(notification.Category, out var categoryEnabled) && !categoryEnabled)
                {
                    this.logger.LogDebug(
                        "Category {Category} is disabled, skipping notification: {Title}",
                        notification.Category, notification.Title);
                    return false;
                }

                // Check minimum priority
                if (notification.Priority < this.preferences.MinimumPriority)
                {
                    this.logger.LogDebug(
                        "Notification priority {Priority} below minimum {MinPriority}, skipping: {Title}",
                        notification.Priority, this.preferences.MinimumPriority, notification.Title);
                    return false;
                }

                // Check Do Not Disturb mode
                if (this.IsDoNotDisturbActive() && notification.Priority < NotificationPriority.Critical)
                {
                    this.logger.LogDebug("Do Not Disturb is active, skipping non-critical notification: {Title}", notification.Title);
                    return false;
                }

                // Check throttling
                if (this.IsThrottled(notification))
                {
                    this.NotificationThrottled?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Throttled due to rate limiting",
                    });
                    return false;
                }

                // Check deduplication
                if (this.IsDuplicate(notification))
                {
                    this.NotificationDeduplicated?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Deduplicated - similar notification recently sent",
                    });
                    return false;
                }

                // Add to queue for processing
                this.notificationQueue.Enqueue(notification);
                this.logger.LogDebug(
                    "Queued notification: {Title} (Priority: {Priority}, Category: {Category})",
                    notification.Title, notification.Priority, notification.Category);

                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error sending notification: {Title}", notification.Title);
                return false;
            }
        }

        public async Task<bool> SendNotificationAsync(string title, string message,
            NotificationPriority priority = NotificationPriority.Normal,
            NotificationCategory category = NotificationCategory.Information)
        {
            var notification = new SmartNotification
            {
                Title = title,
                Message = message,
                Priority = priority,
                Category = category,
                DeduplicationKey = $"{category}:{title}:{message}".GetHashCode().ToString(),
            };

            return await this.SendNotificationAsync(notification);
        }

        public async Task<bool> ScheduleNotificationAsync(SmartNotification notification, DateTime deliveryTime)
        {
            notification.ScheduledFor = deliveryTime;
            this.scheduledNotifications.TryAdd(notification.Id, notification);

            this.logger.LogDebug(
                "Scheduled notification {Id} for delivery at {DeliveryTime}",
                notification.Id, deliveryTime);

            return true;
        }

        public async Task<bool> CancelNotificationAsync(string notificationId)
        {
            var removed = this.scheduledNotifications.TryRemove(notificationId, out var notification);
            if (removed)
            {
                this.logger.LogDebug("Cancelled scheduled notification: {Id}", notificationId);
            }
            return removed;
        }

        public async Task<List<SmartNotification>> GetPendingNotificationsAsync()
        {
            var pending = new List<SmartNotification>();

            // Add queued notifications
            pending.AddRange(this.notificationQueue.ToArray());

            // Add scheduled notifications
            pending.AddRange(this.scheduledNotifications.Values);

            return pending.OrderByDescending(n => n.Priority).ThenBy(n => n.CreatedAt).ToList();
        }

        public async Task<List<SmartNotification>> GetNotificationHistoryAsync(TimeSpan? period = null)
        {
            var cutoff = period.HasValue ? DateTime.UtcNow - period.Value : DateTime.MinValue;

            lock (this.sentNotifications)
            {
                return this.sentNotifications
                    .Where(n => n.CreatedAt >= cutoff)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }
        }

        public async Task ClearHistoryAsync()
        {
            lock (this.sentNotifications)
            {
                this.sentNotifications.Clear();
            }

            this.notificationHistory.Clear();
            this.logger.LogInformation("Cleared notification history");
        }

        public async Task UpdatePreferencesAsync(NotificationPreferences preferences)
        {
            this.preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            await this.SavePreferencesAsync();
            this.logger.LogInformation("Updated notification preferences");
        }

        public async Task<NotificationPreferences> GetPreferencesAsync()
        {
            return this.preferences;
        }

        public async Task SetDoNotDisturbAsync(bool enabled, TimeSpan? duration = null)
        {
            var wasActive = this.IsDoNotDisturbActive();

            if (enabled)
            {
                this.doNotDisturbUntil = duration.HasValue
                    ? DateTime.UtcNow + duration.Value
                    : DateTime.MaxValue;
                this.preferences.DoNotDisturbMode = true;
            }
            else
            {
                this.doNotDisturbUntil = null;
                this.preferences.DoNotDisturbMode = false;
            }

            var isActive = this.IsDoNotDisturbActive();
            if (wasActive != isActive)
            {
                this.DoNotDisturbChanged?.Invoke(this, isActive);
                this.logger.LogInformation("Do Not Disturb mode {Status}", isActive ? "enabled" : "disabled");
            }
        }

        public bool IsDoNotDisturbActive()
        {
            if (!this.preferences.DoNotDisturbMode)
            {
                return false;
            }

            if (this.doNotDisturbUntil.HasValue && DateTime.UtcNow > this.doNotDisturbUntil.Value)
            {
                this.preferences.DoNotDisturbMode = false;
                this.doNotDisturbUntil = null;
                return false;
            }

            // Check time-based DND
            var now = DateTime.Now.TimeOfDay;
            if (this.preferences.DoNotDisturbStart < this.preferences.DoNotDisturbEnd)
            {
                // Same day range (e.g., 10 PM to 8 AM next day)
                return now >= this.preferences.DoNotDisturbStart || now <= this.preferences.DoNotDisturbEnd;
            }
            else
            {
                // Cross-midnight range (e.g., 10 PM to 8 AM)
                return now >= this.preferences.DoNotDisturbStart && now <= this.preferences.DoNotDisturbEnd;
            }
        }

        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();

            lock (this.sentNotifications)
            {
                var last24Hours = this.sentNotifications.Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-1)).ToList();
                var lastWeek = this.sentNotifications.Where(n => n.CreatedAt >= DateTime.UtcNow.AddDays(-7)).ToList();

                stats["TotalSent"] = this.sentNotifications.Count;
                stats["SentLast24Hours"] = last24Hours.Count;
                stats["SentLastWeek"] = lastWeek.Count;
                stats["PendingCount"] = this.notificationQueue.Count;
                stats["ScheduledCount"] = this.scheduledNotifications.Count;

                // Category breakdown
                var categoryStats = this.sentNotifications
                    .GroupBy(n => n.Category)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                stats["ByCategory"] = categoryStats;

                // Priority breakdown
                var priorityStats = this.sentNotifications
                    .GroupBy(n => n.Priority)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count());
                stats["ByPriority"] = priorityStats;
            }

            return stats;
        }

        public async Task<bool> TestNotificationAsync()
        {
            var testNotification = new SmartNotification
            {
                Title = "Test Notification",
                Message = "This is a test notification from ThreadPilot Smart Notification System",
                Priority = NotificationPriority.Normal,
                Category = NotificationCategory.System,
            };

            return await this.SendNotificationAsync(testNotification);
        }

        private void ProcessQueueCallback(object? state)
        {
            TaskSafety.FireAndForget(this.ProcessQueueCallbackAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Error during notification queue processing");
            });
        }

        private async Task ProcessQueueCallbackAsync()
        {
            if (this.disposed)
            {
                return;
            }

            await this.processingLock.WaitAsync();
            try
            {
                var processedCount = 0;
                var maxProcessPerCycle = 10;

                while (this.notificationQueue.TryDequeue(out var notification) && processedCount < maxProcessPerCycle)
                {
                    await this.ProcessNotificationAsync(notification);
                    processedCount++;
                }

                // Process scheduled notifications
                var now = DateTime.UtcNow;
                var dueNotifications = this.scheduledNotifications.Values
                    .Where(n => n.ScheduledFor <= now)
                    .ToList();

                foreach (var notification in dueNotifications)
                {
                    this.scheduledNotifications.TryRemove(notification.Id, out _);
                    await this.ProcessNotificationAsync(notification);
                }
            }
            finally
            {
                this.processingLock.Release();
            }
        }

        private async Task ProcessNotificationAsync(SmartNotification notification)
        {
            try
            {
                // Check if notification has expired
                if (notification.ExpiresAfter.HasValue &&
                    DateTime.UtcNow - notification.CreatedAt > notification.ExpiresAfter.Value)
                {
                    this.logger.LogDebug("Notification expired: {Title}", notification.Title);
                    return;
                }

                // Send through base notification service
                await this.baseNotificationService.ShowNotificationAsync(
                    notification.Title,
                    notification.Message,
                    this.ConvertToNotificationType(notification.Priority));

                // Assume success since no exception was thrown
                var success = true;

                if (success)
                {
                    // Record successful delivery
                    this.RecordNotificationSent(notification);

                    this.NotificationSent?.Invoke(this, new SmartNotificationEventArgs
                    {
                        Notification = notification,
                        Reason = "Successfully delivered",
                    });

                    this.logger.LogDebug("Successfully sent notification: {Title}", notification.Title);
                }
                else if (notification.RetryCount < notification.MaxRetries)
                {
                    // Retry failed notification
                    notification.RetryCount++;
                    this.notificationQueue.Enqueue(notification);
                    this.logger.LogDebug(
                        "Retrying notification: {Title} (Attempt {Retry}/{Max})",
                        notification.Title, notification.RetryCount, notification.MaxRetries);
                }
                else
                {
                    this.logger.LogWarning(
                        "Failed to send notification after {MaxRetries} attempts: {Title}",
                        notification.MaxRetries, notification.Title);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error processing notification: {Title}", notification.Title);
            }
        }

        private bool IsThrottled(SmartNotification notification)
        {
            if (!this.preferences.ThrottleConfigs.TryGetValue(notification.Category, out var config))
            {
                return false; // No throttling configured for this category
            }

            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            var now = DateTime.UtcNow;

            // Check minimum interval
            if (this.lastNotificationTimes.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < config.MinInterval)
                {
                    return true;
                }
            }

            // Check hourly and daily limits
            if (!this.notificationHistory.TryGetValue(key, out var history))
            {
                history = new List<DateTime>();
                this.notificationHistory[key] = history;
            }

            // Clean old entries
            var oneHourAgo = now.AddHours(-1);
            var oneDayAgo = now.AddDays(-1);
            history.RemoveAll(t => t < oneDayAgo);

            var hourlyCount = history.Count(t => t >= oneHourAgo);
            var dailyCount = history.Count;

            return hourlyCount >= config.MaxPerHour || dailyCount >= config.MaxPerDay;
        }

        private bool IsDuplicate(SmartNotification notification)
        {
            if (string.IsNullOrEmpty(notification.DeduplicationKey))
            {
                return false;
            }

            if (!this.preferences.ThrottleConfigs.TryGetValue(notification.Category, out var config) ||
                !config.EnableDeduplication)
            {
                return false;
            }

            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            if (this.lastNotificationTimes.TryGetValue(key, out var lastTime))
            {
                return DateTime.UtcNow - lastTime < config.DeduplicationWindow;
            }

            return false;
        }

        private void RecordNotificationSent(SmartNotification notification)
        {
            var key = $"{notification.Category}:{notification.DeduplicationKey}";
            var now = DateTime.UtcNow;

            this.lastNotificationTimes[key] = now;

            if (!this.notificationHistory.TryGetValue(key, out var history))
            {
                history = new List<DateTime>();
                this.notificationHistory[key] = history;
            }
            history.Add(now);

            lock (this.sentNotifications)
            {
                this.sentNotifications.Add(notification);

                // Keep only last 1000 notifications in memory
                if (this.sentNotifications.Count > 1000)
                {
                    this.sentNotifications.RemoveRange(0, this.sentNotifications.Count - 1000);
                }
            }
        }

        private NotificationType ConvertToNotificationType(NotificationPriority priority)
        {
            return priority switch
            {
                NotificationPriority.Critical => NotificationType.Error,
                NotificationPriority.High => NotificationType.Warning,
                NotificationPriority.Normal => NotificationType.Information,
                NotificationPriority.Low => NotificationType.Information,
                _ => NotificationType.Information,
            };
        }

        private NotificationPreferences CreateDefaultPreferences()
        {
            var preferences = new NotificationPreferences();

            // Enable all categories by default
            foreach (NotificationCategory category in Enum.GetValues<NotificationCategory>())
            {
                preferences.CategoryEnabled[category] = true;
                preferences.ThrottleConfigs[category] = new NotificationThrottleConfig
                {
                    Category = category,
                    MinInterval = TimeSpan.FromSeconds(30),
                    MaxPerHour = category == NotificationCategory.Error ? 20 : 10,
                    MaxPerDay = category == NotificationCategory.Error ? 100 : 50,
                };
            }

            return preferences;
        }

        private Task LoadPreferencesAsync()
        {
            // Simplified - would load from actual storage
            this.logger.LogDebug("Loaded notification preferences");
            return Task.CompletedTask;
        }

        private Task SavePreferencesAsync()
        {
            // Simplified - would save to actual storage
            this.logger.LogDebug("Saved notification preferences");
            return Task.CompletedTask;
        }

        private void CleanupCallback(object? state)
        {
            TaskSafety.FireAndForget(this.CleanupCallbackAsync(), ex =>
            {
                this.logger.LogWarning(ex, "Error during notification cleanup");
            });
        }

        private async Task CleanupCallbackAsync()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-7);

                // Clean notification history
                var keysToRemove = new List<string>();
                foreach (var kvp in this.notificationHistory)
                {
                    kvp.Value.RemoveAll(t => t < cutoff);
                    if (!kvp.Value.Any())
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    this.notificationHistory.TryRemove(key, out _);
                }

                this.logger.LogDebug("Cleaned up notification history, removed {Count} empty entries", keysToRemove.Count);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Error during notification cleanup");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.processingTimer?.Dispose();
                    this.cleanupTimer?.Dispose();
                    this.processingLock?.Dispose();
                    this.logger.LogInformation("SmartNotificationService disposed");
                }
                this.disposed = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

