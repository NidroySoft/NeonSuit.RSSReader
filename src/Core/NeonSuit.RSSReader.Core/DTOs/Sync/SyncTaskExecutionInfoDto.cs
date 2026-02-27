using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for detailed task execution information.
    /// </summary>
    public class SyncTaskExecutionInfoDto
    {
        /// <summary>
        /// Type of the task.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Start time of the last run.
        /// </summary>
        public DateTime? LastRunStart { get; set; }

        /// <summary>
        /// End time of the last run.
        /// </summary>
        public DateTime? LastRunEnd { get; set; }

        /// <summary>
        /// Duration of the last run in seconds.
        /// </summary>
        public double? LastRunDurationSeconds { get; set; }

        /// <summary>
        /// Formatted last run duration.
        /// </summary>
        public string LastRunDurationFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Whether the last run was successful.
        /// </summary>
        public bool LastRunSuccessful { get; set; }

        /// <summary>
        /// Error message from last run (if failed).
        /// </summary>
        public string? LastRunError { get; set; }

        /// <summary>
        /// Total number of runs.
        /// </summary>
        public int TotalRuns { get; set; }

        /// <summary>
        /// Number of successful runs.
        /// </summary>
        public int SuccessfulRuns { get; set; }

        /// <summary>
        /// Average run duration in seconds.
        /// </summary>
        public double AverageRunDurationSeconds { get; set; }

        /// <summary>
        /// Formatted average run duration.
        /// </summary>
        public string AverageRunDurationFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Next scheduled run time.
        /// </summary>
        public DateTime? NextScheduledRun { get; set; }

        /// <summary>
        /// Formatted next scheduled run.
        /// </summary>
        public string NextScheduledFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Results from the last run.
        /// </summary>
        public Dictionary<string, object> LastRunResults { get; set; } = new();
    }
}