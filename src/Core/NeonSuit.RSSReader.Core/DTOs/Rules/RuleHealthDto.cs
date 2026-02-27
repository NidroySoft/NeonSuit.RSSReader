// =======================================================
// Core/DTOs/Rules/RuleHealthDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using System;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for rule health monitoring.
    /// Used in dashboard and rule analytics views.
    /// </summary>
    public class RuleHealthDto
    {
        /// <summary>
        /// Unique identifier of the rule.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the rule.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status.
        /// </summary>
        public RuleHealthStatus Status { get; set; }

        /// <summary>
        /// Whether the rule is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Total match count.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Matches in the last 24 hours.
        /// </summary>
        public int MatchesLast24h { get; set; }

        /// <summary>
        /// Matches in the last 7 days.
        /// </summary>
        public int MatchesLast7d { get; set; }

        /// <summary>
        /// Matches in the last 30 days.
        /// </summary>
        public int MatchesLast30d { get; set; }

        /// <summary>
        /// Last match date.
        /// </summary>
        public DateTime? LastMatchDate { get; set; }

        /// <summary>
        /// Time since last match (human readable).
        /// </summary>
        public string TimeSinceLastMatch { get; set; } = string.Empty;

        /// <summary>
        /// Average matches per day (last 30 days).
        /// </summary>
        public double AverageMatchesPerDay { get; set; }
    }
}