// =======================================================
// Core/DTOs/Feeds/FeedSummaryDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.DTOs.Feeds
{
    /// <summary>
    /// Lightweight Data Transfer Object for feed list views.
    /// Used in feed lists, sidebars, and subscription management.
    /// </summary>
    public class FeedSummaryDto
    {
        /// <summary>
        /// Unique identifier of the feed.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Human-readable title of the feed.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed favicon or logo.
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        /// ID of the parent category.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Name of the parent category.
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// Number of unread articles in this feed.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Total number of articles in this feed.
        /// </summary>
        public int TotalArticleCount { get; set; }

        /// <summary>
        /// Whether automatic updates are enabled.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Health status based on failure count.
        /// </summary>
        public FeedHealthStatus HealthStatus { get; set; }

        /// <summary>
        /// Whether this is a podcast feed.
        /// </summary>
        public bool IsPodcastFeed { get; set; }

        /// <summary>
        /// Timestamp of the last update (for UI display).
        /// </summary>
        public string? LastUpdatedDisplay { get; set; }
    }
}