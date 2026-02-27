// =======================================================
// File: Core/DTOs/ArticleTags/BulkTagOperationDto.cs
// =======================================================

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.ArticleTags
{
    /// <summary>
    /// DTO for bulk tag operations (add/remove multiple tags to/from multiple articles).
    /// </summary>
    /// <remarks>
    /// Used in batch operations like "apply tag to all selected articles" or
    /// "remove tag from all articles in this category".
    /// </remarks>
    public class BulkTagOperationDto
    {
        /// <summary>
        /// List of article IDs to operate on.
        /// </summary>
        [Required]
        [MinLength(1)]
        public List<int> ArticleIds { get; set; } = new();

        /// <summary>
        /// List of tag IDs to add or remove.
        /// </summary>
        [Required]
        [MinLength(1)]
        public List<int> TagIds { get; set; } = new();

        /// <summary>
        /// Operation type (Add or Remove).
        /// </summary>
        [Required]
        public BulkTagOperationType Operation { get; set; }

        /// <summary>
        /// Source of the operation (for audit).
        /// </summary>
        [MaxLength(50)]
        public string AppliedBy { get; set; } = "user";
    }

    /// <summary>
    /// Type of bulk tag operation.
    /// </summary>
    public enum BulkTagOperationType
    {
        /// <summary>Add tags to articles.</summary>
        Add,
        /// <summary>Remove tags from articles.</summary>
        Remove
    }
}