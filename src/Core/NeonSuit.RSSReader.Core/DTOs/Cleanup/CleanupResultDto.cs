using System;
using System.Collections.Generic;
using NeonSuit.RSSReader.Core.Models.Cleanup;

namespace NeonSuit.RSSReader.Core.DTOs.Cleanup
{
    /// <summary>
    /// Data Transfer Object for comprehensive cleanup operation results.
    /// </summary>
    public class CleanupResultDto
    {
        /// <summary>
        /// Whether the cleanup operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Whether the cleanup was skipped.
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Timestamp when cleanup was performed.
        /// </summary>
        public DateTime PerformedAt { get; set; }

        /// <summary>
        /// Total duration of the cleanup operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Results of article deletion operation.
        /// </summary>
        public ArticleDeletionResult? ArticleCleanup { get; set; }

        /// <summary>
        /// Results of orphaned record removal.
        /// </summary>
        public OrphanRemovalResult? OrphanCleanup { get; set; }

        /// <summary>
        /// Total space freed in bytes across all operations.
        /// </summary>
        public long SpaceFreedBytes { get; set; }

        /// <summary>
        /// Total space freed in MB (calculated).
        /// </summary>
        public double SpaceFreedMB => SpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Collection of errors encountered during cleanup.
        /// </summary>
        public List<string> Errors { get; set; } = new();
    }
}