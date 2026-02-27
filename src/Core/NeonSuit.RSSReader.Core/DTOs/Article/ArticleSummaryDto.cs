// =======================================================
// File: Core/DTOs/Articles/ArticleSummaryDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Lightweight Data Transfer Object for article list views (cards, grids, summaries).
    /// Contains only the essential fields needed for displaying article previews.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO is optimized for performance in list views, containing only the fields
    /// necessary for rendering article cards, titles, and basic metadata.
    /// </para>
    /// <para>
    /// Used in:
    /// <list type="bullet">
    /// <item><description>Feed article lists</description></item>
    /// <item><description>Search results</description></item>
    /// <item><description>Tag-filtered views</description></item>
    /// <item><description>Unread/Starred/Favorite collections</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class ArticleSummaryDto
    {
        /// <summary>
        /// Unique identifier of the article.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Article title or headline.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Truncated preview text (max 200 chars) with HTML stripped.
        /// </summary>
        public string Excerpt { get; set; } = string.Empty;

        /// <summary>
        /// Publication date in UTC.
        /// </summary>
        public DateTime? PublishedDate { get; set; }

        /// <summary>
        /// Human-readable time ago string (e.g., "2h", "3d", "just now").
        /// </summary>
        public string TimeAgo { get; set; } = string.Empty;

        /// <summary>
        /// Name of the source feed.
        /// </summary>
        public string FeedTitle { get; set; } = string.Empty;

        /// <summary>
        /// ID of the source feed (for navigation).
        /// </summary>
        public int FeedId { get; set; }

        /// <summary>
        /// URL of the feed's favicon or logo.
        /// </summary>
        public string? FeedIconUrl { get; set; }

        /// <summary>
        /// Whether the article is starred by the user.
        /// </summary>
        public bool IsStarred { get; set; }

        /// <summary>
        /// Whether the article is marked as favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Current read status of the article.
        /// </summary>
        public ArticleStatus Status { get; set; }

        /// <summary>
        /// URL of the featured image (if any).
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Number of tags associated with this article.
        /// </summary>
        public int TagCount { get; set; }

        /// <summary>
        /// Indicates whether the article has media attachments (podcast/video).
        /// </summary>
        public bool HasEnclosure { get; set; }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString() => $"{Title} - {FeedTitle} ({PublishedDate:yyyy-MM-dd})";
    }
}