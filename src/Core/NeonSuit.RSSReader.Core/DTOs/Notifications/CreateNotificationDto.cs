// =======================================================
// Core/DTOs/Notifications/CreateNotificationDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Notifications
{
    /// <summary>
    /// Data Transfer Object for creating a new notification.
    /// Used by the rule engine and manual notification triggers.
    /// </summary>
    public class CreateNotificationDto
    {
        /// <summary>
        /// ID of the article to notify about.
        /// </summary>
        [Required]
        public int ArticleId { get; set; }

        /// <summary>
        /// ID of the rule that triggered this notification (if any).
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Type of notification to send.
        /// </summary>
        public NotificationType NotificationType { get; set; } = NotificationType.Toast;

        /// <summary>
        /// Priority level.
        /// </summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        /// <summary>
        /// Title to display.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message to display.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Channel identifier.
        /// </summary>
        [MaxLength(50)]
        public string Channel { get; set; } = "default";

        /// <summary>
        /// Comma-separated tags for filtering.
        /// </summary>
        [MaxLength(500)]
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Sound to play (optional).
        /// </summary>
        [MaxLength(255)]
        public string? SoundPlayed { get; set; }

        /// <summary>
        /// Display duration in seconds.
        /// </summary>
        [Range(1, 60)]
        public int Duration { get; set; } = 7;
    }
}