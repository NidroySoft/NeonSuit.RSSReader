// =======================================================
// File: Core/DTOs/Articles/UpdateArticleDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Data Transfer Object for updating an existing article.
    /// Only user-modifiable fields are included (metadata, tags, and user state).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO is used when users manually edit article metadata or when
    /// updating user interaction states (read status, starred, favorite).
    /// </para>
    /// <para>
    /// All properties are optional to support partial updates.
    /// Only provided properties will be updated.
    /// </para>
    /// </remarks>
    public class UpdateArticleDto
    {
        /// <summary>
        /// Article title.
        /// </summary>
        [MaxLength(1200)]
        public string? Title { get; set; }

        /// <summary>
        /// Short summary or description.
        /// </summary>
        [MaxLength(4000)]
        public string? Summary { get; set; }

        /// <summary>
        /// Author name(s).
        /// </summary>
        [MaxLength(512)]
        public string? Author { get; set; }

        /// <summary>
        /// Current read status.
        /// </summary>
        public ArticleStatus? Status { get; set; }

        /// <summary>
        /// Starred status.
        /// </summary>
        public bool? IsStarred { get; set; }

        /// <summary>
        /// Favorite status.
        /// </summary>
        public bool? IsFavorite { get; set; }

        /// <summary>
        /// Reading progress percentage.
        /// </summary>
        [Range(0, 100)]
        public int? ReadPercentage { get; set; }

        /// <summary>
        /// Last read timestamp.
        /// </summary>
        public DateTime? LastReadAt { get; set; }

        /// <summary>
        /// URL of the featured image (user can override).
        /// </summary>
        [MaxLength(2048)]
        [Url]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Comma-separated categories (user can override).
        /// </summary>
        [MaxLength(1024)]
        public string? Categories { get; set; }

        /// <summary>
        /// Complete replacement list of tag names.
        /// If provided, replaces all existing tags.
        /// </summary>
        public List<string>? TagNames { get; set; }

        /// <summary>
        /// Tag names to add to the existing tags.
        /// </summary>
        public List<string>? AddTags { get; set; }

        /// <summary>
        /// Tag names to remove from the existing tags.
        /// </summary>
        public List<string>? RemoveTags { get; set; }
    }
}