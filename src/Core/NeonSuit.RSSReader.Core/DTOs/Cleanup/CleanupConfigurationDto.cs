using System;

namespace NeonSuit.RSSReader.Core.DTOs.Cleanup
{
    /// <summary>
    /// Data Transfer Object for database cleanup configuration.
    /// </summary>
    public class CleanupConfigurationDto
    {
        /// <summary>
        /// Number of days to keep articles before they become eligible for cleanup.
        /// </summary>
        public int ArticleRetentionDays { get; set; } = 30;

        /// <summary>
        /// Whether to keep favorite/starred articles regardless of age.
        /// </summary>
        public bool KeepFavorites { get; set; } = true;

        /// <summary>
        /// Whether to keep unread articles regardless of age.
        /// </summary>
        public bool KeepUnread { get; set; } = true;

        /// <summary>
        /// Whether automatic cleanup is enabled.
        /// </summary>
        public bool AutoCleanupEnabled { get; set; } = true;

        /// <summary>
        /// Maximum size of image cache in megabytes.
        /// </summary>
        public int MaxImageCacheSizeMB { get; set; } = 500;

        /// <summary>
        /// Hour of day (0-23) when automatic cleanup should run.
        /// </summary>
        public int CleanupHourOfDay { get; set; } = 2;

        /// <summary>
        /// Day of week when automatic cleanup should run.
        /// </summary>
        public DayOfWeek CleanupDayOfWeek { get; set; } = DayOfWeek.Sunday;

        /// <summary>
        /// Whether to run VACUUM after cleanup operations.
        /// </summary>
        public bool VacuumAfterCleanup { get; set; } = true;

        /// <summary>
        /// Whether to rebuild indexes after cleanup operations.
        /// </summary>
        public bool RebuildIndexesAfterCleanup { get; set; } = false;
    }
}