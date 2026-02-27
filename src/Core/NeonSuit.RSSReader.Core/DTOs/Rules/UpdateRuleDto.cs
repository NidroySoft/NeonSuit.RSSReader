// =======================================================
// Core/DTOs/Rules/UpdateRuleDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for updating an existing rule.
    /// All properties are optional to support partial updates.
    /// </summary>
    public class UpdateRuleDto
    {
        /// <summary>
        /// Descriptive name for the rule.
        /// </summary>
        [MaxLength(200)]
        public string? Name { get; set; }

        /// <summary>
        /// Optional description.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Field to search in.
        /// </summary>
        public RuleFieldTarget? Target { get; set; }

        /// <summary>
        /// Search operator.
        /// </summary>
        public RuleOperator? Operator { get; set; }

        /// <summary>
        /// Value to compare against.
        /// </summary>
        [MaxLength(500)]
        public string? Value { get; set; }

        /// <summary>
        /// Second value (for range operations).
        /// </summary>
        [MaxLength(500)]
        public string? Value2 { get; set; }

        /// <summary>
        /// Whether search is case-sensitive.
        /// </summary>
        public bool? IsCaseSensitive { get; set; }

        /// <summary>
        /// Regex pattern (if Operator = Regex).
        /// </summary>
        [MaxLength(500)]
        public string? RegexPattern { get; set; }

        /// <summary>
        /// Whether to use advanced conditions.
        /// </summary>
        public bool? UsesAdvancedConditions { get; set; }

        /// <summary>
        /// Rule scope.
        /// </summary>
        public RuleScope? Scope { get; set; }

        /// <summary>
        /// Feed IDs (if Scope = SpecificFeeds).
        /// </summary>
        public List<int>? FeedIds { get; set; }

        /// <summary>
        /// Category IDs (if Scope = SpecificCategories).
        /// </summary>
        public List<int>? CategoryIds { get; set; }

        /// <summary>
        /// Action type.
        /// </summary>
        public RuleActionType? ActionType { get; set; }

        /// <summary>
        /// Tag IDs to apply (if Action = ApplyTags).
        /// </summary>
        public List<int>? TagIds { get; set; }

        /// <summary>
        /// Category ID to move to (if Action = MoveToCategory).
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Sound file path (if Action = PlaySound).
        /// </summary>
        [MaxLength(500)]
        public string? SoundPath { get; set; }

        /// <summary>
        /// Notification template.
        /// </summary>
        [MaxLength(1000)]
        public string? NotificationTemplate { get; set; }

        /// <summary>
        /// Notification priority.
        /// </summary>
        public NotificationPriority? NotificationPriority { get; set; }

        /// <summary>
        /// Highlight color.
        /// </summary>
        [MaxLength(20)]
        public string? HighlightColor { get; set; }

        /// <summary>
        /// Execution priority (lower = higher priority).
        /// </summary>
        [Range(1, 1000)]
        public int? Priority { get; set; }

        /// <summary>
        /// Whether the rule is enabled.
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// Stop processing on match.
        /// </summary>
        public bool? StopOnMatch { get; set; }

        /// <summary>
        /// Only apply to new articles.
        /// </summary>
        public bool? OnlyNewArticles { get; set; }

        /// <summary>
        /// Reset match count to zero.
        /// </summary>
        public bool? ResetMatchCount { get; set; }
    }
}
