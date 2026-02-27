// =======================================================
// Core/DTOs/Notifications/NotificationDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Notifications
{
    /// <summary>
    /// Data Transfer Object for complete notification information.
    /// Used in notification history views and detail panels.
    /// </summary>
    public class NotificationDto
    {
        /// <summary>
        /// Unique identifier of the notification.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the article that triggered this notification.
        /// </summary>
        public int ArticleId { get; set; }

        /// <summary>
        /// Title of the article (for display).
        /// </summary>
        public string ArticleTitle { get; set; } = string.Empty;

        /// <summary>
        /// ID of the rule that triggered this notification (if any).
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Name of the rule that triggered this notification.
        /// </summary>
        public string? RuleName { get; set; }

        /// <summary>
        /// Type of notification (Toast, Sound, Both, etc.).
        /// </summary>
        public NotificationType NotificationType { get; set; }

        /// <summary>
        /// Priority level of the notification.
        /// </summary>
        public NotificationPriority Priority { get; set; }

        /// <summary>
        /// Title shown in the notification.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message body of the notification.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Channel the notification was sent from.
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Tags associated with this notification.
        /// </summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Sound played (if any).
        /// </summary>
        public string? SoundPlayed { get; set; }

        /// <summary>
        /// Notification display duration in seconds.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Timestamp when the notification was sent.
        /// </summary>
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Human-readable time ago string.
        /// </summary>
        public string TimeAgo { get; set; } = string.Empty;

        /// <summary>
        /// Whether the notification was successfully delivered.
        /// </summary>
        public bool Delivered { get; set; }

        /// <summary>
        /// Error message if delivery failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Timestamp when the user interacted with the notification.
        /// </summary>
        public DateTime? ActionAt { get; set; }

        /// <summary>
        /// Type of user action (clicked, dismissed, snoozed).
        /// </summary>
        public NotificationAction Action { get; set; }

        /// <summary>
        /// Whether the user clicked the notification.
        /// </summary>
        public bool Clicked => Action == NotificationAction.Clicked;

        /// <summary>
        /// Whether the user dismissed the notification.
        /// </summary>
        public bool Dismissed => Action == NotificationAction.Dismissed;

        /// <summary>
        /// Response time in seconds (sent to action).
        /// </summary>
        public double? ResponseTimeSeconds { get; set; }
    }
}