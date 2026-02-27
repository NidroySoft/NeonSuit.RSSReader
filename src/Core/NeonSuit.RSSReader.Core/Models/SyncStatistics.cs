using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Aggregated sync statistics.
    /// </summary>
    [Table("SyncStatistics")]
    public class SyncStatistics
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Total number of sync cycles.
        /// </summary>
        public int TotalSyncCycles { get; set; }

        /// <summary>
        /// Number of successful syncs.
        /// </summary>
        public int SuccessfulSyncs { get; set; }

        /// <summary>
        /// Number of failed syncs.
        /// </summary>
        public int FailedSyncs { get; set; }

        /// <summary>
        /// Average sync duration in seconds.
        /// </summary>
        public double AverageSyncDurationSeconds { get; set; }

        /// <summary>
        /// Total sync time in seconds.
        /// </summary>
        public double TotalSyncTimeSeconds { get; set; }

        /// <summary>
        /// Total articles processed.
        /// </summary>
        public int ArticlesProcessed { get; set; }

        /// <summary>
        /// Total feeds updated.
        /// </summary>
        public int FeedsUpdated { get; set; }

        /// <summary>
        /// Total tags applied.
        /// </summary>
        public int TagsApplied { get; set; }

        /// <summary>
        /// Timestamp of last update.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}