using System;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization status.
    /// </summary>
    public class SyncStatusDto
    {
        /// <summary>
        /// Current overall synchronization status.
        /// </summary>
        public string CurrentStatus { get; set; } = string.Empty;

        /// <summary>
        /// Whether any synchronization task is currently running.
        /// </summary>
        public bool IsSynchronizing { get; set; }

        /// <summary>
        /// Time when the last synchronization cycle completed.
        /// </summary>
        public DateTime? LastSyncCompleted { get; set; }

        /// <summary>
        /// Time when the next full synchronization is scheduled.
        /// </summary>
        public DateTime? NextSyncScheduled { get; set; }

        /// <summary>
        /// Formatted string for last sync time.
        /// </summary>
        public string LastSyncFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Formatted string for next sync time.
        /// </summary>
        public string NextSyncFormatted { get; set; } = string.Empty;
    }
}