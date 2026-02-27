// =======================================================
// File: Core/DTOs/Articles/ArticleDetailDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Comprehensive Data Transfer Object for article detail views.
    /// Contains all metadata, content, and user interaction state for full article display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO is used when displaying a single article in detail view, including:
    /// full content, author information, media attachments, and all user interaction states.
    /// </para>
    /// <para>
    /// Performance note: Content field can be large; consider lazy loading or streaming
    /// for very long articles on memory-constrained devices.
    /// </para>
    /// </remarks>
    public class ArticleDetailDto
    {
        /// <summary>
        /// Unique identifier of the article.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Feed-provided globally unique identifier.
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Article title or headline.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Canonical URL to the original article.
        /// </summary>
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Full article content (HTML or plain text).
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Short summary or description.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Author name(s).
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Publication date in UTC.
        /// </summary>
        public DateTime? PublishedDate { get; set; }

        /// <summary>
        /// Date when the article was added to the database.
        /// </summary>
        public DateTime AddedDate { get; set; }

        /// <summary>
        /// ID of the source feed.
        /// </summary>
        public int FeedId { get; set; }

        /// <summary>
        /// Name of the source feed.
        /// </summary>
        public string FeedTitle { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed's website.
        /// </summary>
        public string FeedWebsiteUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed's favicon or logo.
        /// </summary>
        public string? FeedIconUrl { get; set; }

        /// <summary>
        /// Current read status.
        /// </summary>
        public ArticleStatus Status { get; set; }

        /// <summary>
        /// Whether the article is starred.
        /// </summary>
        public bool IsStarred { get; set; }

        /// <summary>
        /// Whether the article is marked as favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Reading progress percentage (0-100).
        /// </summary>
        public int ReadPercentage { get; set; }

        /// <summary>
        /// Last time the user interacted with this article.
        /// </summary>
        public DateTime? LastReadAt { get; set; }

        /// <summary>
        /// List of tag names associated with this article.
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// URL of the featured image.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Comma-separated categories from the feed.
        /// </summary>
        public string? Categories { get; set; }

        /// <summary>
        /// Language code of the article content.
        /// </summary>
        public string? Language { get; set; }

        #region Enclosure (Podcast/Media)

        /// <summary>
        /// URL of the media enclosure.
        /// </summary>
        public string? EnclosureUrl { get; set; }

        /// <summary>
        /// MIME type of the enclosure.
        /// </summary>
        public string? EnclosureType { get; set; }

        /// <summary>
        /// Size of the enclosure in bytes.
        /// </summary>
        public long? EnclosureLength { get; set; }

        /// <summary>
        /// Indicates whether this article has a media enclosure.
        /// </summary>
        public bool HasEnclosure => !string.IsNullOrEmpty(EnclosureUrl);

        /// <summary>
        /// Indicates whether this enclosure is an audio file (podcast).
        /// </summary>
        public bool IsAudio => EnclosureType?.StartsWith("audio/") == true;

        /// <summary>
        /// Indicates whether this enclosure is a video file.
        /// </summary>
        public bool IsVideo => EnclosureType?.StartsWith("video/") == true;

        #endregion

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString() => $"{Title} - {FeedTitle} ({PublishedDate:yyyy-MM-dd})";
    }
}