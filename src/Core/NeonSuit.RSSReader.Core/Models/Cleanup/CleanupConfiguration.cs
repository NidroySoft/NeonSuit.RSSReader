namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Configuration settings for database cleanup and maintenance operations.
    /// Controls retention policies, scheduling, and optimization behaviors.
    /// </summary>
    public class CleanupConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupConfiguration"/> class with default values.
        /// </summary>
        public CleanupConfiguration()
        {
            ArticleRetentionDays = 30;
            KeepFavorites = true;
            KeepUnread = true;
            AutoCleanupEnabled = true;
            ImageCacheRetentionDays = 30;
            MaxImageCacheSizeMB = 500;
            CleanupHourOfDay = 2;
            CleanupDayOfWeek = DayOfWeek.Sunday;
            VacuumAfterCleanup = true;
            RebuildIndexesAfterCleanup = false;
        }

        #region Retention Policies

        /// <summary>
        /// Gets or sets the number of days to retain articles before deletion.
        /// Articles older than this value are candidates for cleanup.
        /// </summary>
        /// <value>Number of days. Default is 30. Zero or negative values disable article cleanup.</value>
        public int ArticleRetentionDays { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether favorite articles should be preserved
        /// regardless of retention period.
        /// </summary>
        /// <value>true to keep favorite articles; otherwise, false. Default is true.</value>
        public bool KeepFavorites { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unread articles should be preserved
        /// regardless of retention period.
        /// </summary>
        /// <value>true to keep unread articles; otherwise, false. Default is true.</value>
        public bool KeepUnread { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether automatic cleanup is enabled.
        /// When disabled, manual cleanup must be triggered explicitly.
        /// </summary>
        /// <value>true to enable automatic cleanup; otherwise, false. Default is true.</value>
        public bool AutoCleanupEnabled { get; set; }

        #endregion

        #region Image Cache

        /// <summary>
        /// Gets or sets the number of days to retain cached images.
        /// </summary>
        /// <value>Number of days. Default is 30.</value>
        public int ImageCacheRetentionDays { get; set; }

        /// <summary>
        /// Gets or sets the maximum size of the image cache in megabytes.
        /// When exceeded, oldest images are removed until under this limit.
        /// </summary>
        /// <value>Maximum size in MB. Default is 500.</value>
        public int MaxImageCacheSizeMB { get; set; }

        #endregion

        #region Scheduling

        /// <summary>
        /// Gets or sets the hour of day (0-23) when automatic cleanup should run.
        /// </summary>
        /// <value>Hour in 24-hour format. Default is 2 (2:00 AM).</value>
        public int CleanupHourOfDay { get; set; }

        /// <summary>
        /// Gets or sets the day of week when weekly cleanup should run.
        /// Only applies if cleanup frequency is weekly.
        /// </summary>
        /// <value>DayOfWeek enum value. Default is Sunday.</value>
        public DayOfWeek CleanupDayOfWeek { get; set; }

        #endregion

        #region Database Optimization

        /// <summary>
        /// Gets or sets a value indicating whether to perform VACUUM operation
        /// after cleanup to reclaim storage space.
        /// </summary>
        /// <value>true to vacuum after cleanup; otherwise, false. Default is true.</value>
        public bool VacuumAfterCleanup { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to rebuild indexes after cleanup.
        /// </summary>
        /// <value>true to rebuild indexes; otherwise, false. Default is false.</value>
        public bool RebuildIndexesAfterCleanup { get; set; }

        #endregion

        #region Validation

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        /// <returns>True if configuration is valid; otherwise, false.</returns>
        public bool IsValid()
        {
            return ArticleRetentionDays >= 0 &&
                   ImageCacheRetentionDays > 0 &&
                   MaxImageCacheSizeMB > 0 &&
                   CleanupHourOfDay >= 0 && CleanupHourOfDay <= 23;
        }

        #endregion

        #region Cloning

        /// <summary>
        /// Creates a deep copy of this configuration instance.
        /// </summary>
        /// <returns>A new <see cref="CleanupConfiguration"/> instance with identical values.</returns>
        public CleanupConfiguration Clone()
        {
            return new CleanupConfiguration
            {
                ArticleRetentionDays = this.ArticleRetentionDays,
                KeepFavorites = this.KeepFavorites,
                KeepUnread = this.KeepUnread,
                AutoCleanupEnabled = this.AutoCleanupEnabled,
                ImageCacheRetentionDays = this.ImageCacheRetentionDays,
                MaxImageCacheSizeMB = this.MaxImageCacheSizeMB,
                CleanupHourOfDay = this.CleanupHourOfDay,
                CleanupDayOfWeek = this.CleanupDayOfWeek,
                VacuumAfterCleanup = this.VacuumAfterCleanup,
                RebuildIndexesAfterCleanup = this.RebuildIndexesAfterCleanup
            };
        }

        #endregion

        #region String Representation

        /// <summary>
        /// Gets a summary of the current configuration for logging purposes.
        /// </summary>
        /// <returns>String representation of key configuration values.</returns>
        public override string ToString()
        {
            return $"Retention: {ArticleRetentionDays} days, KeepFavorites: {KeepFavorites}, " +
                   $"KeepUnread: {KeepUnread}, AutoCleanup: {AutoCleanupEnabled}, " +
                   $"Vacuum: {VacuumAfterCleanup}, RebuildIndexes: {RebuildIndexesAfterCleanup}";
        }

        #endregion

        #region Preference Keys

        /// <summary>
        /// Preference key constants for application settings storage.
        /// These keys are used with ISettingsService to persist user preferences.
        /// </summary>
        public static class PreferenceKeys
        {
            /// <summary>Number of days to retain articles before cleanup.</summary>
            public const string ArticleRetentionDays = "article_retention_days";

            /// <summary>Whether to preserve favorite articles during cleanup.</summary>
            public const string KeepFavoriteArticles = "keep_favorite_articles";

            /// <summary>Whether to preserve unread articles during cleanup.</summary>
            public const string KeepUnreadArticles = "keep_unread_articles";

            /// <summary>Master switch for automatic database cleanup.</summary>
            public const string AutoCleanupEnabled = "auto_cleanup_enabled";

            /// <summary>Maximum age in days for cached images.</summary>
            public const string ImageCacheRetentionDays = "image_cache_retention_days";

            /// <summary>Maximum size in MB for image cache.</summary>
            public const string ImageCacheMaxSizeMB = "image_cache_max_size_mb";

            /// <summary>Hour of day (0-23) when automatic cleanup runs.</summary>
            public const string CleanupHourOfDay = "cleanup_hour_of_day";

            /// <summary>Day of week for weekly cleanup schedule.</summary>
            public const string CleanupDayOfWeek = "cleanup_day_of_week";

            /// <summary>Whether to perform VACUUM after cleanup.</summary>
            public const string VacuumAfterCleanup = "vacuum_after_cleanup";

            /// <summary>Whether to rebuild indexes after cleanup.</summary>
            public const string RebuildIndexesAfterCleanup = "rebuild_indexes_after_cleanup";

            /// <summary>Last time cleanup was performed (Unix timestamp).</summary>
            public const string LastCleanupTime = "last_cleanup_time";

            /// <summary>Whether cleanup is currently running (prevents concurrent runs).</summary>
            public const string CleanupInProgress = "cleanup_in_progress";
        }

        #endregion
    }
}