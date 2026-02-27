using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Audit log for all notifications sent to the user.
    /// Tracks delivery, interaction, and effectiveness metrics for analytics and debugging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This entity records every notification sent to the user, including:
    /// <list type="bullet">
    /// <item><description>Which article triggered the notification</description></item>
    /// <item><description>Which rule (if any) generated it</description></item>
    /// <item><description>Delivery status and any errors</description></item>
    /// <item><description>User interaction (clicked, dismissed, snoozed)</description></item>
    /// <item><description>Performance metrics (duration, timing)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Used for:
    /// <list type="bullet">
    /// <item><description>Notification history UI</description></item>
    /// <item><description>Analytics on notification effectiveness</description></item>
    /// <item><description>Debugging failed notifications</description></item>
    /// <item><description>Preventing duplicate notifications</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Performance optimizations:
    /// <list type="bullet">
    /// <item><description>Indexed on ArticleId for quick history lookups</description></item>
    /// <item><description>Indexed on RuleId for rule effectiveness analysis</description></item>
    /// <item><description>Indexed on SentAt for time-based queries and cleanup</description></item>
    /// <item><description>MaxLength constraints on text fields to optimize storage</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [Table("NotificationLogs")]
    public class NotificationLog
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationLog"/> class.
        /// Sets default values for timestamps and delivery status.
        /// </summary>
        public NotificationLog()
        {
            SentAt = DateTime.UtcNow;
            NotificationType = NotificationType.Toast;
            Priority = NotificationPriority.Normal;
            Action = NotificationAction.None;
            Delivered = true;
            Duration = 7;
            Channel = "default";
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Foreign Keys

        /// <summary>
        /// Foreign key to the notified article.
        /// </summary>
        [Required]
        public int ArticleId { get; set; }

        /// <summary>
        /// Foreign key to the rule that triggered the notification.
        /// Null if notification was triggered manually or by system.
        /// </summary>
        public int? RuleId { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Navigation property to the associated article.
        /// </summary>
        [ForeignKey(nameof(ArticleId))]
        public virtual Article? Article { get; set; }

        /// <summary>
        /// Navigation property to the rule that triggered this notification.
        /// </summary>
        [ForeignKey(nameof(RuleId))]
        public virtual Rule? Rule { get; set; }

        #endregion

        #region Notification Content

        /// <summary>
        /// Type of notification (Toast, Sound, Both, Email, etc.).
        /// </summary>
        public NotificationType NotificationType { get; set; }

        /// <summary>
        /// Priority level of the notification (affects display duration and behavior).
        /// </summary>
        public NotificationPriority Priority { get; set; }

        /// <summary>
        /// Title shown in the notification.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Message body of the notification.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Channel the notification was sent from (e.g., "system", "rule", "manual").
        /// </summary>
        [MaxLength(50)]
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Tags associated with this notification for filtering and categorization.
        /// Stored as comma-separated values.
        /// </summary>
        [MaxLength(500)]
        public string Tags { get; set; } = string.Empty;

        /// <summary>
        /// Sound played (if any). Null means default system sound.
        /// </summary>
        [MaxLength(255)]
        public string? SoundPlayed { get; set; }

        /// <summary>
        /// Notification display duration in seconds.
        /// </summary>
        public int Duration { get; set; }

        #endregion

        #region Delivery Status

        /// <summary>
        /// Timestamp when the notification was sent (UTC).
        /// </summary>
        public DateTime SentAt { get; set; }

        /// <summary>
        /// Whether the notification was successfully delivered.
        /// </summary>
        public bool Delivered { get; set; }

        /// <summary>
        /// Error message if delivery failed.
        /// </summary>
        [MaxLength(500)]
        public string? Error { get; set; }

        #endregion

        #region User Interaction

        /// <summary>
        /// Timestamp when the user interacted with the notification (clicked, dismissed, etc.).
        /// </summary>
        public DateTime? ActionAt { get; set; }

        /// <summary>
        /// Type of user action (clicked, dismissed, snoozed, etc.).
        /// </summary>
        public NotificationAction Action { get; set; }

        #endregion

        #region Computed Properties (Not Mapped)

        /// <summary>
        /// Indicates whether the user clicked the notification.
        /// </summary>
        [NotMapped]
        public bool Clicked => Action == NotificationAction.Clicked;

        /// <summary>
        /// Indicates whether the user dismissed the notification without interacting.
        /// </summary>
        [NotMapped]
        public bool Dismissed => Action == NotificationAction.Dismissed;

        /// <summary>
        /// Indicates whether the user snoozed the notification for later.
        /// </summary>
        [NotMapped]
        public bool Snoozed => Action == NotificationAction.Snoozed;

        /// <summary>
        /// Human-readable time ago string for UI display (e.g., "5m", "2h", "3d").
        /// </summary>
        [NotMapped]
        public string TimeAgo => GetTimeAgo();

        /// <summary>
        /// Response time in seconds (time between sent and user action).
        /// </summary>
        [NotMapped]
        public double? ResponseTimeSeconds => ActionAt.HasValue
            ? (ActionAt.Value - SentAt).TotalSeconds
            : null;

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Calculates a human-readable time ago string.
        /// </summary>
        /// <returns>Formatted string like "5m", "2h", "3d", or "Ahora".</returns>
        private string GetTimeAgo()
        {
            var span = DateTime.UtcNow - SentAt;

            return span.TotalMinutes switch
            {
                < 1 => "Ahora",
                < 60 => $"{(int)span.TotalMinutes}m",
                < 1440 => $"{(int)span.TotalHours}h",
                _ => $"{(int)span.TotalDays}d"
            };
        }

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on (ArticleId, SentAt DESC) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(n => new { n.ArticleId, n.SentAt }).IsDescending();
// Why (benefit): Faster "notification history for article" queries (most recent first)
// Estimated effort: 20–30 min
// Risk level: Low
// Potential impact: Improves UI performance when viewing article notification history

// TODO (High - v1.x): Add composite index on (RuleId, SentAt DESC) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(n => new { n.RuleId, n.SentAt }).IsDescending();
// Why (benefit): Faster rule effectiveness analysis and "recent notifications by rule" queries
// Estimated effort: 20–30 min
// Risk level: Low
// Potential impact: Improves rule analytics dashboard performance

// TODO (Medium - v1.x): Add automatic cleanup job for old notification logs
// What to do: Implement background service that deletes notifications older than configured retention (default 90 days)
// Why (benefit): Prevent unlimited growth of notification log table
// Estimated effort: 1 day
// Risk level: Low (delete-only operation)
// Potential impact: Maintains database size under control

// TODO (Medium - v1.x): Add notification grouping metadata
// What to do: Add GroupId (Guid) and IsGroupSummary (bool) fields
// Why (benefit): Support grouped notifications (e.g., "5 new articles from The Verge")
// Estimated effort: 4–6 hours
// Risk level: Low
// Potential impact: Better UX for bulk notifications

// TODO (Low - v1.x): Add device/platform information
// What to do: Add string? DeviceId; string? Platform; (Windows, Android, iOS)
// Why (benefit): Track notification delivery across multiple devices
// Estimated effort: 2–3 hours
// Risk level: Low
// Potential impact: Multi-device analytics and debugging

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent lost updates if notification status modified concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in concurrent scenarios (rare for logs)