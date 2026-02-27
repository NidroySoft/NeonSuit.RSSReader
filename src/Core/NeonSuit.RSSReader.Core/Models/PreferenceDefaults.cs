namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Default values for all preferences.
    /// Provides a centralized source of truth for default configurations.
    /// </summary>
    public static class PreferenceDefaults
    {
        #region UI & Appearance

        /// <summary>Default theme: "system"</summary>
        public const string Theme = "system";

        /// <summary>Default accent color: green</summary>
        public const string AccentColor = "#FF4CAF50";

        /// <summary>Default view: "list"</summary>
        public const string DefaultView = "list";

        /// <summary>Default article font: "Segoe UI"</summary>
        public const string ArticleFont = "Segoe UI";

        /// <summary>Default font size: "normal"</summary>
        public const string ArticleFontSize = "normal";

        /// <summary>Default UI density: "normal"</summary>
        public const string UiDensity = "normal";

        /// <summary>Default show sidebar: true</summary>
        public const bool ShowSidebar = true;

        /// <summary>Default sidebar width: 250px</summary>
        public const int SidebarWidth = 250;

        /// <summary>Default animations enabled: true</summary>
        public const bool AnimationEnabled = true;

        #endregion

        #region Article Reading

        /// <summary>Default auto mark as read: false</summary>
        public const bool AutoMarkAsRead = false;

        /// <summary>Default auto mark delay: 3 seconds</summary>
        public const int AutoMarkAsReadDelay = 3;

        /// <summary>Default show images: true</summary>
        public const bool ShowImages = true;

        /// <summary>Default lazy load images: true</summary>
        public const bool LazyLoadImages = true;

        /// <summary>Default reader view mode: "clean"</summary>
        public const string ReaderViewMode = "clean";

        /// <summary>Default text justification: "justify"</summary>
        public const string TextJustification = "justify";

        /// <summary>Default line height: 1.5</summary>
        public const double LineHeight = 1.5;

        /// <summary>Default night mode: false</summary>
        public const bool NightMode = false;

        /// <summary>Default night mode schedule: "manual"</summary>
        public const string NightModeSchedule = "manual";

        /// <summary>Default keep favorite articles: true</summary>
        public const bool KeepFavoriteArticles = true;

        /// <summary>Default keep unread articles: true</summary>
        public const bool KeepUnreadArticles = true;

        #endregion

        #region Navigation & Links

        /// <summary>Default open links in app: true</summary>
        public const bool OpenLinksInApp = true;

        /// <summary>Default open links in background: false</summary>
        public const bool OpenLinksBackground = false;

        /// <summary>Default use external browser: false</summary>
        public const bool UseExternalBrowser = false;

        /// <summary>Default external browser path: empty</summary>
        public const string ExternalBrowserPath = "";

        /// <summary>Default middle click opens background: true</summary>
        public const bool MiddleClickOpensBackground = true;

        /// <summary>Default mark as read on scroll: false</summary>
        public const bool MarkAsReadOnScroll = false;

        #endregion

        #region Feed Management

        /// <summary>Default update frequency: 60 minutes</summary>
        public const int DefaultUpdateFrequency = 60;

        /// <summary>Default update on startup: true</summary>
        public const bool UpdateOnStartup = true;

        /// <summary>Default update in background: true</summary>
        public const bool UpdateInBackground = true;

        /// <summary>Default max concurrent downloads: 3</summary>
        public const int MaxConcurrentDownloads = 3;

        /// <summary>Default retry failed feeds: true</summary>
        public const bool RetryFailedFeeds = true;

        /// <summary>Default max retry attempts: 3</summary>
        public const int MaxRetryAttempts = 3;

        /// <summary>Default article retention days: 30</summary>
        public const int ArticleRetentionDays = 30;

        /// <summary>Default keep read articles days: 7</summary>
        public const int KeepReadArticles = 7;

        /// <summary>Default auto cleanup enabled: true</summary>
        public const bool AutoCleanupEnabled = true;

        #endregion

        #region Rule System

        /// <summary>Default rules enabled: true</summary>
        public const bool RulesEnabled = true;

        /// <summary>Default rules processing order: "priority"</summary>
        public const string RulesProcessingOrder = "priority";

        /// <summary>Default stop on first match: false</summary>
        public const bool StopOnFirstMatch = false;

        /// <summary>Default process old articles: false</summary>
        public const bool ProcessOldArticles = false;

        /// <summary>Default rule notification sound: empty</summary>
        public const string RuleNotificationSound = "";

        #endregion

        #region Notifications

        /// <summary>Default notifications enabled: true</summary>
        public const bool NotificationsEnabled = true;

        /// <summary>Default notification duration: 7 seconds</summary>
        public const int NotificationDuration = 7;

        /// <summary>Default notification sound: "default"</summary>
        public const string NotificationSound = "default";

        /// <summary>Default notification position: "bottom-right"</summary>
        public const string NotificationPosition = "bottom-right";

        /// <summary>Default show notification preview: true</summary>
        public const bool ShowNotificationPreview = true;

        /// <summary>Default group notifications: true</summary>
        public const bool GroupNotifications = true;

        /// <summary>Default critical notifications only: false</summary>
        public const bool CriticalNotificationsOnly = false;

        /// <summary>Default do not disturb mode: false</summary>
        public const bool DoNotDisturbMode = false;

        /// <summary>Default do not disturb hours: "22:00-08:00"</summary>
        public const string DoNotDisturbHours = "22:00-08:00";

        #endregion

        #region Tag System

        /// <summary>Default tag cloud enabled: true</summary>
        public const bool TagCloudEnabled = true;

        /// <summary>Default tag cloud max tags: 50</summary>
        public const int TagCloudMaxTags = 50;

        /// <summary>Default auto tagging enabled: false</summary>
        public const bool AutoTaggingEnabled = false;

        /// <summary>Default show tag colors: true</summary>
        public const bool ShowTagColors = true;

        #endregion

        #region Performance

        /// <summary>Default cache enabled: true</summary>
        public const bool CacheEnabled = true;

        /// <summary>Default cache size limit: 500 MB</summary>
        public const int CacheSizeLimit = 500;

        /// <summary>Default preload articles: true</summary>
        public const bool PreloadArticles = true;

        /// <summary>Default max articles in memory: 1000</summary>
        public const int MaxArticlesInMemory = 1000;

        /// <summary>Default enable image cache: true</summary>
        public const bool EnableImageCache = true;

        /// <summary>Default image cache size: 200 MB</summary>
        public const int ImageCacheSize = 200;

        #endregion

        #region Privacy & Security

        /// <summary>Default clear history on exit: false</summary>
        public const bool ClearHistoryOnExit = false;

        /// <summary>Default send usage statistics: false</summary>
        public const bool SendUsageStatistics = false;

        /// <summary>Default block trackers: true</summary>
        public const bool BlockTrackers = true;

        /// <summary>Default https only: true</summary>
        public const bool HttpsOnly = true;

        /// <summary>Default proxy enabled: false</summary>
        public const bool ProxyEnabled = false;

        /// <summary>Default proxy address: empty</summary>
        public const string ProxyAddress = "";

        /// <summary>Default proxy port: 8080</summary>
        public const int ProxyPort = 8080;

        #endregion

        #region Backup & Sync

        /// <summary>Default auto backup enabled: true</summary>
        public const bool AutoBackupEnabled = true;

        /// <summary>Default backup frequency: "weekly"</summary>
        public const string BackupFrequency = "weekly";

        /// <summary>Default backup location: empty (app data)</summary>
        public const string BackupLocation = "";

        /// <summary>Default keep backup copies: 5</summary>
        public const int KeepBackupCopies = 5;

        /// <summary>Default sync enabled: false</summary>
        public const bool SyncEnabled = false;

        /// <summary>Default sync frequency: 60 minutes</summary>
        public const int SyncFrequency = 60;

        #endregion

        #region Keyboard Shortcuts

        /// <summary>Default keyboard shortcuts enabled: true</summary>
        public const bool KeyboardShortcutsEnabled = true;

        /// <summary>Default mark as read shortcut: "Space"</summary>
        public const string MarkAsReadShortcut = "Space";

        /// <summary>Default next article shortcut: "J"</summary>
        public const string NextArticleShortcut = "J";

        /// <summary>Default previous article shortcut: "K"</summary>
        public const string PrevArticleShortcut = "K";

        #endregion

        #region Language & Region

        /// <summary>Default language: "system"</summary>
        public const string Language = "system";

        /// <summary>Default date format: "relative"</summary>
        public const string DateFormat = "relative";

        /// <summary>Default time format: "24h"</summary>
        public const string TimeFormat = "24h";

        /// <summary>Default first day of week: "monday"</summary>
        public const string FirstDayOfWeek = "monday";

        #endregion

        #region Advanced

        /// <summary>Default developer mode: false</summary>
        public const bool DeveloperMode = false;

        /// <summary>Default enable API: false</summary>
        public const bool EnableApi = false;

        /// <summary>Default API port: 8081</summary>
        public const int ApiPort = 8081;

        /// <summary>Default webhook URL: empty</summary>
        public const string WebhookUrl = "";

        /// <summary>Default enable plugins: false</summary>
        public const bool EnablePlugins = false;

        /// <summary>Default plugin directory: empty</summary>
        public const string PluginDirectory = "";

        #endregion

        #region Logging

        /// <summary>Default log level: "Information"</summary>
        public const string LogLevel = "Information";

        /// <summary>Default log retention days: 7</summary>
        public const int LogRetentionDays = 7;

        /// <summary>Default enable verbose logging: false</summary>
        public const bool EnableVerboseLogging = false;

        #endregion

        /// <summary>
        /// Default article retention days: 30
        /// </summary>
        public const int MaxImageCacheSizeMB = 500;

        /// <summary>
        /// Default article retention days: 30
        /// </summary>
        public const int CleanupHourOfDay = 2;
        /// <summary>
        /// Default article retention days: 30
        /// </summary>
        public const DayOfWeek CleanupDayOfWeek = DayOfWeek.Sunday;

        /// <summary>
        /// Default article retention days: 30
        /// </summary>
        public const bool VacuumAfterCleanup = true;

        /// <summary>
        /// Default article retention days: 30
        /// </summary>
        public const bool RebuildIndexesAfterCleanup = false;

    }
}
