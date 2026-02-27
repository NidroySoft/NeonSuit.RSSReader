// =======================================================
// Core/DTOs/Categories/CategoryDto.cs
// =======================================================

using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Categories
{
    /// <summary>
    /// Data Transfer Object for category information.
    /// Used for displaying categories in lists and selection controls.
    /// </summary>
    public class CategoryDto
    {
        /// <summary>
        /// Unique identifier of the category.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Display name of the category.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the category's purpose.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// ID of the parent category (null for root categories).
        /// </summary>
        public int? ParentCategoryId { get; set; }

        /// <summary>
        /// Sort order within the parent category.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Number of feeds directly in this category.
        /// </summary>
        public int FeedCount { get; set; }

        /// <summary>
        /// Total number of unread articles across all feeds in this category.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Depth level in the category tree (0 = root).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Full hierarchical path (e.g., "News / Technology / AI").
        /// </summary>
        public string FullPath { get; set; } = string.Empty;
    }
}