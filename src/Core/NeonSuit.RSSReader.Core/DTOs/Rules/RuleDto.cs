// =======================================================
// Core/DTOs/Rules/RuleDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for complete rule information.
    /// Used in rule details view and editing.
    /// </summary>
    public class RuleDto
    {
        /// <summary>
        /// Unique identifier of the rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Descriptive name for the rule.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description explaining the rule's purpose.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Field where the search will be performed.
        /// </summary>
        public RuleFieldTarget Target { get; set; }

        /// <summary>
        /// Search operator for the main condition.
        /// </summary>
        public RuleOperator Operator { get; set; }

        /// <summary>
        /// Value to compare against.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Second value (for range operations like Between).
        /// </summary>
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the search is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// For regex operations: the regex pattern.
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// Group identifier for complex conditions.
        /// </summary>
        public int ConditionGroup { get; set; }

        /// <summary>
        /// Logical operator to combine with next condition.
        /// </summary>
        public LogicalOperator NextConditionOperator { get; set; }

        /// <summary>
        /// Indicates whether this rule uses advanced conditions.
        /// </summary>
        public bool UsesAdvancedConditions { get; set; }

        /// <summary>
        /// Where the rule applies.
        /// </summary>
        public RuleScope Scope { get; set; }

        /// <summary>
        /// List of feed IDs (if Scope = SpecificFeeds).
        /// </summary>
        public List<int> FeedIds { get; set; } = new();

        /// <summary>
        /// List of category IDs (if Scope = SpecificCategories).
        /// </summary>
        public List<int> CategoryIds { get; set; } = new();

        /// <summary>
        /// Primary action type.
        /// </summary>
        public RuleActionType ActionType { get; set; }

        /// <summary>
        /// List of tag IDs to apply (for ApplyTags action).
        /// </summary>
        public List<int> TagIds { get; set; } = new();

        /// <summary>
        /// Category ID to move article to (for MoveToCategory action).
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Path to sound file for PlaySound action.
        /// </summary>
        public string? SoundPath { get; set; }

        /// <summary>
        /// Template for notification.
        /// </summary>
        public string NotificationTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Notification priority level.
        /// </summary>
        public NotificationPriority NotificationPriority { get; set; }

        /// <summary>
        /// Color for highlighting articles.
        /// </summary>
        public string? HighlightColor { get; set; }

        /// <summary>
        /// Execution order (lower numbers execute first).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether the rule is currently active.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Stop processing other rules if this one matches.
        /// </summary>
        public bool StopOnMatch { get; set; }

        /// <summary>
        /// Only apply to new articles.
        /// </summary>
        public bool OnlyNewArticles { get; set; }

        /// <summary>
        /// Date when the rule was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time the rule was modified.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Number of times this rule has been triggered.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Last time this rule matched an article.
        /// </summary>
        public DateTime? LastMatchDate { get; set; }

        /// <summary>
        /// Human-readable condition for UI display.
        /// </summary>
        public string HumanReadableCondition { get; set; } = string.Empty;

        /// <summary>
        /// Health status of the rule.
        /// </summary>
        public RuleHealthStatus HealthStatus { get; set; }
    }
}