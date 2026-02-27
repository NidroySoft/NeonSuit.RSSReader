using System;
using System.Collections.Generic;
using System.Linq;

namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Represents the projected impact analysis of a database cleanup operation.
    /// Provides estimates of what would be deleted and how much space would be freed
    /// without actually performing the deletion.
    /// </summary>
    public class CleanupAnalysis
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupAnalysis"/> class.
        /// </summary>
        public CleanupAnalysis()
        {
            ArticlesByFeed = new Dictionary<int, int>();
        }

        #region Configuration Used

        /// <summary>
        /// Gets or sets the number of articles found in the database.
        /// </summary>
        public int ArticlesFound { get; set; }

        /// <summary>
        /// Gets or sets the retention period in days that was used for this analysis.
        /// </summary>
        public int RetentionDays { get; set; }

        /// <summary>
        /// Gets or sets the cutoff date calculated from the retention period.
        /// Articles older than this date would be deleted.
        /// </summary>
        public DateTime CutoffDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether favorite articles would be preserved.
        /// </summary>
        public bool WouldKeepFavorites { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unread articles would be preserved.
        /// </summary>
        public bool WouldKeepUnread { get; set; }

        #endregion

        #region Impact Projections

        /// <summary>
        /// Gets or sets the number of articles that would be deleted.
        /// </summary>
        public int ArticlesToDelete { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that would be preserved.
        /// </summary>
        public int ArticlesToKeep { get; set; }

        /// <summary>
        /// Gets the total number of articles analyzed.
        /// </summary>
        public int TotalArticles => ArticlesToDelete + ArticlesToKeep;

        /// <summary>
        /// Gets or sets the estimated bytes that would be freed.
        /// </summary>
        public long EstimatedSpaceFreedBytes { get; set; }

        /// <summary>
        /// Gets the estimated space freed in megabytes.
        /// </summary>
        public double EstimatedSpaceFreedMB => EstimatedSpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the estimated space freed in a human-readable format.
        /// </summary>
        public string EstimatedSpaceFreedFormatted => FormatBytes(EstimatedSpaceFreedBytes);

        /// <summary>
        /// Gets the percentage of articles that would be deleted.
        /// </summary>
        public double DeletionPercentage => TotalArticles > 0 ? (double)ArticlesToDelete / TotalArticles : 0;

        #endregion

        #region Distribution Analysis

        /// <summary>
        /// Gets or sets the distribution of articles to be deleted by feed.
        /// Key is the feed ID, value is the count of articles from that feed.
        /// </summary>
        public Dictionary<int, int> ArticlesByFeed { get; set; }

        /// <summary>
        /// Gets the feed with the most articles to be deleted.
        /// </summary>
        public KeyValuePair<int, int>? MostAffectedFeed
        {
            get
            {
                if (ArticlesByFeed.Count == 0) return null;
                return ArticlesByFeed.MaxBy(kvp => kvp.Value);
            }
        }

        /// <summary>
        /// Gets the number of unique feeds that would be affected.
        /// </summary>
        public int AffectedFeedsCount => ArticlesByFeed.Count;

        #endregion

        #region Risk Assessment

        /// <summary>
        /// Gets a value indicating whether this cleanup would be considered high impact
        /// (more than 50% of articles would be deleted).
        /// </summary>
        public bool IsHighImpact => DeletionPercentage > 0.5;

        /// <summary>
        /// Gets a value indicating whether this cleanup would be considered low impact
        /// (less than 10% of articles would be deleted).
        /// </summary>
        public bool IsLowImpact => DeletionPercentage < 0.1;

        /// <summary>
        /// Gets a risk level assessment for this cleanup operation.
        /// </summary>
        public CleanupRiskLevel RiskLevel => DeletionPercentage switch
        {
            > 0.75 => CleanupRiskLevel.Critical,
            > 0.50 => CleanupRiskLevel.High,
            > 0.25 => CleanupRiskLevel.Medium,
            > 0.10 => CleanupRiskLevel.Low,
            _ => CleanupRiskLevel.Minimal
        };

        /// <summary>
        /// Gets a value indicating whether any articles would be deleted.
        /// </summary>
        public bool AnyDeleted => ArticlesToDelete > 0;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a detailed report of the cleanup analysis.
        /// </summary>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Cleanup Impact Analysis ===",
                $"Analysis Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                "",
                "--- Configuration ---",
                $"Retention Period:   {RetentionDays} days",
                $"Cutoff Date:        {CutoffDate:yyyy-MM-dd}",
                $"Keep Favorites:     {(WouldKeepFavorites ? "Yes" : "No")}",
                $"Keep Unread:        {(WouldKeepUnread ? "Yes" : "No")}",
                "",
                "--- Impact Summary ---",
                $"Articles to Delete: {ArticlesToDelete,10:N0} ({DeletionPercentage:P1})",
                $"Articles to Keep:   {ArticlesToKeep,10:N0} ({1 - DeletionPercentage:P1})",
                $"Total Articles:     {TotalArticles,10:N0}",
                $"",
                $"Space to be Freed:  {EstimatedSpaceFreedFormatted}",
                $"Risk Level:         {RiskLevel}",
                ""
            };

            if (ArticlesByFeed.Count > 0)
            {
                lines.Add("--- Distribution by Feed ---");
                foreach (var feed in ArticlesByFeed.OrderByDescending(x => x.Value).Take(10))
                {
                    lines.Add($"  Feed ID {feed.Key,-10} {feed.Value,8:N0} articles");
                }

                if (ArticlesByFeed.Count > 10)
                {
                    lines.Add($"  ... and {ArticlesByFeed.Count - 10} more feeds");
                }
            }

            lines.Add("===============================");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of the analysis.
        /// </summary>
        public override string ToString()
        {
            return $"{ArticlesToDelete:N0} of {TotalArticles:N0} articles would be deleted " +
                   $"({DeletionPercentage:P1}), freeing ~{EstimatedSpaceFreedFormatted}";
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} B"
            };
        }

        #endregion
    }
}