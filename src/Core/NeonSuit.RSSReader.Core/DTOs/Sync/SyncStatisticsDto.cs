using System;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization statistics.
    /// </summary>
    public class SyncStatisticsDto
    {
        /// <summary>
        /// Total number of sync cycles performed.
        /// </summary>
        public int TotalSyncCycles { get; set; }

        /// <summary>
        /// Number of successful sync cycles.
        /// </summary>
        public int SuccessfulSyncs { get; set; }

        /// <summary>
        /// Number of failed sync cycles.
        /// </summary>
        public int FailedSyncs { get; set; }

        /// <summary>
        /// Success rate as percentage.
        /// </summary>
        public double SuccessRate => TotalSyncCycles > 0
            ? (SuccessfulSyncs * 100.0 / TotalSyncCycles)
            : 0;

        /// <summary>
        /// Average sync cycle duration in seconds.
        /// </summary>
        public double AverageSyncDurationSeconds { get; set; }

        /// <summary>
        /// Total time spent synchronizing (formatted).
        /// </summary>
        public string TotalSyncTimeFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Total articles processed during syncs.
        /// </summary>
        public int ArticlesProcessed { get; set; }

        /// <summary>
        /// Total feeds updated during syncs.
        /// </summary>
        public int FeedsUpdated { get; set; }

        /// <summary>
        /// Total tags applied during syncs.
        /// </summary>
        public int TagsApplied { get; set; }

        /// <summary>
        /// Timestamp of last statistics update.
        /// </summary>
        public DateTime LastStatisticsUpdate { get; set; }

        /// <summary>
        /// Formatted time since last update.
        /// </summary>
        public string LastUpdateFormatted { get; set; } = string.Empty;
    }
}