namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Result of an article deletion operation.
    /// </summary>
    public class ArticleDeletionResult
    {
        /// <summary>Number of articles actually deleted.</summary>
        public int ArticlesDeleted { get; set; }

        /// <summary>Number of articles matching deletion criteria before operation.</summary>
        public int ArticlesFound { get; set; }

        /// <summary>Date of the oldest article deleted.</summary>
        public DateTime? OldestArticleDeleted { get; set; }

        /// <summary>Date of the newest article deleted.</summary>
        public DateTime? NewestArticleDeleted { get; set; }
    }

    /// <summary>
    /// Result of orphan record removal operation.
    /// </summary>
    public class OrphanRemovalResult
    {
        /// <summary>Number of orphaned article-tag associations removed.</summary>
        public int OrphanedArticleTagsRemoved { get; set; }

        /// <summary>Number of orphaned articles removed.</summary>
        public int OrphanedArticlesRemoved { get; set; }

        /// <summary>Number of orphaned categories removed.</summary>
        public int OrphanedCategoriesRemoved { get; set; }

        /// <summary>Total number of records removed.</summary>
        public int TotalRecordsRemoved =>
            OrphanedArticleTagsRemoved + OrphanedArticlesRemoved + OrphanedCategoriesRemoved;
    }

    /// <summary>
    /// Result of database vacuum operation.
    /// </summary>
    public class VacuumResult
    {
        /// <summary>Database size in bytes before vacuum.</summary>
        public long SizeBeforeBytes { get; set; }

        /// <summary>Database size in bytes after vacuum.</summary>
        public long SizeAfterBytes { get; set; }

        /// <summary>Space freed by vacuum operation.</summary>
        public long SpaceFreedBytes => SizeBeforeBytes - SizeAfterBytes;

        /// <summary>Duration of the vacuum operation.</summary>
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Result of cleanup analysis operation.
    /// </summary>
    public class CleanupAnalysisResult
    {
        /// <summary>Number of articles found in the database.</summary>
        public int ArticlesFound { get; set; }
        /// <summary>Retention period analyzed in days.</summary>
        public int RetentionDays { get; set; }

        /// <summary>Cutoff date used for analysis.</summary>
        public DateTime CutoffDate { get; set; }

        /// <summary>Number of articles that would be deleted.</summary>
        public int ArticlesToDelete { get; set; }

        /// <summary>Number of articles that would be preserved.</summary>
        public int ArticlesToKeep { get; set; }

        /// <summary>Whether favorites would be kept in this analysis.</summary>
        public bool WouldKeepFavorites { get; set; }

        /// <summary>Whether unread articles would be kept in this analysis.</summary>
        public bool WouldKeepUnread { get; set; }

        /// <summary>Estimated bytes that would be freed.</summary>
        public long EstimatedSpaceFreedBytes { get; set; }

        /// <summary>Distribution of deletions by feed.</summary>
        public Dictionary<string, int> ArticlesByFeed { get; set; } = new();
    }

    /// <summary>
    /// Result of image cache cleanup operation.
    /// </summary>
    public class ImageCacheCleanupResult
    {
        /// <summary>Number of images deleted.</summary>
        public int ImagesDeleted { get; set; }

        /// <summary>Number of images remaining after cleanup.</summary>
        public int ImagesRemaining { get; set; }

        /// <summary>Number of images before cleanup.</summary>
        public int ImagesBeforeCleanup { get; set; }

        /// <summary>Cache size in bytes before cleanup.</summary>
        public long CacheSizeBeforeBytes { get; set; }

        /// <summary>Cache size in bytes after cleanup.</summary>
        public long CacheSizeAfterBytes { get; set; }

        /// <summary>Space freed in bytes.</summary>
        public long SpaceFreedBytes { get; set; }

        /// <summary>Warning messages for files that couldn't be deleted.</summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Result of database integrity check.
    /// </summary>
    public class IntegrityCheckResult
    {
        /// <summary>Whether the database passed integrity verification.</summary>
        public bool IsValid { get; set; }

        /// <summary>Error messages from integrity check.</summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>Warning messages (e.g., foreign key violations).</summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>Duration of the integrity check.</summary>
        public TimeSpan CheckDuration { get; set; }
    }

    /// <summary>
    /// Configuration settings for database cleanup and maintenance operations.
    /// Controls retention policies, scheduling, and optimization behaviors.
    /// </summary>
    public class CleanupConfiguration
    {
        /// <summary>
        /// Gets or sets the number of days to retain articles before deletion.
        /// Articles older than this value are candidates for cleanup.
        /// </summary>
        /// <value>Number of days. Default is 30. Zero or negative values disable article cleanup.</value>
        public int ArticleRetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets a value indicating whether favorite articles should be preserved
        /// regardless of retention period.
        /// </summary>
        /// <value>true to keep favorite articles; otherwise, false. Default is true.</value>
        public bool KeepFavorites { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether unread articles should be preserved
        /// regardless of retention period.
        /// </summary>
        /// <value>true to keep unread articles; otherwise, false. Default is true.</value>
        public bool KeepUnread { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether automatic cleanup is enabled.
        /// When disabled, manual cleanup must be triggered explicitly.
        /// </summary>
        /// <value>true to enable automatic cleanup; otherwise, false. Default is true.</value>
        public bool AutoCleanupEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of days to retain cached images.
        /// </summary>
        /// <value>Number of days. Default is 30.</value>
        public int ImageCacheRetentionDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum size of the image cache in megabytes.
        /// When exceeded, oldest images are removed until under this limit.
        /// </summary>
        /// <value>Maximum size in MB. Default is 500.</value>
        public int MaxImageCacheSizeMB { get; set; } = 500;

        /// <summary>
        /// Gets or sets the hour of day (0-23) when automatic cleanup should run.
        /// </summary>
        /// <value>Hour in 24-hour format. Default is 2 (2:00 AM).</value>
        public int CleanupHourOfDay { get; set; } = 2;

        /// <summary>
        /// Gets or sets the day of week when weekly cleanup should run.
        /// Only applies if cleanup frequency is weekly.
        /// </summary>
        /// <value>DayOfWeek enum value. Default is Sunday.</value>
        public DayOfWeek CleanupDayOfWeek { get; set; } = DayOfWeek.Sunday;

        /// <summary>
        /// Gets or sets a value indicating whether to perform VACUUM operation
        /// after cleanup to reclaim storage space.
        /// </summary>
        /// <value>true to vacuum after cleanup; otherwise, false. Default is true.</value>
        /// <remarks>
        /// VACUUM rebuilds the database file, repacking it into a minimal amount of disk space.
        /// This operation may temporarily double the database file size during execution.
        /// </remarks>
        public bool VacuumAfterCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to rebuild indexes after cleanup.
        /// </summary>
        /// <value>true to rebuild indexes; otherwise, false. Default is false.</value>
        /// <remarks>
        /// Index rebuilding improves query performance but may take significant time
        /// on large databases. Recommended only if performance degradation is observed.
        /// </remarks>
        public bool RebuildIndexesAfterCleanup { get; set; } = false;

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

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        /// <returns>True if configuration is valid; otherwise, false.</returns>
        public bool IsValid()
        {
            return ArticleRetentionDays >= 0 &&
                   ImageCacheRetentionDays > 0 &&
                   MaxImageCacheSizeMB > 0 &&
                   CleanupHourOfDay >= 0 &&
                   CleanupHourOfDay <= 23;
        }

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

        /// <summary>
        /// Preference key constants for application settings storage.
        /// These keys are used with ISettingsService to persist user preferences
        /// and application configuration across sessions.
        /// </summary>
        public static class PreferenceKeys
        {
            #region Article Display & Reading

            /// <summary>Number of days to retain articles before cleanup.</summary>
            public const string ArticleRetentionDays = "article_retention_days";

            /// <summary>Whether to preserve read articles during cleanup (1=true, 0=false).</summary>
            public const string KeepReadArticles = "keep_read_articles";

            /// <summary>Whether to preserve favorite articles during cleanup.</summary>
            public const string KeepFavoriteArticles = "keep_favorite_articles";

            /// <summary>Default sort order for article lists (Date/Title/Feed).</summary>
            public const string ArticleSortOrder = "article_sort_order";

            /// <summary>Default sort direction (Ascending/Descending).</summary>
            public const string ArticleSortDirection = "article_sort_direction";

            /// <summary>Number of articles to load per page/batch.</summary>
            public const string ArticlesPageSize = "articles_page_size";

            /// <summary>Whether to mark articles as read when opened.</summary>
            public const string AutoMarkAsRead = "auto_mark_as_read";

            /// <summary>Delay in seconds before auto-marking as read.</summary>
            public const string AutoMarkAsReadDelay = "auto_mark_as_read_delay";

            /// <summary>Whether to show unread articles count in UI.</summary>
            public const string ShowUnreadCount = "show_unread_count";

            #endregion

            #region Database Cleanup & Maintenance

            /// <summary>Master switch for automatic database cleanup.</summary>
            public const string AutoCleanupEnabled = "auto_cleanup_enabled";

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

            #endregion

            #region Image Cache

            /// <summary>Maximum age in days for cached images.</summary>
            public const string ImageCacheRetentionDays = "image_cache_retention_days";

            /// <summary>Maximum size in MB for image cache.</summary>
            public const string ImageCacheMaxSizeMB = "image_cache_max_size_mb";

            /// <summary>Legacy key for image cache size (deprecated, use ImageCacheMaxSizeMB).</summary>
            public const string ImageCacheSize = "image_cache_size";

            /// <summary>Whether to cache article images locally.</summary>
            public const string EnableImageCache = "enable_image_cache";

            /// <summary>Quality setting for cached images (Low/Medium/High).</summary>
            public const string ImageCacheQuality = "image_cache_quality";

            /// <summary>Whether to preload images for offline reading.</summary>
            public const string PreloadImages = "preload_images";

            #endregion

            #region Feed Management

            /// <summary>Default update interval for feeds in minutes.</summary>
            public const string DefaultUpdateInterval = "default_update_interval";

            /// <summary>Whether to update feeds automatically in background.</summary>
            public const string AutoUpdateFeeds = "auto_update_feeds";

            /// <summary>Whether to allow feed updates only on WiFi.</summary>
            public const string UpdateOnlyOnWiFi = "update_only_on_wifi";

            /// <summary>Timeout in seconds for feed requests.</summary>
            public const string FeedRequestTimeout = "feed_request_timeout";

            /// <summary>Maximum number of concurrent feed updates.</summary>
            public const string MaxConcurrentUpdates = "max_concurrent_updates";

            /// <summary>Whether to show error notifications for failed updates.</summary>
            public const string ShowFeedErrors = "show_feed_errors";

            /// <summary>Number of retry attempts for failed feed updates.</summary>
            public const string FeedUpdateRetries = "feed_update_retries";

            #endregion

            #region UI & Appearance

            /// <summary>Selected application theme (Light/Dark/System).</summary>
            public const string AppTheme = "app_theme";

            /// <summary>Selected accent color.</summary>
            public const string AccentColor = "accent_color";

            /// <summary>Font size for article content (Small/Medium/Large).</summary>
            public const string ArticleFontSize = "article_font_size";

            /// <summary>Font family for article content.</summary>
            public const string ArticleFontFamily = "article_font_family";

            /// <summary>Whether to use compact layout mode.</summary>
            public const string CompactLayout = "compact_layout";

            /// <summary>Width of the feed list pane (for resizable layouts).</summary>
            public const string FeedListWidth = "feed_list_width";

            /// <summary>Whether the reading pane is visible.</summary>
            public const string ReadingPaneVisible = "reading_pane_visible";

            /// <summary>Whether to show article thumbnails in list.</summary>
            public const string ShowThumbnails = "show_thumbnails";

            /// <summary>Whether to enable animations in UI.</summary>
            public const string EnableAnimations = "enable_animations";

            #endregion

            #region Notifications

            /// <summary>Master switch for notifications.</summary>
            public const string NotificationsEnabled = "notifications_enabled";

            /// <summary>Whether to show notifications for new articles.</summary>
            public const string NotifyNewArticles = "notify_new_articles";

            /// <summary>Minimum articles count to trigger notification.</summary>
            public const string NotificationThreshold = "notification_threshold";

            /// <summary>Whether to play sound with notifications.</summary>
            public const string NotificationSound = "notification_sound";

            /// <summary>Whether to use badge notifications on app icon.</summary>
            public const string BadgeNotifications = "badge_notifications";

            #endregion

            #region Search & Filters

            /// <summary>Default search scope (Title/Content/All).</summary>
            public const string DefaultSearchScope = "default_search_scope";

            /// <summary>Whether search should include archived articles.</summary>
            public const string SearchIncludeArchived = "search_include_archived";

            /// <summary>Recent search queries (JSON array).</summary>
            public const string RecentSearches = "recent_searches";

            /// <summary>Maximum number of recent searches to store.</summary>
            public const string MaxRecentSearches = "max_recent_searches";

            #endregion

            #region Import/Export & Data

            /// <summary>Last backup date (Unix timestamp).</summary>
            public const string LastBackupDate = "last_backup_date";

            /// <summary>Automatic backup enabled.</summary>
            public const string AutoBackupEnabled = "auto_backup_enabled";

            /// <summary>Backup interval in days.</summary>
            public const string BackupIntervalDays = "backup_interval_days";

            /// <summary>Backup location path.</summary>
            public const string BackupLocation = "backup_location";

            /// <summary>Whether to include images in backups.</summary>
            public const string BackupIncludeImages = "backup_include_images";

            /// <summary>OPML export last path used.</summary>
            public const string LastExportPath = "last_export_path";

            /// <summary>OPML import last path used.</summary>
            public const string LastImportPath = "last_import_path";

            #endregion

            #region Privacy & Security

            /// <summary>Whether app lock/PIN is enabled.</summary>
            public const string AppLockEnabled = "app_lock_enabled";

            /// <summary>Hashed PIN code for app lock.</summary>
            public const string AppLockPinHash = "app_lock_pin_hash";

            /// <summary>Whether to clear clipboard on app background.</summary>
            public const string ClearClipboardOnBackground = "clear_clipboard_on_background";

            /// <summary>Whether to allow analytics/crash reporting.</summary>
            public const string AnalyticsEnabled = "analytics_enabled";

            #endregion

            #region Advanced/Debug

            /// <summary>Log level (Verbose/Debug/Info/Warning/Error).</summary>
            public const string LogLevel = "log_level";

            /// <summary>Maximum log file size in MB.</summary>
            public const string MaxLogSizeMB = "max_log_size_mb";

            /// <summary>Whether to enable verbose database logging.</summary>
            public const string DatabaseLogging = "database_logging";

            /// <summary>Developer mode enabled (shows advanced options).</summary>
            public const string DeveloperMode = "developer_mode";

            /// <summary>Last app version that was launched (for migrations).</summary>
            public const string LastAppVersion = "last_app_version";

            #endregion
        }
    }

    /// <summary>
    /// Default values for application preferences.
    /// These values are used when a preference has not been explicitly set by the user.
    /// </summary>
    public static class DatabaseCleanupPreferenceDefaults
    {
        #region Article Display & Reading
        public const int ArticleRetentionDays = 30;
        public const int KeepReadArticles = 1; // true
        public const int KeepFavoriteArticles = 1; // true
        public const string ArticleSortOrder = "Date";
        public const string ArticleSortDirection = "Descending";
        public const int ArticlesPageSize = 50;
        public const int AutoMarkAsRead = 1; // true
        public const int AutoMarkAsReadDelay = 3; // seconds
        public const int ShowUnreadCount = 1; // true
        #endregion

        #region Database Cleanup & Maintenance
        public const int AutoCleanupEnabled = 1; // true
        public const int CleanupHourOfDay = 2; // 2:00 AM
        public const int CleanupDayOfWeek = 0; // Sunday
        public const int VacuumAfterCleanup = 1; // true
        public const int RebuildIndexesAfterCleanup = 0; // false
        #endregion

        #region Image Cache
        public const int ImageCacheRetentionDays = 30;
        public const int ImageCacheMaxSizeMB = 500;
        public const int EnableImageCache = 1; // true
        public const string ImageCacheQuality = "Medium";
        public const int PreloadImages = 0; // false
        #endregion

        #region Feed Management
        public const int DefaultUpdateInterval = 60; // minutes
        public const int AutoUpdateFeeds = 1; // true
        public const int UpdateOnlyOnWiFi = 0; // false
        public const int FeedRequestTimeout = 30; // seconds
        public const int MaxConcurrentUpdates = 5;
        public const int ShowFeedErrors = 1; // true
        public const int FeedUpdateRetries = 3;
        #endregion

        #region UI & Appearance
        public const string AppTheme = "System";
        public const string AccentColor = "Blue";
        public const string ArticleFontSize = "Medium";
        public const string ArticleFontFamily = "Segoe UI";
        public const int CompactLayout = 0; // false
        public const int FeedListWidth = 280; // pixels
        public const int ReadingPaneVisible = 1; // true
        public const int ShowThumbnails = 1; // true
        public const int EnableAnimations = 1; // true
        #endregion

        #region Notifications
        public const int NotificationsEnabled = 1; // true
        public const int NotifyNewArticles = 1; // true
        public const int NotificationThreshold = 5; // articles
        public const int NotificationSound = 0; // false
        public const int BadgeNotifications = 1; // true
        #endregion

        #region Search & Filters
        public const string DefaultSearchScope = "All";
        public const int SearchIncludeArchived = 0; // false
        public const int MaxRecentSearches = 10;
        #endregion

        #region Import/Export & Data
        public const int AutoBackupEnabled = 0; // false
        public const int BackupIntervalDays = 7;
        public const string BackupLocation = ""; // Default to app data
        public const int BackupIncludeImages = 0; // false
        #endregion

        #region Privacy & Security
        public const int AppLockEnabled = 0; // false
        public const int ClearClipboardOnBackground = 0; // false
        public const int AnalyticsEnabled = 1; // true
        #endregion

        #region Advanced/Debug
        public const string LogLevel = "Info";
        public const int MaxLogSizeMB = 10;
        public const int DatabaseLogging = 0; // false
        public const int DeveloperMode = 0; // false
        #endregion
    }

    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupStarting"/> event.
    /// Contains configuration and timing information about the cleanup operation that is about to begin.
    /// </summary>
    public class CleanupStartingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupStartingEventArgs"/> class.
        /// </summary>
        /// <param name="configuration">The cleanup configuration that will be used for this operation.</param>
        /// <param name="startTime">The UTC timestamp when the cleanup operation is starting.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
        public CleanupStartingEventArgs(CleanupConfiguration configuration, DateTime startTime)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            StartTime = startTime;
        }

        /// <summary>
        /// Gets the cleanup configuration that will be used for this operation.
        /// </summary>
        /// <value>The <see cref="CleanupConfiguration"/> containing retention policies and options.</value>
        public CleanupConfiguration Configuration { get; }

        /// <summary>
        /// Gets the UTC timestamp when the cleanup operation is starting.
        /// </summary>
        /// <value>The start time in UTC.</value>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets a value indicating whether this is an automatic scheduled cleanup
        /// or a manually triggered cleanup.
        /// </summary>
        /// <value>true if automatic cleanup; otherwise, false.</value>
        public bool IsAutomatic => Configuration.AutoCleanupEnabled;

        /// <summary>
        /// Gets a summary of the cleanup operation for logging or display purposes.
        /// </summary>
        /// <returns>A formatted string describing the cleanup operation.</returns>
        public override string ToString()
        {
            return $"Cleanup starting at {StartTime:yyyy-MM-dd HH:mm:ss UTC} - " +
                   $"Retention: {Configuration.ArticleRetentionDays} days, " +
                   $"Keep Favorites: {Configuration.KeepFavorites}, " +
                   $"Keep Unread: {Configuration.KeepUnread}";
        }
    }

    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupProgress"/> event.
    /// Reports progress updates during a cleanup operation.
    /// </summary>
    public class CleanupProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupProgressEventArgs"/> class.
        /// </summary>
        /// <param name="currentStep">Description of the current cleanup step being executed.</param>
        /// <param name="stepNumber">The current step number (1-based).</param>
        /// <param name="totalSteps">The total number of steps in the cleanup process.</param>
        public CleanupProgressEventArgs(string currentStep, int stepNumber, int totalSteps)
        {
            CurrentStep = currentStep ?? throw new ArgumentNullException(nameof(currentStep));
            StepNumber = stepNumber;
            TotalSteps = totalSteps;
        }

        /// <summary>
        /// Gets the description of the current cleanup step.
        /// </summary>
        public string CurrentStep { get; }

        /// <summary>
        /// Gets the current step number (1-based).
        /// </summary>
        public int StepNumber { get; }

        /// <summary>
        /// Gets the total number of steps in the cleanup process.
        /// </summary>
        public int TotalSteps { get; }

        /// <summary>
        /// Gets the completion percentage (0-100).
        /// </summary>
        public int PercentComplete => TotalSteps > 0 ? (StepNumber * 100) / TotalSteps : 0;

        /// <summary>
        /// Gets a value indicating whether this is the final step.
        /// </summary>
        public bool IsFinalStep => StepNumber >= TotalSteps;
    }

    /// <summary>
    /// Provides data for the <see cref="IDatabaseCleanupService.OnCleanupCompleted"/> event.
    /// Contains the final results of the cleanup operation.
    /// </summary>
    public class CleanupCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupCompletedEventArgs"/> class.
        /// </summary>
        /// <param name="result">The result of the cleanup operation.</param>
        /// <param name="completionTime">The UTC timestamp when the cleanup completed.</param>
        /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
        public CleanupCompletedEventArgs(CleanupResult result, DateTime completionTime)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            CompletionTime = completionTime;
        }

        /// <summary>
        /// Gets the result of the cleanup operation.
        /// </summary>
        public CleanupResult Result { get; }

        /// <summary>
        /// Gets the UTC timestamp when the cleanup operation completed.
        /// </summary>
        public DateTime CompletionTime { get; }

        /// <summary>
        /// Gets a value indicating whether the cleanup completed successfully.
        /// </summary>
        public bool Success => Result.Success;

        /// <summary>
        /// Gets the duration of the cleanup operation.
        /// </summary>
        public TimeSpan Duration => Result.Duration;

        /// <summary>
        /// Gets a summary of the completed cleanup for logging purposes.
        /// </summary>
        /// <returns>A formatted string describing the cleanup completion.</returns>
        public override string ToString()
        {
            var status = Result.Success ? "completed successfully" : "failed";
            return $"Cleanup {status} at {CompletionTime:yyyy-MM-dd HH:mm:ss UTC} " +
                   $"(Duration: {Duration.TotalSeconds:F1}s, " +
                   $"Articles deleted: {Result.ArticleCleanup?.ArticlesDeleted ?? 0}, " +
                   $"Space freed: {Result.SpaceFreedBytes / (1024 * 1024):F1} MB)";
        }
    }

    /// <summary>
    /// Represents the comprehensive results of a database cleanup operation.
    /// Aggregates results from all sub-operations including article deletion,
    /// orphan removal, vacuum, and index rebuild.
    /// </summary>
    public class CleanupResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupResult"/> class.
        /// </summary>
        public CleanupResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            OrphanCleanup = new OrphanRemovalResult();
            ImageCacheCleanup = new ImageCacheCleanupResult();
            VacuumResult = new VacuumResult();
        }

        #region Status Properties

        /// <summary>
        /// Gets or sets a value indicating whether the cleanup operation completed successfully.
        /// </summary>
        /// <value>true if the operation succeeded; otherwise, false.</value>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the cleanup was skipped 
        /// (e.g., if auto-cleanup is disabled).
        /// </summary>
        /// <value>true if the operation was skipped; otherwise, false.</value>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the cleanup operation was performed.
        /// </summary>
        public DateTime PerformedAt { get; set; }

        /// <summary>
        /// Gets or sets the total duration of the cleanup operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        #endregion

        #region Operation Results

        /// <summary>
        /// Gets or sets the detailed results of the article cleanup sub-operation.
        /// Contains information about deleted articles and retention statistics.
        /// </summary>
        /// <value>An <see cref="ArticleCleanupResult"/> instance, or null if article cleanup was not performed.</value>
        public ArticleDeletionResult? ArticleCleanup { get; set; }

        /// <summary>
        /// Gets or sets the detailed results of the orphan record removal sub-operation.
        /// Contains counts of removed orphaned records from junction tables.
        /// </summary>
        /// <value>An <see cref="OrphanCleanupResult"/> instance, or null if orphan cleanup was not performed.</value>
        public OrphanRemovalResult OrphanCleanup { get; set; }

        /// <summary>
        /// Gets or sets the results of the image cache cleanup sub-operation.
        /// Contains information about deleted images and space reclaimed.
        /// </summary>
        /// <value>An <see cref="ImageCacheCleanupResult"/> instance, or null if image cache cleanup was not performed.</value>
        public ImageCacheCleanupResult ImageCacheCleanup { get; set; }

        /// <summary>
        /// Gets or sets the results of the database vacuum sub-operation.
        /// Contains space statistics before and after the vacuum.
        /// </summary>
        /// <value>A <see cref="VacuumResult"/> instance, or null if vacuum was not performed.</value>
        public VacuumResult VacuumResult { get; set; }

        #endregion

        #region Aggregated Statistics

        /// <summary>
        /// Gets the total number of articles deleted during the cleanup.
        /// </summary>
        /// <value>Total count of deleted articles from all operations.</value>
        public int TotalArticlesDeleted =>
            (ArticleCleanup?.ArticlesDeleted ?? 0) +
            (OrphanCleanup?.OrphanedArticlesRemoved ?? 0);

        /// <summary>
        /// Gets the total number of records deleted (articles + orphans + tags).
        /// </summary>
        /// <value>Total count of all deleted records.</value>
        public int TotalRecordsDeleted =>
            (ArticleCleanup?.ArticlesDeleted ?? 0) +
            (OrphanCleanup?.TotalRecordsRemoved ?? 0);

        /// <summary>
        /// Gets or sets the total space freed in bytes across all operations.
        /// </summary>
        /// <value>Space freed in bytes.</value>
        public long SpaceFreedBytes { get; set; }

        /// <summary>
        /// Gets the total space freed in megabytes.
        /// </summary>
        /// <value>Space freed in megabytes (2 decimal places precision).</value>
        public double SpaceFreedMB => SpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the total space freed in a human-readable format.
        /// </summary>
        /// <returns>Formatted string with appropriate unit (B, KB, MB, GB).</returns>
        public string SpaceFreedFormatted
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                return SpaceFreedBytes switch
                {
                    >= GB => $"{SpaceFreedBytes / (double)GB:F2} GB",
                    >= MB => $"{SpaceFreedBytes / (double)MB:F2} MB",
                    >= KB => $"{SpaceFreedBytes / (double)KB:F2} KB",
                    _ => $"{SpaceFreedBytes} B"
                };
            }
        }

        #endregion

        #region Error Handling

        /// <summary>
        /// Gets the collection of error messages encountered during the cleanup.
        /// </summary>
        /// <value>List of error strings. Empty if no errors occurred.</value>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the collection of warning messages encountered during the cleanup.
        /// </summary>
        /// <value>List of warning strings. Empty if no warnings occurred.</value>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets a value indicating whether any errors occurred during cleanup.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Gets a value indicating whether any warnings occurred during cleanup.
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Adds an error message to the result.
        /// </summary>
        /// <param name="error">The error message to add.</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            Success = false;
        }

        /// <summary>
        /// Adds a warning message to the result.
        /// </summary>
        /// <param name="warning">The warning message to add.</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a summary report of the cleanup operation.
        /// </summary>
        /// <returns>A formatted multi-line string suitable for logging or display.</returns>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Database Cleanup Report ===",
                $"Status: {(Success ? "Success" : HasErrors ? "Failed" : "Unknown")}",
                $"Performed At: {PerformedAt:yyyy-MM-dd HH:mm:ss UTC}",
                $"Duration: {Duration.TotalSeconds:F2} seconds",
                "",
                "--- Articles ---",
                $"Articles Deleted: {ArticleCleanup?.ArticlesDeleted ?? 0}",
                $"Orphaned Records Removed: {OrphanCleanup?.TotalRecordsRemoved ?? 0}",
                "",
                "--- Space ---",
                $"Space Freed: {SpaceFreedFormatted}",
                "",
                "--- Errors & Warnings ---",
                $"Errors: {Errors.Count}",
                $"Warnings: {Warnings.Count}"
            };

            if (HasErrors)
            {
                lines.Add("");
                lines.Add("Error Details:");
                lines.AddRange(Errors.Select(e => $"  - {e}"));
            }

            if (HasWarnings)
            {
                lines.Add("");
                lines.Add("Warning Details:");
                lines.AddRange(Warnings.Select(w => $"  - {w}"));
            }

            lines.Add("");
            lines.Add("==============================");

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of the cleanup result.
        /// </summary>
        /// <returns>A single-line summary string.</returns>
        public override string ToString()
        {
            if (Skipped)
                return $"Cleanup skipped at {PerformedAt:yyyy-MM-dd HH:mm}";

            var status = Success ? "Success" : "Failed";
            return $"{status}: {TotalArticlesDeleted} articles deleted, {SpaceFreedFormatted} freed in {Duration.TotalSeconds:F1}s";
        }

        #endregion
    }

    /// <summary>
    /// Represents the results of the article cleanup sub-operation.
    /// </summary>
    public class ArticleCleanupResult
    {
        /// <summary>
        /// Gets or sets the total number of articles before cleanup started.
        /// </summary>
        public int TotalArticlesBefore { get; set; }

        /// <summary>
        /// Gets or sets the number of articles deleted.
        /// </summary>
        public int ArticlesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the number of articles remaining after cleanup.
        /// </summary>
        public int ArticlesRemaining => TotalArticlesBefore - ArticlesDeleted;

        /// <summary>
        /// Gets or sets the publication date of the oldest deleted article.
        /// </summary>
        public DateTime? OldestArticleDeleted { get; set; }

        /// <summary>
        /// Gets or sets the publication date of the newest deleted article.
        /// </summary>
        public DateTime? NewestArticleDeleted { get; set; }

        /// <summary>
        /// Gets or sets the cutoff date used for deletion.
        /// </summary>
        public DateTime CutoffDateUsed { get; set; }

        /// <summary>
        /// Gets a value indicating whether any articles were deleted.
        /// </summary>
        public bool AnyDeleted => ArticlesDeleted > 0;
    }

    /// <summary>
    /// Represents the results of the orphaned record removal operation.
    /// Tracks the number of orphaned records removed from junction tables
    /// and entities that have lost their parent references.
    /// </summary>
    /// <remarks>
    /// Orphaned records are records that reference non-existent parent entities,
    /// typically caused by cascading delete failures or data corruption.
    /// Examples include ArticleTags referencing deleted Articles or Tags,
    /// or Articles referencing deleted Feeds.
    /// </remarks>
    public class OrphanCleanupResult
    {
        #region Individual Counters

        /// <summary>
        /// Gets or sets the number of orphaned article-tag associations removed.
        /// These are records in the ArticleTags junction table where either
        /// the ArticleId references a non-existent Article, or the TagId
        /// references a non-existent Tag.
        /// </summary>
        /// <value>Count of deleted ArticleTags records.</value>
        public int OrphanedArticleTagsRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned articles removed.
        /// These are articles whose FeedId references a feed that no longer exists.
        /// </summary>
        /// <value>Count of deleted Articles records.</value>
        public int OrphanedArticlesRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned categories removed.
        /// These are categories that are no longer referenced by any feed.
        /// </summary>
        /// <value>Count of deleted Categories records.</value>
        public int OrphanedCategoriesRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned feed-category associations removed.
        /// These are feed-category mappings where the category no longer exists.
        /// </summary>
        /// <value>Count of deleted orphaned feed-category associations.</value>
        public int OrphanedFeedCategoriesRemoved { get; set; }

        /// <summary>
        /// Gets or sets the number of orphaned attachment records removed.
        /// These are attachments (images, enclosures) whose parent article no longer exists.
        /// </summary>
        /// <value>Count of deleted attachment records.</value>
        public int OrphanedAttachmentsRemoved { get; set; }

        /// <summary>
        /// Gets the total number of orphaned records removed.
        /// </summary>
        public int TotalRecordsDeleted => OrphanedArticleTagsRemoved + OrphanedArticlesRemoved;

        #endregion

        #region Aggregated Statistics

        /// <summary>
        /// Gets the total number of orphaned records removed across all tables.
        /// </summary>
        /// <value>Sum of all individual orphan removal counters.</value>
        public int TotalRecordsRemoved =>
            OrphanedArticleTagsRemoved +
            OrphanedArticlesRemoved +
            OrphanedCategoriesRemoved +
            OrphanedFeedCategoriesRemoved +
            OrphanedAttachmentsRemoved;

        /// <summary>
        /// Gets a value indicating whether any orphaned records were found and removed.
        /// </summary>
        /// <value>true if at least one orphan was removed; otherwise, false.</value>
        public bool AnyRemoved => TotalRecordsRemoved > 0;

        /// <summary>
        /// Gets a breakdown of orphans by type for detailed reporting.
        /// </summary>
        /// <returns>Dictionary mapping orphan type to count.</returns>
        public Dictionary<string, int> GetBreakdown()
        {
            var breakdown = new Dictionary<string, int>();

            if (OrphanedArticleTagsRemoved > 0)
                breakdown["ArticleTags"] = OrphanedArticleTagsRemoved;

            if (OrphanedArticlesRemoved > 0)
                breakdown["Articles"] = OrphanedArticlesRemoved;

            if (OrphanedCategoriesRemoved > 0)
                breakdown["Categories"] = OrphanedCategoriesRemoved;

            if (OrphanedFeedCategoriesRemoved > 0)
                breakdown["FeedCategories"] = OrphanedFeedCategoriesRemoved;

            if (OrphanedAttachmentsRemoved > 0)
                breakdown["Attachments"] = OrphanedAttachmentsRemoved;

            return breakdown;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a human-readable summary of the orphan cleanup operation.
        /// </summary>
        /// <returns>A formatted string describing what was cleaned.</returns>
        public override string ToString()
        {
            if (!AnyRemoved)
                return "No orphaned records found";

            var parts = new List<string>();

            if (OrphanedArticleTagsRemoved > 0)
                parts.Add($"{OrphanedArticleTagsRemoved} article-tag associations");

            if (OrphanedArticlesRemoved > 0)
                parts.Add($"{OrphanedArticlesRemoved} orphaned articles");

            if (OrphanedCategoriesRemoved > 0)
                parts.Add($"{OrphanedCategoriesRemoved} orphaned categories");

            if (OrphanedFeedCategoriesRemoved > 0)
                parts.Add($"{OrphanedFeedCategoriesRemoved} feed-category mappings");

            if (OrphanedAttachmentsRemoved > 0)
                parts.Add($"{OrphanedAttachmentsRemoved} orphaned attachments");

            return $"Removed {TotalRecordsRemoved} orphaned records: {string.Join(", ", parts)}";
        }

        /// <summary>
        /// Generates a detailed report suitable for logging.
        /// </summary>
        /// <returns>Multi-line string with detailed breakdown.</returns>
        public string ToDetailedString()
        {
            var lines = new List<string>
            {
                "=== Orphan Cleanup Results ===",
                $"Total Records Removed: {TotalRecordsRemoved}",
                "",
                "Breakdown by Type:",
                $"  - ArticleTags (orphaned):     {OrphanedArticleTagsRemoved,5}",
                $"  - Articles (no feed):         {OrphanedArticlesRemoved,5}",
                $"  - Categories (unused):        {OrphanedCategoriesRemoved,5}",
                $"  - Feed-Category mappings:     {OrphanedFeedCategoriesRemoved,5}",
                $"  - Attachments (no article):     {OrphanedAttachmentsRemoved,5}",
                "=============================="
            };

            return string.Join(Environment.NewLine, lines);
        }

        #endregion
    }

    /// <summary>
    /// Represents comprehensive database statistics for monitoring and diagnostics.
    /// Provides metrics on articles, feeds, tags, and database file size.
    /// </summary>
    public class DatabaseStatistics
    {
        #region Metadata

        /// <summary>
        /// Gets or sets the UTC timestamp when these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets the age of these statistics (time elapsed since generation).
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - GeneratedAt;

        /// <summary>
        /// Gets a value indicating whether these statistics are stale (older than 1 hour).
        /// </summary>
        public bool IsStale => Age.TotalHours > 1;

        #endregion

        #region Article Statistics

        /// <summary>
        /// Gets or sets the total number of articles in the database.
        /// </summary>
        public int TotalArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as read.
        /// </summary>
        public int ReadArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as unread.
        /// </summary>
        public int UnreadArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as favorite.
        /// </summary>
        public int FavoriteArticles { get; set; }

        /// <summary>
        /// Gets the read ratio (percentage of articles that have been read).
        /// </summary>
        public double ReadRatio => TotalArticles > 0 ? (double)ReadArticles / TotalArticles : 0;

        /// <summary>
        /// Gets the unread ratio (percentage of articles that are unread).
        /// </summary>
        public double UnreadRatio => TotalArticles > 0 ? (double)UnreadArticles / TotalArticles : 0;

        /// <summary>
        /// Gets the favorite ratio (percentage of articles that are favorites).
        /// </summary>
        public double FavoriteRatio => TotalArticles > 0 ? (double)FavoriteArticles / TotalArticles : 0;

        #endregion

        #region Article Age Distribution

        /// <summary>
        /// Gets or sets the number of articles older than 30 days.
        /// </summary>
        public int ArticlesOlderThan30Days { get; set; }

        /// <summary>
        /// Gets or sets the number of articles older than 60 days.
        /// </summary>
        public int ArticlesOlderThan60Days { get; set; }

        /// <summary>
        /// Gets or sets the number of articles older than 90 days.
        /// </summary>
        public int ArticlesOlderThan90Days { get; set; }

        /// <summary>
        /// Gets the publication date of the oldest article in the database.
        /// </summary>
        public DateTime OldestArticleDate { get; set; }

        /// <summary>
        /// Gets the publication date of the newest article in the database.
        /// </summary>
        public DateTime NewestArticleDate { get; set; }

        /// <summary>
        /// Gets the time span covered by articles (newest - oldest).
        /// </summary>
        public TimeSpan ArticleTimeSpan => NewestArticleDate - OldestArticleDate;

        #endregion

        #region Feed Statistics

        /// <summary>
        /// Gets or sets the total number of feeds.
        /// </summary>
        public int TotalFeeds { get; set; }

        /// <summary>
        /// Gets or sets the number of active feeds.
        /// </summary>
        public int ActiveFeeds { get; set; }

        /// <summary>
        /// Gets or sets the number of inactive feeds.
        /// </summary>
        public int InactiveFeeds => TotalFeeds - ActiveFeeds;

        /// <summary>
        /// Gets the average number of articles per feed.
        /// </summary>
        public double AverageArticlesPerFeed => TotalFeeds > 0 ? (double)TotalArticles / TotalFeeds : 0;

        /// <summary>
        /// Gets the activity ratio (percentage of feeds that are active).
        /// </summary>
        public double FeedActivityRatio => TotalFeeds > 0 ? (double)ActiveFeeds / TotalFeeds : 0;

        #endregion

        #region Tag Statistics

        /// <summary>
        /// Gets or sets the total number of tags.
        /// </summary>
        public int TotalTags { get; set; }

        /// <summary>
        /// Gets the average number of articles per tag.
        /// </summary>
        public double AverageArticlesPerTag => TotalTags > 0 ? (double)TotalArticles / TotalTags : 0;

        #endregion

        #region Database File Statistics

        /// <summary>
        /// Gets or sets the physical size of the database file in bytes.
        /// </summary>
        public long DatabaseSizeBytes { get; set; }

        /// <summary>
        /// Gets the database size in kilobytes.
        /// </summary>
        public double DatabaseSizeKB => DatabaseSizeBytes / 1024.0;

        /// <summary>
        /// Gets the database size in megabytes.
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the database size in gigabytes.
        /// </summary>
        public double DatabaseSizeGB => DatabaseSizeBytes / (1024.0 * 1024.0 * 1024.0);

        /// <summary>
        /// Gets the database size in a human-readable format.
        /// </summary>
        public string DatabaseSizeFormatted
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                return DatabaseSizeBytes switch
                {
                    >= GB => $"{DatabaseSizeGB:F2} GB",
                    >= MB => $"{DatabaseSizeMB:F2} MB",
                    >= KB => $"{DatabaseSizeKB:F2} KB",
                    _ => $"{DatabaseSizeBytes} B"
                };
            }
        }

        /// <summary>
        /// Gets the average size per article in bytes.
        /// </summary>
        public double AverageArticleSizeBytes => TotalArticles > 0 ? (double)DatabaseSizeBytes / TotalArticles : 0;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a comprehensive report of database statistics.
        /// </summary>
        /// <returns>A formatted multi-line string suitable for logging or display.</returns>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Database Statistics Report ===",
                $"Generated At: {GeneratedAt:yyyy-MM-dd HH:mm:ss UTC} (Age: {Age.TotalMinutes:F0} minutes)",
                "",
                "--- Articles ---",
                $"Total Articles:     {TotalArticles,10:N0}",
                $"  Read:             {ReadArticles,10:N0} ({ReadRatio:P1})",
                $"  Unread:           {UnreadArticles,10:N0} ({UnreadRatio:P1})",
                $"  Favorites:        {FavoriteArticles,10:N0} ({FavoriteRatio:P1})",
                $"",
                $"Age Distribution:",
                $"  > 30 days:        {ArticlesOlderThan30Days,10:N0}",
                $"  > 60 days:        {ArticlesOlderThan60Days,10:N0}",
                $"  > 90 days:        {ArticlesOlderThan90Days,10:N0}",
                $"  Oldest:           {OldestArticleDate:yyyy-MM-dd}",
                $"  Newest:           {NewestArticleDate:yyyy-MM-dd}",
                $"  Time Span:        {ArticleTimeSpan.TotalDays:F0} days",
                "",
                "--- Feeds ---",
                $"Total Feeds:        {TotalFeeds,10:N0}",
                $"  Active:           {ActiveFeeds,10:N0} ({FeedActivityRatio:P1})",
                $"  Inactive:         {InactiveFeeds,10:N0}",
                $"Avg Articles/Feed:  {AverageArticlesPerFeed:F1}",
                "",
                "--- Tags ---",
                $"Total Tags:         {TotalTags,10:N0}",
                $"Avg Articles/Tag:   {AverageArticlesPerTag:F1}",
                "",
                "--- Storage ---",
                $"Database Size:      {DatabaseSizeFormatted,10}",
                $"Avg Article Size:   {AverageArticleSizeBytes:F0} bytes",
                "=================================="
            };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of database statistics.
        /// </summary>
        /// <returns>A single-line summary string.</returns>
        public override string ToString()
        {
            return $"{TotalArticles:N0} articles, {TotalFeeds:N0} feeds ({ActiveFeeds:N0} active), " +
                   $"{TotalTags:N0} tags, {DatabaseSizeFormatted}";
        }

        #endregion
    }

    /// <summary>
    /// Represents the projected impact analysis of a database cleanup operation.
    /// Provides estimates of what would be deleted and how much space would be freed
    /// without actually performing the deletion.
    /// </summary>
    /// <remarks>
    /// This class is used to preview cleanup effects before committing to the operation,
    /// allowing users to make informed decisions about retention policies.
    /// </remarks>
    public class CleanupAnalysis
    {
        #region Configuration Used

        /// <summary>
        /// Gets or sets the retention period in days that was used for this analysis.
        /// </summary>
        /// <value>Number of days of retention.</value>
        public int RetentionDays { get; set; }

        /// <summary>
        /// Gets or sets the cutoff date calculated from the retention period.
        /// Articles older than this date would be deleted.
        /// </summary>
        /// <value>The cutoff date in UTC.</value>
        public DateTime CutoffDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether favorite articles would be preserved.
        /// </summary>
        /// <value>true if favorites would be kept; otherwise, false.</value>
        public bool WouldKeepFavorites { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether unread articles would be preserved.
        /// </summary>
        /// <value>true if unread articles would be kept; otherwise, false.</value>
        public bool WouldKeepUnread { get; set; }

        #endregion

        #region Impact Projections

        /// <summary>
        /// Gets or sets the number of articles that would be deleted.
        /// </summary>
        /// <value>Projected count of articles to delete.</value>
        public int ArticlesToDelete { get; set; }

        /// <summary>
        /// Gets or sets the number of articles that would be preserved.
        /// </summary>
        /// <value>Projected count of articles to keep.</value>
        public int ArticlesToKeep { get; set; }

        /// <summary>
        /// Gets the total number of articles analyzed.
        /// </summary>
        /// <value>Sum of articles to delete and keep.</value>
        public int TotalArticles => ArticlesToDelete + ArticlesToKeep;

        /// <summary>
        /// Gets or sets the estimated space that would be freed in bytes.
        /// </summary>
        /// <value>Estimated bytes to be freed.</value>
        /// <remarks>
        /// This is a rough estimate based on average article size (typically ~5KB per article).
        /// Actual space freed may vary depending on database fragmentation.
        /// </remarks>
        public long EstimatedSpaceFreedBytes { get; set; }

        /// <summary>
        /// Gets the estimated space freed in megabytes.
        /// </summary>
        public double EstimatedSpaceFreedMB => EstimatedSpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the estimated space freed in a human-readable format.
        /// </summary>
        public string EstimatedSpaceFreedFormatted
        {
            get
            {
                const long KB = 1024;
                const long MB = KB * 1024;
                const long GB = MB * 1024;

                return EstimatedSpaceFreedBytes switch
                {
                    >= GB => $"{EstimatedSpaceFreedBytes / (double)GB:F2} GB",
                    >= MB => $"{EstimatedSpaceFreedMB:F2} MB",
                    >= KB => $"{EstimatedSpaceFreedBytes / (double)KB:F2} KB",
                    _ => $"{EstimatedSpaceFreedBytes} B"
                };
            }
        }

        /// <summary>
        /// Gets the percentage of articles that would be deleted.
        /// </summary>
        public double DeletionPercentage => TotalArticles > 0 ? (double)ArticlesToDelete / TotalArticles : 0;

        #endregion

        #region Distribution Analysis

        /// <summary>
        /// Gets or sets the distribution of articles to be deleted by feed.
        /// Key is the feed title, value is the count of articles from that feed.
        /// </summary>
        /// <value>Dictionary mapping feed titles to article counts.</value>
        public Dictionary<string, int> ArticlesByFeed { get; set; } = new();

        /// <summary>
        /// Gets the feed with the most articles to be deleted.
        /// </summary>
        public KeyValuePair<string, int>? MostAffectedFeed
        {
            get
            {
                if (ArticlesByFeed.Count == 0) return null;
                var max = ArticlesByFeed.MaxBy(kvp => kvp.Value);
                return max;
            }
        }

        /// <summary>
        /// Gets the number of unique feeds that would be affected.
        /// </summary>
        public int AffectedFeedsCount => ArticlesByFeed.Count;

        #endregion

        #region Risk Assessment

        /// <summary>
        /// Gets a value indicating whether this cleanup would be considered high impact.
        /// </summary>
        /// <value>true if more than 50% of articles would be deleted.</value>
        public bool IsHighImpact => DeletionPercentage > 0.5;

        /// <summary>
        /// Gets a value indicating whether this cleanup would be considered safe.
        /// </summary>
        /// <value>true if less than 10% of articles would be deleted.</value>
        public bool IsLowImpact => DeletionPercentage < 0.1;

        /// <summary>
        /// Gets a risk level assessment for this cleanup operation.
        /// </summary>
        public CleanupRiskLevel RiskLevel => DeletionPercentage switch
        {
            > 0.75 => CleanupRiskLevel.Critical,
            > 0.50 => CleanupRiskLevel.High,
            > 0.25 => CleanupRiskLevel.Medium,
            > 0.10 => CleanupRiskLevel.Low,
            _ => CleanupRiskLevel.Minimal
        };

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a detailed report of the cleanup analysis.
        /// </summary>
        /// <returns>A formatted multi-line string suitable for display to users.</returns>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Cleanup Impact Analysis ===",
                $"Analysis Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}",
                "",
                "--- Configuration ---",
                $"Retention Period:   {RetentionDays} days",
                $"Cutoff Date:        {CutoffDate:yyyy-MM-dd}",
                $"Keep Favorites:     {(WouldKeepFavorites ? "Yes" : "No")}",
                $"Keep Unread:        {(WouldKeepUnread ? "Yes" : "No")}",
                "",
                "--- Impact Summary ---",
                $"Articles to Delete: {ArticlesToDelete,10:N0} ({DeletionPercentage:P1})",
                $"Articles to Keep:   {ArticlesToKeep,10:N0} ({1 - DeletionPercentage:P1})",
                $"Total Articles:     {TotalArticles,10:N0}",
                $"",
                $"Space to be Freed:  {EstimatedSpaceFreedFormatted}",
                $"Risk Level:         {RiskLevel}",
                ""
            };

            if (ArticlesByFeed.Count > 0)
            {
                lines.Add("--- Distribution by Feed ---");
                foreach (var feed in ArticlesByFeed.OrderByDescending(x => x.Value).Take(10))
                {
                    lines.Add($"  {feed.Key,-30} {feed.Value,8:N0} articles");
                }

                if (ArticlesByFeed.Count > 10)
                {
                    lines.Add($"  ... and {ArticlesByFeed.Count - 10} more feeds");
                }
                lines.Add("");
            }

            lines.Add("==============================");

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of the analysis.
        /// </summary>
        /// <returns>A single-line summary string.</returns>
        public override string ToString()
        {
            return $"{ArticlesToDelete:N0} of {TotalArticles:N0} articles would be deleted " +
                   $"({DeletionPercentage:P1}), freeing ~{EstimatedSpaceFreedFormatted}";
        }

        #endregion
    }

    /// <summary>
    /// Risk levels for cleanup operations.
    /// </summary>
    public enum CleanupRiskLevel
    {
        /// <summary>Minimal risk, less than 10% deletion.</summary>
        Minimal,

        /// <summary>Low risk, 10-25% deletion.</summary>
        Low,

        /// <summary>Medium risk, 25-50% deletion.</summary>
        Medium,

        /// <summary>High risk, 50-75% deletion.</summary>
        High,

        /// <summary>Critical risk, more than 75% deletion.</summary>
        Critical
    }

}