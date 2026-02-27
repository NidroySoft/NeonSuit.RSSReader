// =======================================================
// File: Core/DTOs/Articles/ArticleStateDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Article
{
    /// <summary>
    /// Data Transfer Object for updating only the user interaction state of an article.
    /// Ultra-lightweight DTO for quick state changes (read, starred, favorite).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used for bulk operations and real-time UI updates where only the user state
    /// needs to be modified (mark as read, toggle star, etc.).
    /// </para>
    /// <para>
    /// All properties are nullable to support partial updates.
    /// </para>
    /// </remarks>
    public class ArticleStateDto
    {
        /// <summary>
        /// Article status (Read/Unread).
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
        /// Returns true if any property has a value.
        /// </summary>
        public bool HasChanges =>
            Status.HasValue ||
            IsStarred.HasValue ||
            IsFavorite.HasValue ||
            ReadPercentage.HasValue ||
            LastReadAt.HasValue;
    }
}