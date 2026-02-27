// =======================================================
// Core/DTOs/Feeds/UpdateFeedDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Feeds
{
    /// <summary>
    /// Data Transfer Object for updating an existing feed.
    /// All properties are optional to support partial updates.
    /// </summary>
    public class UpdateFeedDto
    {
        /// <summary>
        /// Human-readable title of the feed.
        /// </summary>
        [MaxLength(512)]
        public string? Title { get; set; }

        /// <summary>
        /// Feed description or subtitle.
        /// </summary>
        [MaxLength(2048)]
        public string? Description { get; set; }

        /// <summary>
        /// Website/homepage URL.
        /// </summary>
        [Url]
        [MaxLength(2048)]
        public string? WebsiteUrl { get; set; }

        /// <summary>
        /// URL of the feed favicon or logo.
        /// </summary>
        [Url]
        [MaxLength(2048)]
        public string? IconUrl { get; set; }

        /// <summary>
        /// ID of the parent category.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Desired refresh interval.
        /// </summary>
        public FeedUpdateFrequency? UpdateFrequency { get; set; }

        /// <summary>
        /// Whether automatic updates are enabled.
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Feed-specific article retention days.
        /// </summary>
        [Range(0, 365)]
        public int? ArticleRetentionDays { get; set; }

        /// <summary>
        /// Whether this is a podcast feed.
        /// </summary>
        public bool? IsPodcastFeed { get; set; }

        /// <summary>
        /// Reset failure count to zero.
        /// </summary>
        public bool? ResetFailureCount { get; set; }
    }
}