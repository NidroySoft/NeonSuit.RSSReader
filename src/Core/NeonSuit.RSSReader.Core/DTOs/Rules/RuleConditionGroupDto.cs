// =======================================================
// Core/DTOs/Rules/RuleConditionGroupDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for a group of rule conditions.
    /// Used when organizing conditions into logical groups (AND/OR).
    /// </summary>
    public class RuleConditionGroupDto
    {
        /// <summary>
        /// Group identifier.
        /// </summary>
        public int GroupId { get; set; }

        /// <summary>
        /// Logical operator for combining conditions in this group.
        /// </summary>
        public LogicalOperator GroupOperator { get; set; } = LogicalOperator.AND;

        /// <summary>
        /// Conditions in this group, ordered by Order.
        /// </summary>
        public List<RuleConditionDto> Conditions { get; set; } = new();

        /// <summary>
        /// Human-readable representation of the group.
        /// </summary>
        public string HumanReadable { get; set; } = string.Empty;
    }
}