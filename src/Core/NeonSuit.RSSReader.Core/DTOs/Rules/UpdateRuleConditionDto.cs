// =======================================================
// Core/DTOs/Rules/UpdateRuleConditionDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for updating an existing rule condition.
    /// All properties are optional to support partial updates.
    /// </summary>
    public class UpdateRuleConditionDto
    {
        /// <summary>
        /// Group identifier for organizing conditions.
        /// </summary>
        public int? GroupId { get; set; }

        /// <summary>
        /// Execution order within the group.
        /// </summary>
        public int? Order { get; set; }

        /// <summary>
        /// Which article field to evaluate.
        /// </summary>
        public RuleFieldTarget? Field { get; set; }

        /// <summary>
        /// Comparison operator to apply.
        /// </summary>
        public RuleOperator? Operator { get; set; }

        /// <summary>
        /// Primary value to compare against.
        /// </summary>
        [MaxLength(500)]
        public string? Value { get; set; }

        /// <summary>
        /// Secondary value (for BETWEEN, NOT BETWEEN operations).
        /// </summary>
        [MaxLength(500)]
        public string? Value2 { get; set; }

        /// <summary>
        /// Whether the comparison is case-sensitive.
        /// </summary>
        public bool? IsCaseSensitive { get; set; }

        /// <summary>
        /// Whether to negate this entire condition.
        /// </summary>
        public bool? Negate { get; set; }

        /// <summary>
        /// For Regex operator: the pattern to match.
        /// </summary>
        [MaxLength(500)]
        public string? RegexPattern { get; set; }

        /// <summary>
        /// For date comparisons: format string.
        /// </summary>
        [MaxLength(50)]
        public string? DateFormat { get; set; }

        /// <summary>
        /// How this condition combines with the next condition.
        /// </summary>
        public LogicalOperator? CombineWithNext { get; set; }
    }
}