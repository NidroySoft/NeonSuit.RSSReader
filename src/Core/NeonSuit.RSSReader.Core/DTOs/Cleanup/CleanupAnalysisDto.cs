using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Cleanup
{
    /// <summary>
    /// Data Transfer Object for cleanup impact analysis results.
    /// </summary>
    public class CleanupAnalysisDto
    {
        /// <summary>
        /// Number of days used for retention analysis.
        /// </summary>
        public int RetentionDays { get; set; }

        /// <summary>
        /// Cutoff date used for analysis.
        /// </summary>
        public DateTime CutoffDate { get; set; }

        /// <summary>
        /// Number of articles that would be deleted.
        /// </summary>
        public int ArticlesToDelete { get; set; }

        /// <summary>
        /// Number of articles that would be kept.
        /// </summary>
        public int ArticlesToKeep { get; set; }

        /// <summary>
        /// Whether favorites would be kept.
        /// </summary>
        public bool WouldKeepFavorites { get; set; }

        /// <summary>
        /// Whether unread articles would be kept.
        /// </summary>
        public bool WouldKeepUnread { get; set; }

        /// <summary>
        /// Estimated space that would be freed in bytes.
        /// </summary>
        public long EstimatedSpaceFreedBytes { get; set; }

        /// <summary>
        /// Estimated space that would be freed in MB (calculated).
        /// </summary>
        public double EstimatedSpaceFreedMB => EstimatedSpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Dictionary of article counts by feed ID.
        /// </summary>
        public Dictionary<int, int> ArticlesByFeed { get; set; } = new();
    }
}