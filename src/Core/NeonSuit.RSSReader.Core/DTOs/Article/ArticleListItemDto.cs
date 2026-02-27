// =======================================================
// File: Core/DTOs/Articles/ArticleListItemDto.cs
// =======================================================

using System;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Ultra-lightweight Data Transfer Object for article list items in virtualized lists.
    /// Contains minimal fields for memory-efficient rendering of large lists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO is optimized for virtualized/listbox scenarios where thousands of items
    /// may be rendered. It contains only the absolute minimum fields needed for display.
    /// </para>
    /// <para>
    /// Used in:
    /// <list type="bullet">
    /// <item><description>Virtualized/infinite scroll lists</description></item>
    /// <item><description>Mobile list views with limited memory</description></item>
    /// <item><description>Performance-critical UI components</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public class ArticleListItemDto
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Article title (truncated if needed).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Publication date (short format).
        /// </summary>
        public DateTime? PublishedDate { get; set; }

        /// <summary>
        /// Name of the source feed.
        /// </summary>
        public string FeedTitle { get; set; } = string.Empty;

        /// <summary>
        /// Whether the article is unread (for visual indicators).
        /// </summary>
        public bool IsUnread { get; set; }

        /// <summary>
        /// Whether the article is starred.
        /// </summary>
        public bool IsStarred { get; set; }

        /// <summary>
        /// Whether the article is favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Whether the article has an image (for layout decisions).
        /// </summary>
        public bool HasImage { get; set; }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString() => Title;
    }
}