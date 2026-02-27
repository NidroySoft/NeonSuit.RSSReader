// =======================================================
// Core/DTOs/Categories/CreateCategoryDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Categories
{
    /// <summary>
    /// Data Transfer Object for creating a new category.
    /// </summary>
    public class CreateCategoryDto
    {
        /// <summary>
        /// Display name of the category.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

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
        /// Sort order within the parent (lower numbers appear first).
        /// </summary>
        public int SortOrder { get; set; } = 0;
    }
}