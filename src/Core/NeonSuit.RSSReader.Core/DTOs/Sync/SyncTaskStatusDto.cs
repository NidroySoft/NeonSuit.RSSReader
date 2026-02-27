using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization task status.
    /// </summary>
    public class SyncTaskStatusDto
    {
        /// <summary>
        /// Type of the task.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the task.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Whether the task is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Task interval in minutes.
        /// </summary>
        public int IntervalMinutes { get; set; }

        /// <summary>
        /// Next scheduled run time.
        /// </summary>
        public DateTime? NextScheduled { get; set; }

        /// <summary>
        /// Formatted next scheduled time.
        /// </summary>
        public string NextScheduledFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Last run start time.
        /// </summary>
        public DateTime? LastRunStart { get; set; }

        /// <summary>
        /// Last run end time.
        /// </summary>
        public DateTime? LastRunEnd { get; set; }

        /// <summary>
        /// Last run duration in seconds.
        /// </summary>
        public double? LastRunDurationSeconds { get; set; }

        /// <summary>
        /// Whether last run was successful.
        /// </summary>
        public bool LastRunSuccessful { get; set; }

        /// <summary>
        /// Total number of runs.
        /// </summary>
        public int TotalRuns { get; set; }

        /// <summary>
        /// Number of successful runs.
        /// </summary>
        public int SuccessfulRuns { get; set; }

        /// <summary>
        /// Success rate as percentage.
        /// </summary>
        public double SuccessRate => TotalRuns > 0
            ? (SuccessfulRuns * 100.0 / TotalRuns)
            : 0;
    }
}