using System;

namespace NeonSuit.RSSReader.Core.DTOs.Sync
{
    /// <summary>
    /// Data Transfer Object for synchronization error information.
    /// </summary>
    public class SyncErrorInfoDto
    {
        /// <summary>
        /// Time when the error occurred.
        /// </summary>
        public DateTime ErrorTime { get; set; }

        /// <summary>
        /// Formatted error time.
        /// </summary>
        public string ErrorTimeFormatted { get; set; } = string.Empty;

        /// <summary>
        /// Task type that encountered the error.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;

        /// <summary>
        /// Error message.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Stack trace (if available).
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Whether the error is recoverable (can be retried).
        /// </summary>
        public bool IsRecoverable { get; set; }

        /// <summary>
        /// Number of retry attempts for this error.
        /// </summary>
        public int RetryCount { get; set; }
    }
}