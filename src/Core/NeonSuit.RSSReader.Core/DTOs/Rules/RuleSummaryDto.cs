// =======================================================
// Core/DTOs/Rules/RuleSummaryDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Lightweight Data Transfer Object for rule list views.
    /// Used in rules management interface and rule selection controls.
    /// </summary>
    public class RuleSummaryDto
    {
        /// <summary>
        /// Unique identifier of the rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Descriptive name of the rule.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable condition summary.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Primary action type.
        /// </summary>
        public RuleActionType ActionType { get; set; }

        /// <summary>
        /// Whether the rule is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Execution priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Number of times matched.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Last time matched.
        /// </summary>
        public DateTime? LastMatchDate { get; set; }

        /// <summary>
        /// Health status.
        /// </summary>
        public RuleHealthStatus HealthStatus { get; set; }

        /// <summary>
        /// Scope of the rule.
        /// </summary>
        public RuleScope Scope { get; set; }

        /// <summary>
        /// Human-readable time ago for last match.
        /// </summary>
        public string LastMatchTimeAgo { get; set; } = string.Empty;
    }
}