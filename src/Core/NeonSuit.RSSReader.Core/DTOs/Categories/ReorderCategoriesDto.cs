// =======================================================
// Core/DTOs/Categories/ReorderCategoriesDto.cs
// =======================================================

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Categories
{
    /// <summary>
    /// Data Transfer Object for reordering categories.
    /// Used when user drags and drops categories to change their order.
    /// </summary>
    public class ReorderCategoriesDto
    {
        /// <summary>
        /// List of category IDs in the desired order.
        /// </summary>
        [Required]
        [MinLength(1)]
        public List<int> CategoryIds { get; set; } = new();

        /// <summary>
        /// ID of the parent category being reordered (null for root).
        /// </summary>
        public int? ParentCategoryId { get; set; }
    }
}