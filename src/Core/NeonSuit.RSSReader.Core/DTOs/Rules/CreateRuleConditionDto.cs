// =======================================================
// Core/DTOs/Rules/CreateRuleConditionDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for creating a new rule condition.
    /// </summary>
    public class CreateRuleConditionDto
    {
        /// <summary>
        /// ID of the parent rule.
        /// </summary>
        [Required]
        public int RuleId { get; set; }

        /// <summary>
        /// Group identifier for organizing conditions.
        /// </summary>
        public int GroupId { get; set; } = 0;

        /// <summary>
        /// Execution order within the group.
        /// </summary>
        public int Order { get; set; } = 0;

        /// <summary>
        /// Which article field to evaluate.
        /// </summary>
        [Required]
        public RuleFieldTarget Field { get; set; } = RuleFieldTarget.Title;

        /// <summary>
        /// Comparison operator to apply.
        /// </summary>
        [Required]
        public RuleOperator Operator { get; set; } = RuleOperator.Contains;

        /// <summary>
        /// Primary value to compare against.
        /// </summary>
        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Secondary value (for BETWEEN, NOT BETWEEN operations).
        /// </summary>
        [MaxLength(500)]
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the comparison is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// Whether to negate this entire condition.
        /// </summary>
        public bool Negate { get; set; }

        /// <summary>
        /// For Regex operator: the pattern to match.
        /// </summary>
        [MaxLength(500)]
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// For date comparisons: format string.
        /// </summary>
        [MaxLength(50)]
        public string DateFormat { get; set; } = string.Empty;

        /// <summary>
        /// How this condition combines with the next condition.
        /// </summary>
        public LogicalOperator CombineWithNext { get; set; } = LogicalOperator.AND;
    }
}