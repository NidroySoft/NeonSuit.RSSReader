// =======================================================
// Core/DTOs/Rules/RuleConditionDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for individual rule conditions.
    /// Used in advanced rule editing and condition management.
    /// </summary>
    public class RuleConditionDto
    {
        /// <summary>
        /// Unique identifier of the condition.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the parent rule.
        /// </summary>
        public int RuleId { get; set; }

        /// <summary>
        /// Group identifier for organizing conditions (0 = default group).
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Execution order within the group (ascending).
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Which article field to evaluate.
        /// </summary>
        public RuleFieldTarget Field { get; set; }

        /// <summary>
        /// Comparison operator to apply.
        /// </summary>
        public RuleOperator Operator { get; set; }

        /// <summary>
        /// Primary value to compare against.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Secondary value (for BETWEEN, NOT BETWEEN operations).
        /// </summary>
        public string Value2 { get; set; } = string.Empty;

        /// <summary>
        /// Whether the comparison is case-sensitive.
        /// </summary>
        public bool IsCaseSensitive { get; set; }

        /// <summary>
        /// Whether to negate this entire condition (NOT).
        /// </summary>
        public bool Negate { get; set; }

        /// <summary>
        /// For Regex operator: the pattern to match.
        /// </summary>
        public string RegexPattern { get; set; } = string.Empty;

        /// <summary>
        /// For date comparisons: format string (e.g., "yyyy-MM-dd").
        /// </summary>
        public string DateFormat { get; set; } = string.Empty;

        /// <summary>
        /// How this condition combines with the NEXT condition in the same group.
        /// </summary>
        public LogicalOperator CombineWithNext { get; set; }

        /// <summary>
        /// Human-readable description of the condition for UI display.
        /// </summary>
        public string HumanReadable { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the condition is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Display name of the field in Spanish.
        /// </summary>
        public string FieldDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the operator in Spanish.
        /// </summary>
        public string OperatorDisplayName { get; set; } = string.Empty;
    }
}