
namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Lightweight DTO containing key database metrics for monitoring, diagnostics, 
    /// and user-facing statistics dashboard.
    /// </summary>
    public class DatabaseStats
    {
        /// <summary>
        /// Total size of the SQLite database file in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Current number of stored articles.
        /// </summary>
        public int ArticleCount { get; set; }

        /// <summary>
        /// Current number of subscribed feeds.
        /// </summary>
        public int FeedCount { get; set; }

        /// <summary>
        /// Number of active user-defined rules/filters.
        /// </summary>
        public int RuleCount { get; set; }

        /// <summary>
        /// Number of stored notification history/log entries.
        /// </summary>
        public int NotificationCount { get; set; }

        /// <summary>
        /// Date/time of the most recent successful backup (null if never backed up).
        /// </summary>
        public DateTime? LastBackup { get; set; }

        /// <summary>
        /// Returns a human-readable summary of the statistics.
        /// </summary>
        public override string ToString()
        {
            var sizeInMB = TotalSize / (1024.0 * 1024.0);
            var lastBackupStr = LastBackup?.ToString("yyyy-MM-dd HH:mm") ?? "Never";
            return $"Size: {sizeInMB:F2} MB | Articles: {ArticleCount} | Feeds: {FeedCount} | Rules: {RuleCount} | Notifications: {NotificationCount} | Last Backup: {lastBackupStr}";
        }
    }
}