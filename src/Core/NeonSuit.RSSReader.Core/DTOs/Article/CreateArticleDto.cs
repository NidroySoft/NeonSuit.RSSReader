// =======================================================
// File: Core/DTOs/Articles/CreateArticleDto.cs
// =======================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Data Transfer Object for creating a new article.
    /// Used when importing articles from feeds or manually adding articles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO contains all fields that can be set during article creation.
    /// Server-generated fields (Id, AddedDate, ContentHash, etc.) are not included.
    /// </para>
    /// <para>
    /// Validation attributes ensure data integrity before reaching the database.
    /// </para>
    /// </remarks>
    public class CreateArticleDto
    {
        /// <summary>
        /// Feed-provided globally unique identifier.
        /// </summary>
        [Required]
        [MaxLength(512)]
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// ID of the parent feed.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int FeedId { get; set; }

        /// <summary>
        /// Article title.
        /// </summary>
        [Required]
        [MaxLength(1200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Canonical URL to the original article.
        /// </summary>
        [Required]
        [MaxLength(2048)]
        [Url]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Short summary or description.
        /// </summary>
        [MaxLength(4000)]
        public string? Summary { get; set; }

        /// <summary>
        /// Full article content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Author name(s).
        /// </summary>
        [MaxLength(512)]
        public string? Author { get; set; }

        /// <summary>
        /// Publication date (from feed).
        /// </summary>
        public DateTime? PublishedDate { get; set; }

        /// <summary>
        /// URL of the featured image.
        /// </summary>
        [MaxLength(2048)]
        [Url]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Comma-separated categories from the feed.
        /// </summary>
        [MaxLength(1024)]
        public string? Categories { get; set; }

        /// <summary>
        /// Language code.
        /// </summary>
        [MaxLength(16)]
        public string? Language { get; set; }

        #region Enclosure

        /// <summary>
        /// URL of the media enclosure.
        /// </summary>
        [MaxLength(2048)]
        [Url]
        public string? EnclosureUrl { get; set; }

        /// <summary>
        /// MIME type of the enclosure.
        /// </summary>
        [MaxLength(128)]
        public string? EnclosureType { get; set; }

        /// <summary>
        /// Size of the enclosure in bytes.
        /// </summary>
        [Range(0, long.MaxValue)]
        public long? EnclosureLength { get; set; }

        #endregion

        /// <summary>
        /// Initial tag names to associate with this article.
        /// </summary>
        public List<string> TagNames { get; set; } = new();
    }
}