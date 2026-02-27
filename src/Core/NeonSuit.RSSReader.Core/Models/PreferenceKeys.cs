namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Complete preference keys for all application features.
    /// Provides centralized key management to prevent typos and ensure consistency.
    /// </summary>
    public static class PreferenceKeys
    {
        #region UI & Appearance

        /// <summary>UI theme: "light", "dark", "system", "custom"</summary>
        public const string Theme = "theme";

        /// <summary>Accent color in hex format: "#FF4CAF50"</summary>
        public const string AccentColor = "accent_color";

        /// <summary>Default view mode: "list", "grid", "magazine", "compact"</summary>
        public const string DefaultView = "default_view";

        /// <summary>Font family for article display</summary>
        public const string ArticleFont = "article_font";

        /// <summary>Font size: "small", "normal", "large", "xlarge"</summary>
        public const string ArticleFontSize = "article_font_size";

        /// <summary>UI density: "compact", "normal", "comfortable"</summary>
        public const string UiDensity = "ui_density";

        /// <summary>Whether to show the sidebar</summary>
        public const string ShowSidebar = "show_sidebar";

        /// <summary>Sidebar width in pixels</summary>
        public const string SidebarWidth = "sidebar_width";

        /// <summary>Whether UI animations are enabled</summary>
        public const string AnimationEnabled = "animation_enabled";

        #endregion

        #region Article Reading

        /// <summary>Whether to automatically mark articles as read when viewed</summary>
        public const string AutoMarkAsRead = "auto_mark_as_read";

        /// <summary>Delay in seconds before auto-marking as read</summary>
        public const string AutoMarkAsReadDelay = "auto_mark_as_read_delay";

        /// <summary>Whether to show images in articles</summary>
        public const string ShowImages = "show_images";

        /// <summary>Whether to lazy load images for performance</summary>
        public const string LazyLoadImages = "lazy_load_images";

        /// <summary>Reader view mode: "full", "clean", "original"</summary>
        public const string ReaderViewMode = "reader_view_mode";

        /// <summary>Text justification: "left", "justify"</summary>
        public const string TextJustification = "text_justification";

        /// <summary>Line height multiplier (1.0 - 2.0)</summary>
        public const string LineHeight = "line_height";

        /// <summary>Whether night mode is enabled</summary>
        public const string NightMode = "night_mode";

        /// <summary>Night mode schedule: "sunset", "21:00", "manual"</summary>
        public const string NightModeSchedule = "night_mode_schedule";

        #endregion

        #region Navigation & Links

        /// <summary>Whether to open links inside the app</summary>
        public const string OpenLinksInApp = "open_links_in_app";

        /// <summary>Whether to open links in background tabs</summary>
        public const string OpenLinksBackground = "open_links_background";

        /// <summary>Whether to use an external browser</summary>
        public const string UseExternalBrowser = "use_external_browser";

        /// <summary>Path to external browser executable</summary>
        public const string ExternalBrowserPath = "external_browser_path";

        /// <summary>Whether middle-click opens links in background</summary>
        public const string MiddleClickOpensBackground = "middle_click_opens_background";

        /// <summary>Whether to mark articles as read when scrolling past them</summary>
        public const string MarkAsReadOnScroll = "mark_as_read_on_scroll";

        #endregion

        #region Feed Management

        /// <summary>Default update frequency in minutes</summary>
        public const string DefaultUpdateFrequency = "default_update_frequency";

        /// <summary>Whether to update feeds on application startup</summary>
        public const string UpdateOnStartup = "update_on_startup";

        /// <summary>Whether to update feeds in background</summary>
        public const string UpdateInBackground = "update_in_background";

        /// <summary>Maximum concurrent feed downloads (1-10)</summary>
        public const string MaxConcurrentDownloads = "max_concurrent_downloads";

        /// <summary>Whether to retry failed feeds</summary>
        public const string RetryFailedFeeds = "retry_failed_feeds";

        /// <summary>Maximum retry attempts for failed feeds</summary>
        public const string MaxRetryAttempts = "max_retry_attempts";

        /// <summary>Article retention days (0 = infinite)</summary>
        public const string ArticleRetentionDays = "article_retention_days";

        /// <summary>Days to keep read articles</summary>
        public const string KeepReadArticles = "keep_read_articles";

        /// <summary>Whether auto-cleanup is enabled</summary>
        public const string AutoCleanupEnabled = "auto_cleanup_enabled";

        /// <summary>Whether to keep favorite articles during cleanup</summary>
        public const string KeepFavoriteArticles = "keep_favorite_articles";

        /// <summary>Whether to keep unread articles during cleanup</summary>
        public const string KeepUnreadArticles = "keep_unread_articles";

        #endregion

        #region Rule System

        /// <summary>Whether rules engine is enabled</summary>
        public const string RulesEnabled = "rules_enabled";

        /// <summary>Rules processing order: "priority", "sequential"</summary>
        public const string RulesProcessingOrder = "rules_processing_order";

        /// <summary>Whether to stop processing after first matching rule</summary>
        public const string StopOnFirstMatch = "stop_on_first_match";

        /// <summary>Whether to process old articles through rules</summary>
        public const string ProcessOldArticles = "process_old_articles";

        /// <summary>Notification sound path for rule notifications</summary>
        public const string RuleNotificationSound = "rule_notification_sound";

        #endregion

        #region Notifications

        /// <summary>Whether notifications are enabled</summary>
        public const string NotificationsEnabled = "notifications_enabled";

        /// <summary>Notification duration in seconds</summary>
        public const string NotificationDuration = "notification_duration";

        /// <summary>Notification sound</summary>
        public const string NotificationSound = "notification_sound";

        /// <summary>Notification position on screen</summary>
        public const string NotificationPosition = "notification_position";

        /// <summary>Whether to show notification preview text</summary>
        public const string ShowNotificationPreview = "show_notification_preview";

        /// <summary>Whether to group notifications</summary>
        public const string GroupNotifications = "group_notifications";

        /// <summary>Whether to show only critical notifications</summary>
        public const string CriticalNotificationsOnly = "critical_notifications_only";

        /// <summary>Whether do not disturb mode is enabled</summary>
        public const string DoNotDisturbMode = "do_not_disturb_mode";

        /// <summary>Do not disturb hours range: "22:00-08:00"</summary>
        public const string DoNotDisturbHours = "do_not_disturb_hours";

        #endregion

        #region Tag System

        /// <summary>Whether tag cloud is enabled</summary>
        public const string TagCloudEnabled = "tag_cloud_enabled";

        /// <summary>Maximum number of tags in cloud</summary>
        public const string TagCloudMaxTags = "tag_cloud_max_tags";

        /// <summary>Whether auto-tagging is enabled</summary>
        public const string AutoTaggingEnabled = "auto_tagging_enabled";

        /// <summary>Whether to show tag colors</summary>
        public const string ShowTagColors = "show_tag_colors";

        #endregion

        #region Performance

        /// <summary>Whether caching is enabled</summary>
        public const string CacheEnabled = "cache_enabled";

        /// <summary>Cache size limit in MB</summary>
        public const string CacheSizeLimit = "cache_size_limit";

        /// <summary>Whether to preload articles</summary>
        public const string PreloadArticles = "preload_articles";

        /// <summary>Maximum articles to keep in memory</summary>
        public const string MaxArticlesInMemory = "max_articles_in_memory";

        /// <summary>Whether image cache is enabled</summary>
        public const string EnableImageCache = "enable_image_cache";

        /// <summary>Image cache size in MB</summary>
        public const string ImageCacheSize = "image_cache_size";

        #endregion

        #region Privacy & Security

        /// <summary>Whether to clear history on application exit</summary>
        public const string ClearHistoryOnExit = "clear_history_on_exit";

        /// <summary>Whether to send anonymous usage statistics</summary>
        public const string SendUsageStatistics = "send_usage_statistics";

        /// <summary>Whether to block known trackers</summary>
        public const string BlockTrackers = "block_trackers";

        /// <summary>Whether to enforce HTTPS only</summary>
        public const string HttpsOnly = "https_only";

        /// <summary>Whether proxy is enabled</summary>
        public const string ProxyEnabled = "proxy_enabled";

        /// <summary>Proxy server address</summary>
        public const string ProxyAddress = "proxy_address";

        /// <summary>Proxy server port</summary>
        public const string ProxyPort = "proxy_port";

        #endregion

        #region Backup & Sync

        /// <summary>Whether auto-backup is enabled</summary>
        public const string AutoBackupEnabled = "auto_backup_enabled";

        /// <summary>Backup frequency: "daily", "weekly", "monthly"</summary>
        public const string BackupFrequency = "backup_frequency";

        /// <summary>Backup location path</summary>
        public const string BackupLocation = "backup_location";

        /// <summary>Number of backup copies to keep</summary>
        public const string KeepBackupCopies = "keep_backup_copies";

        /// <summary>Whether synchronization is enabled</summary>
        public const string SyncEnabled = "sync_enabled";

        /// <summary>Synchronization frequency in minutes</summary>
        public const string SyncFrequency = "sync_frequency";

        /// <summary>Last synchronization timestamp</summary>
        public const string LastSyncDate = "last_sync_date";

        #endregion

        #region Keyboard Shortcuts

        /// <summary>Whether keyboard shortcuts are enabled</summary>
        public const string KeyboardShortcutsEnabled = "keyboard_shortcuts_enabled";

        /// <summary>Mark as read shortcut key</summary>
        public const string MarkAsReadShortcut = "mark_as_read_shortcut";

        /// <summary>Next article shortcut key</summary>
        public const string NextArticleShortcut = "next_article_shortcut";

        /// <summary>Previous article shortcut key</summary>
        public const string PrevArticleShortcut = "prev_article_shortcut";

        #endregion

        #region Language & Region

        /// <summary>Application language code</summary>
        public const string Language = "language";

        /// <summary>Date format: "relative", "absolute"</summary>
        public const string DateFormat = "date_format";

        /// <summary>Time format: "12h", "24h"</summary>
        public const string TimeFormat = "time_format";

        /// <summary>First day of week: "monday", "sunday"</summary>
        public const string FirstDayOfWeek = "first_day_of_week";

        #endregion

        #region Advanced

        /// <summary>Whether developer mode is enabled</summary>
        public const string DeveloperMode = "developer_mode";

        /// <summary>Whether API is enabled</summary>
        public const string EnableApi = "enable_api";

        /// <summary>API port number</summary>
        public const string ApiPort = "api_port";

        /// <summary>Webhook URL for external integrations</summary>
        public const string WebhookUrl = "webhook_url";

        /// <summary>Whether plugins are enabled</summary>
        public const string EnablePlugins = "enable_plugins";

        /// <summary>Plugin directory path</summary>
        public const string PluginDirectory = "plugin_directory";

        #endregion

        #region Logging

        /// <summary>Log level</summary>
        public const string LogLevel = "log_level";

        /// <summary>Log retention days</summary>
        public const string LogRetentionDays = "log_retention_days";

        /// <summary>Whether verbose logging is enabled</summary>
        public const string EnableVerboseLogging = "enable_verbose_logging";

        #endregion

        /// <summary>
        /// Maximum image cache size in megabytes
        /// </summary>
        public const string MaxImageCacheSizeMB = "image_cache_max_size_mb";

        /// <summary>
        /// Maximum image cache size in megabytes
        /// </summary>
        public const string CleanupHourOfDay = "cleanup_hour_of_day";

        /// <summary>
        /// Maximum image cache size in megabytes
        /// </summary>
        public const string CleanupDayOfWeek = "cleanup_day_of_week";

        /// <summary>
        ///  Whether to vacuum the database after cleanup
        /// </summary>
        public const string VacuumAfterCleanup = "vacuum_after_cleanup";

        /// <summary>
        ///  Whether to rebuild indexes after cleanup
        /// </summary>
        public const string RebuildIndexesAfterCleanup = "rebuild_indexes_after_cleanup";
    }

}
