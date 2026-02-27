// =======================================================
// Core/DTOs/Feeds/FeedHealthDto.cs
// =======================================================

using NeonSuit.RSSReader.Core.Enums;

namespace NeonSuit.RSSReader.Core.DTOs.Feeds
{
    /// <summary>
    /// Data Transfer Object for feed health monitoring.
    /// Used in dashboard and health checks.
    /// </summary>
    public class FeedHealthDto
    {
        /// <summary>
        /// Unique identifier of the feed.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Title of the feed.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Health status.
        /// </summary>
        public FeedHealthStatus Status { get; set; }

        /// <summary>
        /// Number of consecutive failures.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Last error message.
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Time since last successful update.
        /// </summary>
        public string? TimeSinceLastUpdate { get; set; }

        /// <summary>
        /// Whether the feed is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Next scheduled update.
        /// </summary>
        public DateTime? NextUpdate { get; set; }
    }
}