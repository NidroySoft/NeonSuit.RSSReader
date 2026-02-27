// =======================================================
// Core/DTOs/Notifications/NotificationSummaryDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Notifications
{
    /// <summary>
    /// Lightweight Data Transfer Object for notification list views.
    /// Used in notification center, dropdowns, and recent activity lists.
    /// </summary>
    public class NotificationSummaryDto
    {
        /// <summary>
        /// Unique identifier of the notification.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Title shown in the notification.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message body (truncated if needed).
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Priority level (affects visual styling).
        /// </summary>
        public NotificationPriority Priority { get; set; }

        /// <summary>
        /// Timestamp when sent.
        /// </summary>
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Human-readable time ago string.
        /// </summary>
        public string TimeAgo { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user has interacted with this notification.
        /// </summary>
        public bool IsRead => Action != NotificationAction.None;

        /// <summary>
        /// Type of user action taken.
        /// </summary>
        public NotificationAction Action { get; set; }

        /// <summary>
        /// Whether the notification was successfully delivered.
        /// </summary>
        public bool Delivered { get; set; }

        /// <summary>
        /// ID of the related article (for navigation).
        /// </summary>
        public int ArticleId { get; set; }

        /// <summary>
        /// Title of the related article.
        /// </summary>
        public string ArticleTitle { get; set; } = string.Empty;
    }
}