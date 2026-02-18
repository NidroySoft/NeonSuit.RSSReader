using NeonSuit.RSSReader.Core.Enums;
using SQLite;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Audit log for all notifications sent to the user.
    /// Tracks delivery, interaction, and effectiveness.
    /// </summary>
    [Table("NotificationLogs")]
    public class NotificationLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the notified article
        /// </summary>
        [Indexed, NotNull]
        public int ArticleId { get; set; }

        /// <summary>
        /// Foreign key to the rule that triggered the notification
        /// </summary>
        [Indexed]
        public int? RuleId { get; set; }

        /// <summary>
        /// Type of notification (Toast, Sound, Both)
        /// </summary>
        public NotificationType NotificationType { get; set; } = NotificationType.Toast;

        /// <summary>
        /// Priority level of the notification
        /// </summary>
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        /// <summary>
        /// Title shown in the notification
        /// </summary>
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message body of the notification
        /// </summary>
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// When the notification was sent
        /// </summary>
        [Indexed]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the user clicked/dismissed the notification
        /// </summary>
        public DateTime? ActionAt { get; set; }

        /// <summary>
        /// Type of user action (click, dismiss, snooze)
        /// </summary>
        public NotificationAction Action { get; set; } = NotificationAction.None;

        /// <summary>
        /// Channel the notification was sent from
        /// </summary>
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user clicked the notification
        /// </summary>
        public bool Clicked => Action == NotificationAction.Clicked;

        /// <summary>
        /// Whether the notification was successfully delivered
        /// </summary>
        public bool Delivered { get; set; } = true;

        /// <summary>
        /// Error message if delivery failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Notification duration in seconds
        /// </summary>
        public int Duration { get; set; } = 7;

        /// <summary>
        /// Tags associated with this notification
        /// </summary>
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Sound played (if any)
        /// </summary>
        public string? SoundPlayed { get; set; }
               
        /// <summary>
        /// Elapsed time since sent (for UI display)
        /// </summary>
        [Ignore]
        public string TimeAgo => GetTimeAgo();

        private string GetTimeAgo()
        {
            var span = DateTime.UtcNow - SentAt;
            if (span.TotalMinutes < 1) return "Ahora";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h";
            return $"{(int)span.TotalDays}d";
        }
    }
}