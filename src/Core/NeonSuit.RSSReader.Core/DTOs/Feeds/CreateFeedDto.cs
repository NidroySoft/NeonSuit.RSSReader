// =======================================================
// Core/DTOs/Feeds/CreateFeedDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Feeds
{
    /// <summary>
    /// Data Transfer Object for adding a new feed.
    /// </summary>
    public class CreateFeedDto
    {
        /// <summary>
        /// Canonical URL of the feed.
        /// </summary>
        [Required]
        [Url]
        [MaxLength(2048)]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable title of the feed.
        /// </summary>
        [Required]
        [MaxLength(512)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Feed description or subtitle.
        /// </summary>
        [MaxLength(2048)]
        public string? Description { get; set; }

        /// <summary>
        /// Website/homepage URL associated with the feed.
        /// </summary>
        [Url]
        [MaxLength(2048)]
        public string WebsiteUrl { get; set; } = string.Empty;

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
        public FeedUpdateFrequency UpdateFrequency { get; set; } = FeedUpdateFrequency.EveryHour;

        /// <summary>
        /// Feed-specific article retention days.
        /// </summary>
        [Range(0, 365)]
        public int? ArticleRetentionDays { get; set; }

        /// <summary>
        /// Whether this is a podcast feed.
        /// </summary>
        public bool IsPodcastFeed { get; set; }
    }
}