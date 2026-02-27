// =======================================================
// File: Core/DTOs/ArticleTags/ArticleTagInfoDto.cs
// =======================================================

using System;

namespace NeonSuit.RSSReader.Core.DTOs.ArticleTags
{
    /// <summary>
    /// DTO for article-tag relationship information.
    /// Used when displaying tag metadata on articles (who applied it, when, confidence).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This DTO is used in article detail views to show not just which tags are applied,
    /// but also metadata about the application (automatic vs manual, confidence score, etc.).
    /// </para>
    /// </remarks>
    public class ArticleTagInfoDto
    {
        /// <summary>
        /// ID of the tag.
        /// </summary>
        public int TagId { get; set; }

        /// <summary>
        /// Name of the tag.
        /// </summary>
        public string TagName { get; set; } = string.Empty;

        /// <summary>
        /// Color of the tag (for UI display).
        /// </summary>
        public string TagColor { get; set; } = string.Empty;

        /// <summary>
        /// Source that applied this tag ("user", "rule", "system", etc.).
        /// </summary>
        public string AppliedBy { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the tag was applied.
        /// </summary>
        public DateTime AppliedAt { get; set; }

        /// <summary>
        /// Human-readable time ago string.
        /// </summary>
        public string TimeAgo { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score (0.0–1.0) for auto-applied tags.
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Whether this tag was applied automatically by a rule.
        /// </summary>
        public bool IsAutoApplied => AppliedBy != "user";

        /// <summary>
        /// Name of the rule that applied this tag (if applicable).
        /// </summary>
        public string? RuleName { get; set; }
    }
}