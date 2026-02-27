// =======================================================
// File: Core/DTOs/ArticleTags/CreateArticleTagDto.cs
// =======================================================

using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.ArticleTags
{
    /// <summary>
    /// DTO for creating a new article-tag association.
    /// Used when manually tagging articles or when rules apply tags.
    /// </summary>
    public class CreateArticleTagDto
    {
        /// <summary>
        /// ID of the article to tag.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int ArticleId { get; set; }

        /// <summary>
        /// ID of the tag to apply.
        /// </summary>
        [Required]
        [Range(1, int.MaxValue)]
        public int TagId { get; set; }

        /// <summary>
        /// Source that applied this tag ("user", "rule", "system").
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AppliedBy { get; set; } = "user";

        /// <summary>
        /// ID of the rule that applied this tag (if applicable).
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Confidence score for auto-applied tags.
        /// </summary>
        [Range(0, 1)]
        public double? Confidence { get; set; }
    }
}