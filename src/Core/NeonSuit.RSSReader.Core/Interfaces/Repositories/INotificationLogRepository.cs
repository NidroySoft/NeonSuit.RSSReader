using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing notification audit logs.
    /// Provides methods for tracking, querying, and analyzing notification delivery and interactions.
    /// </summary>
    public interface INotificationLogRepository
    {
        /// <summary>
        /// Retrieves a notification log entry by its unique identifier.
        /// </summary>
        /// <param name="id">The notification log ID.</param>
        /// <returns>The NotificationLog object or null if not found.</returns>
        Task<NotificationLog?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all notification logs from the database.
        /// </summary>
        /// <returns>A list of all notification logs.</returns>
        Task<List<NotificationLog>> GetAllAsync();

        /// <summary>
        /// Retrieves notification logs for a specific article.
        /// </summary>
        /// <param name="articleId">The article ID.</param>
        /// <returns>List of notification logs for the article.</returns>
        Task<List<NotificationLog>> GetByArticleIdAsync(int articleId);

        /// <summary>
        /// Retrieves notification logs triggered by a specific rule.
        /// </summary>
        /// <param name="ruleId">The rule ID.</param>
        /// <returns>List of notification logs triggered by the rule.</returns>
        Task<List<NotificationLog>> GetByRuleIdAsync(int ruleId);

        /// <summary>
        /// Retrieves notification logs within a specific date range.
        /// </summary>
        /// <param name="startDate">Start date of the range.</param>
        /// <param name="endDate">End date of the range.</param>
        /// <returns>List of notification logs within the date range.</returns>
        Task<List<NotificationLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Retrieves recent notification logs with a specified limit.
        /// </summary>
        /// <param name="limit">Maximum number of logs to return.</param>
        /// <returns>List of recent notification logs.</returns>
        Task<List<NotificationLog>> GetRecentAsync(int limit = 50);

        /// <summary>
        /// Inserts a new notification log entry.
        /// </summary>
        /// <param name="notificationLog">The notification log to insert.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> InsertAsync(NotificationLog notificationLog);

        /// <summary>
        /// Updates an existing notification log entry.
        /// </summary>
        /// <param name="notificationLog">The notification log with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateAsync(NotificationLog notificationLog);

        /// <summary>
        /// Deletes a notification log by its ID.
        /// </summary>
        /// <param name="id">The notification log ID to delete.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteAsync(int id);

        /// <summary>
        /// Records a user action on a notification (click, dismiss, etc.).
        /// </summary>
        /// <param name="notificationId">The notification log ID.</param>
        /// <param name="action">The user action performed.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> RecordActionAsync(int notificationId, NotificationAction action);

        /// <summary>
        /// Gets statistics about notification delivery and interactions.
        /// </summary>
        /// <returns>Notification statistics including totals and click rates.</returns>
        Task<NotificationStatistics> GetStatisticsAsync();

        /// <summary>
        /// Gets notification logs that have not been acted upon by the user.
        /// </summary>
        /// <returns>List of pending notification logs.</returns>
        Task<List<NotificationLog>> GetPendingNotificationsAsync();

        /// <summary>
        /// Cleans up old notification logs beyond the retention period.
        /// </summary>
        /// <param name="retentionDays">Number of days to retain logs.</param>
        /// <returns>Number of logs deleted.</returns>
        Task<int> CleanupOldLogsAsync(int retentionDays = 30);

        /// <summary>
        /// Gets the click-through rate for notifications.
        /// </summary>
        /// <returns>The click-through rate as a percentage.</returns>
        Task<double> GetClickThroughRateAsync();
    }

    /// <summary>
    /// Represents statistics about notification delivery and interactions.
    /// </summary>
    public class NotificationStatistics
    {
        public int TotalNotifications { get; set; }
        public int ClickedNotifications { get; set; }
        public int DismissedNotifications { get; set; }
        public int FailedDeliveries { get; set; }
        public double ClickThroughRate { get; set; }
        public DateTime? FirstNotificationDate { get; set; }
        public DateTime? LastNotificationDate { get; set; }

        /// <summary>
        /// Gets the formatted click-through rate as a percentage string.
        /// </summary>
        public string ClickThroughRateFormatted => $"{ClickThroughRate:F2}%";
    }
}