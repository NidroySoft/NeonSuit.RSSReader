// =======================================================
// Core/DTOs/Categories/UpdateCategoryDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Categories
{
    /// <summary>
    /// Data Transfer Object for updating an existing category.
    /// All properties are optional to support partial updates.
    /// </summary>
    public class UpdateCategoryDto
    {
        /// <summary>
        /// Display name of the category.
        /// </summary>
        [MaxLength(200)]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description of the category.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// ID of the parent category (null for root categories).
        /// </summary>
        public int? ParentCategoryId { get; set; }

        /// <summary>
        /// Sort order within the parent.
        /// </summary>
        public int? SortOrder { get; set; }
    }
}