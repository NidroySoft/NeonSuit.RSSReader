using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Rules
{
    /// <summary>
    /// Data Transfer Object for rule test results.
    /// </summary>
    public class RuleTestResultDto
    {
        /// <summary>
        /// Name of the rule that was tested.
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Number of articles that matched the rule.
        /// </summary>
        public int MatchedCount { get; set; }

        /// <summary>
        /// Total articles tested.
        /// </summary>
        public int TotalTested { get; set; }

        /// <summary>
        /// Match percentage (MatchedCount / TotalTested * 100).
        /// </summary>
        public double MatchPercentage => TotalTested > 0 ? (MatchedCount * 100.0 / TotalTested) : 0;

        /// <summary>
        /// List of article IDs that matched.
        /// </summary>
        public List<int> MatchedArticleIds { get; set; } = new();

        /// <summary>
        /// Average evaluation time per article in milliseconds.
        /// </summary>
        public double AverageEvaluationTimeMs { get; set; }
    }
}