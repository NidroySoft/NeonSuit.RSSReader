// =======================================================
// Core/DTOs/Categories/CategoryTreeDto.cs
// =======================================================

using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Categories
{
    /// <summary>
    /// Data Transfer Object for hierarchical category tree display.
    /// Used in navigation sidebars and tree views.
    /// </summary>
    public class CategoryTreeDto
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
        /// Sort order within the parent.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Number of feeds directly in this category.
        /// </summary>
        public int FeedCount { get; set; }

        /// <summary>
        /// Number of unread articles in this category.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// Depth level (0 = root, 1 = first child, etc.).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Whether the category is expanded in the UI.
        /// </summary>
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Child categories.
        /// </summary>
        public List<CategoryTreeDto> Children { get; set; } = new();
    }
}