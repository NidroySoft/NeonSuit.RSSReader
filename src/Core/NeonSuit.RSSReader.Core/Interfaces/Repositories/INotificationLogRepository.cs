using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing notification audit logs.
    /// Provides methods for tracking, querying, and analyzing notification delivery and interactions.
    /// </summary>
    /// <remarks>
    /// This interface defines the contract for data access operations related to <see cref="NotificationLog"/> entities.
    /// It emphasizes efficient data retrieval and modification, suitable for low-resource environments
    /// by promoting server-side filtering and explicit control over data loading.
    /// </remarks>
    public interface INotificationLogRepository : IRepository<NotificationLog>
    {
        #region Read Collection Operations

        /// <summary>
        /// Retrieves notification logs for a specific article.
        /// </summary>
        /// <param name="articleId">The unique identifier of the article.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of notification logs associated with the specified article.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
        Task<List<NotificationLog>> GetByArticleIdAsync(int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves notification logs triggered by a specific rule.
        /// </summary>
        /// <param name="ruleId">The unique identifier of the rule.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of notification logs triggered by the specified rule.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
        Task<List<NotificationLog>> GetByRuleIdAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves notification logs within a specific date range.
        /// </summary>
        /// <param name="startDate">The start date (inclusive) of the desired range.</param>
        /// <param name="endDate">The end date (inclusive) of the desired range.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of notification logs that fall within the specified date range.</returns>
        /// <exception cref="ArgumentException">Thrown if startDate is greater than endDate.</exception>
        Task<List<NotificationLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a limited number of the most recent notification logs.
        /// </summary>
        /// <param name="limit">The maximum number of recent logs to return. Default is 50.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of recent notification logs, ordered by creation date descending.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if limit is less than or equal to 0.</exception>
        Task<List<NotificationLog>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves notification logs that have not yet been acted upon by the user.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending notification logs.</returns>
        Task<List<NotificationLog>> GetPendingNotificationsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Action and Status Updates

        /// <summary>
        /// Records a user action on a notification (click, dismiss, etc.).
        /// </summary>
        /// <param name="notificationId">The unique identifier of the notification log to update.</param>
        /// <param name="action">The <see cref="NotificationAction"/> performed by the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of rows affected, typically 1 if successful.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if notificationId is less than or equal to 0.</exception>
        Task<int> RecordActionAsync(int notificationId, NotificationAction action, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics and Analytics

        /// <summary>
        /// Gets comprehensive statistics about notification delivery and user interactions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="NotificationStatistics"/> object containing various metrics.</returns>
        Task<NotificationStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates the click-through rate (CTR) for notifications.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The click-through rate as a percentage (double), or 0.0 if no notifications exist.</returns>
        Task<double> GetClickThroughRateAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Maintenance Operations

        /// <summary>
        /// Cleans up old notification logs that are beyond the specified retention period.
        /// </summary>
        /// <param name="retentionDays">The number of days to retain logs. Default is 30 days.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of logs deleted during the cleanup operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if retentionDays is negative.</exception>
        Task<int> CleanupOldLogsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);

        #endregion
    }

    /// <summary>
    /// Represents comprehensive statistics about notification delivery and user interactions.
    /// </summary>
    public class NotificationStatistics
    {
        /// <summary>
        /// Total number of notifications recorded.
        /// </summary>
        public int TotalNotifications { get; set; }

        /// <summary>
        /// Count of notifications that were clicked by the user.
        /// </summary>
        public int ClickedNotifications { get; set; }

        /// <summary>
        /// Count of notifications that were explicitly dismissed by the user.
        /// </summary>
        public int DismissedNotifications { get; set; }

        /// <summary>
        /// Count of notifications that failed to be delivered.
        /// </summary>
        public int FailedDeliveries { get; set; }

        /// <summary>
        /// Calculated click-through rate as a percentage.
        /// </summary>
        public double ClickThroughRate { get; set; }

        /// <summary>
        /// Date and time of the earliest recorded notification.
        /// </summary>
        public DateTime? FirstNotificationDate { get; set; }

        /// <summary>
        /// Date and time of the latest recorded notification.
        /// </summary>
        public DateTime? LastNotificationDate { get; set; }

        /// <summary>
        /// Formatted click-through rate as a percentage string.
        /// </summary>
        public string ClickThroughRateFormatted => $"{ClickThroughRate:F2}%";
    }
}