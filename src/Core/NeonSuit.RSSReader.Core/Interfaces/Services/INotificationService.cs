using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for notification management and delivery.
    /// Provides comprehensive notification handling with support for different notification types,
    /// priority levels, and user action tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all notification-related functionality including:
    /// <list type="bullet">
    /// <item>Notification creation and delivery based on article events and rules</item>
    /// <item>User action tracking (click, dismiss) for notification analytics</item>
    /// <item>Notification history and pending notification management</item>
    /// <item>Duplicate notification prevention through intelligent checks</item>
    /// <item>Notification cleanup based on retention policy</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// Implementations must ensure:
    /// <list type="bullet">
    /// <item>All operations support cancellation via <see cref="CancellationToken"/></item>
    /// <item>Notifications are stored durably and survive application restarts</item>
    /// <item>Event-driven architecture allows UI to react to new notifications in real-time</item>
    /// <item>Proper logging for audit and debugging purposes</item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface INotificationService
    {
        #region Events

        /// <summary>
        /// Event raised when a new notification is created.
        /// </summary>
        /// <remarks>
        /// UI components should subscribe to this event to display toast notifications
        /// and update notification badges/counters in real-time.
        /// Event arguments contain the notification DTO with all relevant information.
        /// </remarks>
        event EventHandler<NotificationDto>? OnNotificationCreated;

        #endregion

        #region Notification Creation and Delivery

        /// <summary>
        /// Sends a notification for an article, optionally triggered by a specific rule.
        /// </summary>
        /// <param name="createDto">The DTO containing notification creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created notification DTO if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if article ID is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// This method performs the following steps:
        /// <list type="bullet">
        /// <item>Checks for duplicate notifications using <see cref="ShouldNotifyAsync"/></item>
        /// <item>Creates a notification record in the database</item>
        /// <item>Raises the <see cref="OnNotificationCreated"/> event for UI consumption</item>
        /// </list>
        /// </remarks>
        Task<NotificationDto?> SendNotificationAsync(CreateNotificationDto createDto, CancellationToken cancellationToken = default);

        #endregion

        #region Notification Actions

        /// <summary>
        /// Records a user action performed on a notification.
        /// </summary>
        /// <param name="actionDto">The DTO containing action information.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the action was successfully recorded; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="actionDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if notification ID is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// This method updates the notification with action timestamp and type for analytics.
        /// </remarks>
        Task<bool> RecordNotificationActionAsync(UpdateNotificationActionDto actionDto, CancellationToken cancellationToken = default);

        #endregion

        #region Notification Queries

        /// <summary>
        /// Retrieves the most recent notifications.
        /// </summary>
        /// <param name="limit">Maximum number of notifications to return. Default: 20.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of the most recent notification DTOs.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="limit"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<NotificationSummaryDto>> GetRecentNotificationsAsync(int limit = 20, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves notifications that are still pending (not yet acted upon).
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of pending notification summary DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<NotificationSummaryDto>> GetPendingNotificationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all notifications for a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of notification DTOs for the specified article.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<NotificationDto>> GetArticleNotificationHistoryAsync(int articleId, CancellationToken cancellationToken = default);

        #endregion

        #region Notification Management

        /// <summary>
        /// Marks all pending notifications as read.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of notifications marked as read.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans up old notifications based on retention policy.
        /// </summary>
        /// <param name="retentionDays">Number of days to keep notifications. Default: 30.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of notifications deleted.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="retentionDays"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> CleanupOldNotificationsAsync(int retentionDays = 30, CancellationToken cancellationToken = default);

        #endregion

        #region Notification Validation

        /// <summary>
        /// Determines whether a notification should be sent for the specified article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="ruleId">Optional rule ID to check specific rule conditions.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if a notification should be sent; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="articleId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        /// <remarks>
        /// This method checks if a notification for this article was already sent recently
        /// to prevent duplicate notifications.
        /// </remarks>
        Task<bool> ShouldNotifyAsync(int articleId, int? ruleId = null, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Retrieves notification statistics for monitoring and analytics.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="NotificationStatsDto"/> object containing various metrics.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<NotificationStatsDto> GetNotificationStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}