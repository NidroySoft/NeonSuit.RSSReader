using SQLite;
using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a subscribed RSS/Atom feed.
    /// </summary>
    public class Feed
    {
        public Feed()
        {
            LastUpdated = DateTime.MinValue;
            CreatedAt = DateTime.UtcNow;
            UnreadCount = 0;
            FailureCount = 0;
            ArticleRetentionDays = 30; // Por defecto: 30 días
        }

        public virtual Category? Category { get; set; }
        public virtual ICollection<Article> Articles { get; set; } = new List<Article>();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// URL of the RSS/Atom feed.
        /// </summary>
        [Unique, NotNull]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Title of the feed.
        /// </summary>
        [NotNull]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Description of the feed.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// URL of the associated website.
        /// </summary>
        [NotNull]
        public string WebsiteUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed's favicon icon.
        /// </summary>
        public string? IconUrl { get; set; } = string.Empty;

        /// <summary>
        /// ID of the category it belongs to (FK).
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Update frequency.
        /// </summary>
        public FeedUpdateFrequency UpdateFrequency { get; set; } = FeedUpdateFrequency.EveryHour;

        /// <summary>
        /// Last successful update date.
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// When to schedule the next update attempt.
        /// </summary>
        [Indexed]
        public DateTime? NextUpdateSchedule { get; set; }

        /// <summary>
        /// Creation date of the subscription.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Whether the feed is active or paused.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Number of consecutive update failures.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Last error message received during update.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Number of days to keep articles before auto-delete.
        /// Null = keep forever, 0 = use global setting.
        /// </summary>
        public int? ArticleRetentionDays { get; set; }

        /// <summary>
        /// Last time this feed was fully synchronized.
        /// </summary>
        public DateTime? LastFullSync { get; set; }

        /// <summary>
        /// Number of articles in this feed (cached).
        /// </summary>
        public int TotalArticleCount { get; set; }

        /// <summary>
        /// Number of unread articles (calculated).
        /// </summary>
        [Ignore]
        public int UnreadCount { get; set; }

        /// <summary>
        /// Whether the feed is currently being updated.
        /// </summary>
        [Ignore]
        public bool IsUpdating { get; set; }

        /// <summary>
        /// Language of the feed.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Health status based on failure count.
        /// </summary>
        [Ignore]
        public FeedHealthStatus HealthStatus
        {
            get
            {
                if (FailureCount == 0) return FeedHealthStatus.Healthy;
                if (FailureCount <= 3) return FeedHealthStatus.Warning;
                return FeedHealthStatus.Error;
            }
        }

        /// <summary>
        /// Returns the effective retention days (feed-specific or global default).
        /// </summary>
        [Ignore]
        public int EffectiveRetentionDays => ArticleRetentionDays ?? 30;
    }  
}