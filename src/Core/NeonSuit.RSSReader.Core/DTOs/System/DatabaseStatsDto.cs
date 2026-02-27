// =======================================================
// Core/DTOs/System/DatabaseStatsDto.cs
// =======================================================

namespace NeonSuit.RSSReader.Core.DTOs.System
{
    /// <summary>
    /// Data Transfer Object containing key database metrics for monitoring, 
    /// diagnostics, and user-facing statistics dashboards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO aggregates statistics from multiple repositories to provide
    /// a comprehensive view of the application's data footprint and health.
    /// </para>
    /// <para>
    /// Used in:
    /// <list type="bullet">
    /// <item><description>Main dashboard (summary cards)</description></item>
    /// <item><description>Health monitoring endpoints</description></item>
    /// <item><description>Backup status displays</description></item>
    /// <item><description>System diagnostics</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class DatabaseStatsDto
    {
        /// <summary>
        /// Total size of the SQLite database file in bytes.
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Total size in a human-readable format (e.g., "2.5 MB").
        /// </summary>
        public string TotalSizeFormatted => FormatBytes(TotalSize);

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
        /// Number of tags in the system.
        /// </summary>
        public int TagCount { get; set; }

        /// <summary>
        /// Date/time of the most recent successful backup (UTC).
        /// Null if never backed up.
        /// </summary>
        public DateTime? LastBackup { get; set; }

        /// <summary>
        /// Human-readable time since last backup.
        /// </summary>
        public string LastBackupTimeAgo => GetTimeAgo(LastBackup);

        /// <summary>
        /// Average articles per feed.
        /// </summary>
        public double AverageArticlesPerFeed => FeedCount > 0
            ? (double)ArticleCount / FeedCount
            : 0;

        /// <summary>
        /// Returns a human-readable summary of the statistics.
        /// </summary>
        public override string ToString()
        {
            var lastBackupStr = LastBackup?.ToString("yyyy-MM-dd HH:mm") ?? "Never";
            return $"Size: {TotalSizeFormatted} | Articles: {ArticleCount:N0} | Feeds: {FeedCount:N0} | " +
                   $"Rules: {RuleCount:N0} | Notifications: {NotificationCount:N0} | Last Backup: {lastBackupStr}";
        }

        /// <summary>
        /// Formats bytes to human-readable string.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} B"
            };
        }

        /// <summary>
        /// Gets time ago string from a date.
        /// </summary>
        private static string GetTimeAgo(DateTime? date)
        {
            if (!date.HasValue)
                return "Never";

            var diff = DateTime.UtcNow - date.Value;

            return diff.TotalDays switch
            {
                < 1 => "Today",
                < 2 => "Yesterday",
                < 7 => $"{(int)diff.TotalDays} days ago",
                < 30 => $"{(int)(diff.TotalDays / 7)} weeks ago",
                < 365 => $"{(int)(diff.TotalDays / 30)} months ago",
                _ => $"{(int)(diff.TotalDays / 365)} years ago"
            };
        }
    }
}