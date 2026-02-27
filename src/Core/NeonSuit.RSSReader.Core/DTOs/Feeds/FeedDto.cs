// =======================================================
// Core/DTOs/Feeds/FeedDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Feeds
{
    /// <summary>
    /// Data Transfer Object for complete feed information.
    /// Used for feed details view and editing.
    /// </summary>
    public class FeedDto
    {
        /// <summary>
        /// Unique identifier of the feed.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Canonical URL of the feed.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable title of the feed.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Feed description or subtitle.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Website/homepage URL associated with the feed.
        /// </summary>
        public string WebsiteUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed favicon or logo.
        /// </summary>
        public string? IconUrl { get; set; }

        /// <summary>
        /// Detected or declared language code.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// ID of the parent category (null if uncategorized).
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Name of the parent category.
        /// </summary>
        public string? CategoryName { get; set; }

        /// <summary>
        /// Desired refresh interval for automatic updates.
        /// </summary>
        public FeedUpdateFrequency UpdateFrequency { get; set; }

        /// <summary>
        /// Timestamp of the last successful update.
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Next scheduled update time.
        /// </summary>
        public DateTime? NextUpdateSchedule { get; set; }

        /// <summary>
        /// Whether automatic updates are enabled.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Number of consecutive failed updates.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Last error message from failed update.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Feed-specific article retention days.
        /// </summary>
        public int? ArticleRetentionDays { get; set; }

        /// <summary>
        /// Effective retention days (feed-specific or global default).
        /// </summary>
        public int EffectiveRetentionDays { get; set; }

        /// <summary>
        /// Total number of articles in this feed.
        /// </summary>
        public int TotalArticleCount { get; set; }

        /// <summary>
        /// Number of unread articles in this feed.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Health status based on failure count.
        /// </summary>
        public FeedHealthStatus HealthStatus { get; set; }

        /// <summary>
        /// Whether this is a podcast feed.
        /// </summary>
        public bool IsPodcastFeed { get; set; }

        /// <summary>
        /// Timestamp when the feed was added.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}