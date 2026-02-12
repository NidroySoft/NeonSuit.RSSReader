using SQLite;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// User preferences and configuration with full INotifyPropertyChanged support.
    /// </summary>
    [Table("UserPreferences")]
    public class UserPreferences : INotifyPropertyChanged
    {
        public UserPreferences()
        {
            LastModified = DateTime.UtcNow;
        }

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Preference key.
        /// </summary>
        [Unique, NotNull]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Preference value (stored as string).
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Last modification date.
        /// </summary>
        public DateTime LastModified { get; set; }

        // ===== PROPIEDADES CALCULADAS (para binding fácil) =====

        [Ignore]
        public bool BoolValue
        {
            get => bool.TryParse(Value, out bool result) && result;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        [Ignore]
        public int IntValue
        {
            get => int.TryParse(Value, out int result) ? result : 0;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        [Ignore]
        public double DoubleValue
        {
            get => double.TryParse(Value, out double result) ? result : 0.0;
            set
            {
                Value = value.ToString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Value));
            }
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Complete preference keys for all application features.
    /// </summary>
    public static class PreferenceKeys
    {
        // ===== INTERFAZ Y APARIENCIA =====
        public const string Theme = "theme"; // "light", "dark", "system", "custom"
        public const string AccentColor = "accent_color"; // "#FF4CAF50"
        public const string DefaultView = "default_view"; // "list", "grid", "magazine", "compact"
        public const string ArticleFont = "article_font"; // "Segoe UI", "Calibri", etc.
        public const string ArticleFontSize = "article_font_size"; // "small", "normal", "large", "xlarge"
        public const string UiDensity = "ui_density"; // "compact", "normal", "comfortable"
        public const string ShowSidebar = "show_sidebar";
        public const string SidebarWidth = "sidebar_width";
        public const string AnimationEnabled = "animation_enabled";

        // ===== LECTURA DE ARTÍCULOS =====
        public const string AutoMarkAsRead = "auto_mark_as_read";
        public const string AutoMarkAsReadDelay = "auto_mark_as_read_delay"; // segundos
        public const string ShowImages = "show_images";
        public const string LazyLoadImages = "lazy_load_images";
        public const string ReaderViewMode = "reader_view_mode"; // "full", "clean", "original"
        public const string TextJustification = "text_justification"; // "left", "justify"
        public const string LineHeight = "line_height"; // 1.0 - 2.0
        public const string NightMode = "night_mode";
        public const string NightModeSchedule = "night_mode_schedule"; // "sunset", "21:00", "manual"

        // ===== NAVEGACIÓN Y ENLACES =====
        public const string OpenLinksInApp = "open_links_in_app";
        public const string OpenLinksBackground = "open_links_background";
        public const string UseExternalBrowser = "use_external_browser";
        public const string ExternalBrowserPath = "external_browser_path";
        public const string MiddleClickOpensBackground = "middle_click_opens_background";
        public const string MarkAsReadOnScroll = "mark_as_read_on_scroll";

        // ===== GESTIÓN DE FEEDS =====
        public const string DefaultUpdateFrequency = "default_update_frequency"; // minutos
        public const string UpdateOnStartup = "update_on_startup";
        public const string UpdateInBackground = "update_in_background";
        public const string MaxConcurrentDownloads = "max_concurrent_downloads"; // 1-10
        public const string RetryFailedFeeds = "retry_failed_feeds";
        public const string MaxRetryAttempts = "max_retry_attempts";
        public const string ArticleRetentionDays = "article_retention_days"; // 0 = infinito
        public const string KeepReadArticles = "keep_read_articles"; // días
        public const string AutoCleanupEnabled = "auto_cleanup_enabled";

        // ===== SISTEMA DE REGLAS =====
        public const string RulesEnabled = "rules_enabled";
        public const string RulesProcessingOrder = "rules_processing_order"; // "priority", "sequential"
        public const string StopOnFirstMatch = "stop_on_first_match";
        public const string ProcessOldArticles = "process_old_articles";
        public const string RuleNotificationSound = "rule_notification_sound"; // ruta archivo

        // ===== NOTIFICACIONES =====
        public const string NotificationsEnabled = "notifications_enabled";
        public const string NotificationDuration = "notification_duration"; // segundos
        public const string NotificationSound = "notification_sound";
        public const string NotificationPosition = "notification_position"; // "top-right", "bottom-right", etc.
        public const string ShowNotificationPreview = "show_notification_preview";
        public const string GroupNotifications = "group_notifications";
        public const string CriticalNotificationsOnly = "critical_notifications_only";
        public const string DoNotDisturbMode = "do_not_disturb_mode";
        public const string DoNotDisturbHours = "do_not_disturb_hours"; // "22:00-08:00"

        // ===== SISTEMA DE ETIQUETAS =====
        public const string TagCloudEnabled = "tag_cloud_enabled";
        public const string TagCloudMaxTags = "tag_cloud_max_tags";
        public const string AutoTaggingEnabled = "auto_tagging_enabled";
        public const string ShowTagColors = "show_tag_colors";

        // ===== RENDIMIENTO =====
        public const string CacheEnabled = "cache_enabled";
        public const string CacheSizeLimit = "cache_size_limit"; // MB
        public const string PreloadArticles = "preload_articles";
        public const string MaxArticlesInMemory = "max_articles_in_memory"; // 100-5000
        public const string EnableImageCache = "enable_image_cache";
        public const string ImageCacheSize = "image_cache_size"; // MB

        // ===== PRIVACIDAD Y SEGURIDAD =====
        public const string ClearHistoryOnExit = "clear_history_on_exit";
        public const string SendUsageStatistics = "send_usage_statistics";
        public const string BlockTrackers = "block_trackers";
        public const string HttpsOnly = "https_only";
        public const string ProxyEnabled = "proxy_enabled";
        public const string ProxyAddress = "proxy_address";
        public const string ProxyPort = "proxy_port";

        // ===== BACKUP Y SINCRONIZACIÓN =====
        public const string AutoBackupEnabled = "auto_backup_enabled";
        public const string BackupFrequency = "backup_frequency"; // "daily", "weekly", "monthly"
        public const string BackupLocation = "backup_location";
        public const string KeepBackupCopies = "keep_backup_copies";
        public const string SyncEnabled = "sync_enabled";
        public const string SyncFrequency = "sync_frequency"; // minutos
        public const string LastSyncDate = "last_sync_date";

        // ===== ACCESOS DIRECTOS DE TECLADO =====
        public const string KeyboardShortcutsEnabled = "keyboard_shortcuts_enabled";
        public const string MarkAsReadShortcut = "mark_as_read_shortcut"; // "Space", "Enter", "R"
        public const string NextArticleShortcut = "next_article_shortcut"; // "J", "Down", "N"
        public const string PrevArticleShortcut = "prev_article_shortcut"; // "K", "Up", "P"

        // ===== IDIOMA Y REGIÓN =====
        public const string Language = "language"; // "es", "en", "fr", etc.
        public const string DateFormat = "date_format"; // "relative", "absolute"
        public const string TimeFormat = "time_format"; // "12h", "24h"
        public const string FirstDayOfWeek = "first_day_of_week"; // "monday", "sunday"

        // ===== EXPERIMENTAL Y AVANZADO =====
        public const string DeveloperMode = "developer_mode";
        public const string EnableApi = "enable_api";
        public const string ApiPort = "api_port";
        public const string WebhookUrl = "webhook_url";
        public const string EnablePlugins = "enable_plugins";
        public const string PluginDirectory = "plugin_directory";

        // ===== LOGGING (Ya tenemos AppLoggerConfig, pero por consistencia) =====
        public const string LogLevel = "log_level";
        public const string LogRetentionDays = "log_retention_days";
        public const string EnableVerboseLogging = "enable_verbose_logging";
    }

    /// <summary>
    /// Default values for all preferences.
    /// </summary>
    public static class PreferenceDefaults
    {
        // Interfaz
        public const string Theme = "system";
        public const string AccentColor = "#FF4CAF50";
        public const string DefaultView = "list";
        public const string ArticleFont = "Segoe UI";
        public const string ArticleFontSize = "normal";
        public const string UiDensity = "normal";
        public const bool ShowSidebar = true;
        public const int SidebarWidth = 250;
        public const bool AnimationEnabled = true;

        // Lectura
        public const bool AutoMarkAsRead = false;
        public const int AutoMarkAsReadDelay = 3;
        public const bool ShowImages = true;
        public const bool LazyLoadImages = true;
        public const string ReaderViewMode = "clean";
        public const string TextJustification = "justify";
        public const double LineHeight = 1.5;
        public const bool NightMode = false;
        public const string NightModeSchedule = "manual";

        // Navegación
        public const bool OpenLinksInApp = true;
        public const bool OpenLinksBackground = false;
        public const bool UseExternalBrowser = false;
        public const string ExternalBrowserPath = "";
        public const bool MiddleClickOpensBackground = true;
        public const bool MarkAsReadOnScroll = false;

        // Feeds
        public const int DefaultUpdateFrequency = 60; // minutos
        public const bool UpdateOnStartup = true;
        public const bool UpdateInBackground = true;
        public const int MaxConcurrentDownloads = 3;
        public const bool RetryFailedFeeds = true;
        public const int MaxRetryAttempts = 3;
        public const int ArticleRetentionDays = 30;
        public const int KeepReadArticles = 7;
        public const bool AutoCleanupEnabled = true;

        // Reglas
        public const bool RulesEnabled = true;
        public const string RulesProcessingOrder = "priority";
        public const bool StopOnFirstMatch = false;
        public const bool ProcessOldArticles = false;
        public const string RuleNotificationSound = "";

        // Notificaciones
        public const bool NotificationsEnabled = true;
        public const int NotificationDuration = 7;
        public const string NotificationSound = "default";
        public const string NotificationPosition = "bottom-right";
        public const bool ShowNotificationPreview = true;
        public const bool GroupNotifications = true;
        public const bool CriticalNotificationsOnly = false;
        public const bool DoNotDisturbMode = false;
        public const string DoNotDisturbHours = "22:00-08:00";

        // Etiquetas
        public const bool TagCloudEnabled = true;
        public const int TagCloudMaxTags = 50;
        public const bool AutoTaggingEnabled = false;
        public const bool ShowTagColors = true;

        // Rendimiento
        public const bool CacheEnabled = true;
        public const int CacheSizeLimit = 500; // MB
        public const bool PreloadArticles = true;
        public const int MaxArticlesInMemory = 1000;
        public const bool EnableImageCache = true;
        public const int ImageCacheSize = 200; // MB

        // Privacidad
        public const bool ClearHistoryOnExit = false;
        public const bool SendUsageStatistics = false;
        public const bool BlockTrackers = true;
        public const bool HttpsOnly = true;
        public const bool ProxyEnabled = false;
        public const string ProxyAddress = "";
        public const int ProxyPort = 8080;

        // Backup
        public const bool AutoBackupEnabled = true;
        public const string BackupFrequency = "weekly";
        public const string BackupLocation = "";
        public const int KeepBackupCopies = 5;
        public const bool SyncEnabled = false;
        public const int SyncFrequency = 60;

        // Teclado
        public const bool KeyboardShortcutsEnabled = true;
        public const string MarkAsReadShortcut = "Space";
        public const string NextArticleShortcut = "J";
        public const string PrevArticleShortcut = "K";

        // Idioma
        public const string Language = "system";
        public const string DateFormat = "relative";
        public const string TimeFormat = "24h";
        public const string FirstDayOfWeek = "monday";

        // Avanzado
        public const bool DeveloperMode = false;
        public const bool EnableApi = false;
        public const int ApiPort = 8081;
        public const string WebhookUrl = "";
        public const bool EnablePlugins = false;
        public const string PluginDirectory = "";

        // Logging
        public const string LogLevel = "Information";
        public const int LogRetentionDays = 7;
        public const bool EnableVerboseLogging = false;
    }

    /// <summary>
    /// Helper methods for working with preferences.
    /// </summary>
    public static class PreferenceHelper
    {
        /// <summary>
        /// Gets all preference keys organized by category.
        /// </summary>
        public static Dictionary<string, List<string>> GetCategorizedKeys()
        {
            return new Dictionary<string, List<string>>
            {
                ["Interfaz"] = new List<string>
                {
                    PreferenceKeys.Theme,
                    PreferenceKeys.AccentColor,
                    PreferenceKeys.DefaultView,
                    PreferenceKeys.ArticleFont,
                    PreferenceKeys.ArticleFontSize,
                    PreferenceKeys.UiDensity,
                    PreferenceKeys.ShowSidebar,
                    PreferenceKeys.SidebarWidth,
                    PreferenceKeys.AnimationEnabled
                },
                ["Lectura"] = new List<string>
                {
                    PreferenceKeys.AutoMarkAsRead,
                    PreferenceKeys.AutoMarkAsReadDelay,
                    PreferenceKeys.ShowImages,
                    PreferenceKeys.LazyLoadImages,
                    PreferenceKeys.ReaderViewMode,
                    PreferenceKeys.TextJustification,
                    PreferenceKeys.LineHeight,
                    PreferenceKeys.NightMode,
                    PreferenceKeys.NightModeSchedule
                },
                ["Feeds"] = new List<string>
                {
                    PreferenceKeys.DefaultUpdateFrequency,
                    PreferenceKeys.UpdateOnStartup,
                    PreferenceKeys.UpdateInBackground,
                    PreferenceKeys.MaxConcurrentDownloads,
                    PreferenceKeys.RetryFailedFeeds,
                    PreferenceKeys.MaxRetryAttempts,
                    PreferenceKeys.ArticleRetentionDays,
                    PreferenceKeys.KeepReadArticles,
                    PreferenceKeys.AutoCleanupEnabled
                },
                ["Reglas"] = new List<string>
                {
                    PreferenceKeys.RulesEnabled,
                    PreferenceKeys.RulesProcessingOrder,
                    PreferenceKeys.StopOnFirstMatch,
                    PreferenceKeys.ProcessOldArticles,
                    PreferenceKeys.RuleNotificationSound
                },
                ["Notificaciones"] = new List<string>
                {
                    PreferenceKeys.NotificationsEnabled,
                    PreferenceKeys.NotificationDuration,
                    PreferenceKeys.NotificationSound,
                    PreferenceKeys.NotificationPosition,
                    PreferenceKeys.ShowNotificationPreview,
                    PreferenceKeys.GroupNotifications,
                    PreferenceKeys.CriticalNotificationsOnly,
                    PreferenceKeys.DoNotDisturbMode,
                    PreferenceKeys.DoNotDisturbHours
                },
                ["Rendimiento"] = new List<string>
                {
                    PreferenceKeys.CacheEnabled,
                    PreferenceKeys.CacheSizeLimit,
                    PreferenceKeys.PreloadArticles,
                    PreferenceKeys.MaxArticlesInMemory,
                    PreferenceKeys.EnableImageCache,
                    PreferenceKeys.ImageCacheSize
                },
                ["Privacidad"] = new List<string>
                {
                    PreferenceKeys.ClearHistoryOnExit,
                    PreferenceKeys.SendUsageStatistics,
                    PreferenceKeys.BlockTrackers,
                    PreferenceKeys.HttpsOnly,
                    PreferenceKeys.ProxyEnabled,
                    PreferenceKeys.ProxyAddress,
                    PreferenceKeys.ProxyPort
                },
                ["Backup"] = new List<string>
                {
                    PreferenceKeys.AutoBackupEnabled,
                    PreferenceKeys.BackupFrequency,
                    PreferenceKeys.BackupLocation,
                    PreferenceKeys.KeepBackupCopies,
                    PreferenceKeys.SyncEnabled,
                    PreferenceKeys.SyncFrequency
                },
                ["Avanzado"] = new List<string>
                {
                    PreferenceKeys.DeveloperMode,
                    PreferenceKeys.EnableApi,
                    PreferenceKeys.ApiPort,
                    PreferenceKeys.WebhookUrl,
                    PreferenceKeys.EnablePlugins,
                    PreferenceKeys.PluginDirectory
                }
            };
        }

        /// <summary>
        /// Gets the default value for a preference key.
        /// </summary>
        public static string GetDefaultValue(string key)
        {
            return key switch
            {
                // ===== INTERFAZ Y APARIENCIA =====
                PreferenceKeys.Theme => PreferenceDefaults.Theme,
                PreferenceKeys.AccentColor => PreferenceDefaults.AccentColor,
                PreferenceKeys.DefaultView => PreferenceDefaults.DefaultView,
                PreferenceKeys.ArticleFont => PreferenceDefaults.ArticleFont,
                PreferenceKeys.ArticleFontSize => PreferenceDefaults.ArticleFontSize,
                PreferenceKeys.UiDensity => PreferenceDefaults.UiDensity,
                PreferenceKeys.ShowSidebar => PreferenceDefaults.ShowSidebar.ToString(),
                PreferenceKeys.SidebarWidth => PreferenceDefaults.SidebarWidth.ToString(),
                PreferenceKeys.AnimationEnabled => PreferenceDefaults.AnimationEnabled.ToString(),

                // ===== LECTURA DE ARTÍCULOS =====
                PreferenceKeys.AutoMarkAsRead => PreferenceDefaults.AutoMarkAsRead.ToString(),
                PreferenceKeys.AutoMarkAsReadDelay => PreferenceDefaults.AutoMarkAsReadDelay.ToString(),
                PreferenceKeys.ShowImages => PreferenceDefaults.ShowImages.ToString(),
                PreferenceKeys.LazyLoadImages => PreferenceDefaults.LazyLoadImages.ToString(),
                PreferenceKeys.ReaderViewMode => PreferenceDefaults.ReaderViewMode,
                PreferenceKeys.TextJustification => PreferenceDefaults.TextJustification,
                PreferenceKeys.LineHeight => PreferenceDefaults.LineHeight.ToString(),
                PreferenceKeys.NightMode => PreferenceDefaults.NightMode.ToString(),
                PreferenceKeys.NightModeSchedule => PreferenceDefaults.NightModeSchedule,

                // ===== NAVEGACIÓN Y ENLACES =====
                PreferenceKeys.OpenLinksInApp => PreferenceDefaults.OpenLinksInApp.ToString(),
                PreferenceKeys.OpenLinksBackground => PreferenceDefaults.OpenLinksBackground.ToString(),
                PreferenceKeys.UseExternalBrowser => PreferenceDefaults.UseExternalBrowser.ToString(),
                PreferenceKeys.ExternalBrowserPath => PreferenceDefaults.ExternalBrowserPath,
                PreferenceKeys.MiddleClickOpensBackground => PreferenceDefaults.MiddleClickOpensBackground.ToString(),
                PreferenceKeys.MarkAsReadOnScroll => PreferenceDefaults.MarkAsReadOnScroll.ToString(),

                // ===== GESTIÓN DE FEEDS =====
                PreferenceKeys.DefaultUpdateFrequency => PreferenceDefaults.DefaultUpdateFrequency.ToString(),
                PreferenceKeys.UpdateOnStartup => PreferenceDefaults.UpdateOnStartup.ToString(),
                PreferenceKeys.UpdateInBackground => PreferenceDefaults.UpdateInBackground.ToString(),
                PreferenceKeys.MaxConcurrentDownloads => PreferenceDefaults.MaxConcurrentDownloads.ToString(),
                PreferenceKeys.RetryFailedFeeds => PreferenceDefaults.RetryFailedFeeds.ToString(),
                PreferenceKeys.MaxRetryAttempts => PreferenceDefaults.MaxRetryAttempts.ToString(),
                PreferenceKeys.ArticleRetentionDays => PreferenceDefaults.ArticleRetentionDays.ToString(),
                PreferenceKeys.KeepReadArticles => PreferenceDefaults.KeepReadArticles.ToString(),
                PreferenceKeys.AutoCleanupEnabled => PreferenceDefaults.AutoCleanupEnabled.ToString(),

                // ===== SISTEMA DE REGLAS =====
                PreferenceKeys.RulesEnabled => PreferenceDefaults.RulesEnabled.ToString(),
                PreferenceKeys.RulesProcessingOrder => PreferenceDefaults.RulesProcessingOrder,
                PreferenceKeys.StopOnFirstMatch => PreferenceDefaults.StopOnFirstMatch.ToString(),
                PreferenceKeys.ProcessOldArticles => PreferenceDefaults.ProcessOldArticles.ToString(),
                PreferenceKeys.RuleNotificationSound => PreferenceDefaults.RuleNotificationSound,

                // ===== NOTIFICACIONES =====
                PreferenceKeys.NotificationsEnabled => PreferenceDefaults.NotificationsEnabled.ToString(),
                PreferenceKeys.NotificationDuration => PreferenceDefaults.NotificationDuration.ToString(),
                PreferenceKeys.NotificationSound => PreferenceDefaults.NotificationSound,
                PreferenceKeys.NotificationPosition => PreferenceDefaults.NotificationPosition,
                PreferenceKeys.ShowNotificationPreview => PreferenceDefaults.ShowNotificationPreview.ToString(),
                PreferenceKeys.GroupNotifications => PreferenceDefaults.GroupNotifications.ToString(),
                PreferenceKeys.CriticalNotificationsOnly => PreferenceDefaults.CriticalNotificationsOnly.ToString(),
                PreferenceKeys.DoNotDisturbMode => PreferenceDefaults.DoNotDisturbMode.ToString(),
                PreferenceKeys.DoNotDisturbHours => PreferenceDefaults.DoNotDisturbHours,

                // ===== SISTEMA DE ETIQUETAS =====
                PreferenceKeys.TagCloudEnabled => PreferenceDefaults.TagCloudEnabled.ToString(),
                PreferenceKeys.TagCloudMaxTags => PreferenceDefaults.TagCloudMaxTags.ToString(),
                PreferenceKeys.AutoTaggingEnabled => PreferenceDefaults.AutoTaggingEnabled.ToString(),
                PreferenceKeys.ShowTagColors => PreferenceDefaults.ShowTagColors.ToString(),

                // ===== RENDIMIENTO =====
                PreferenceKeys.CacheEnabled => PreferenceDefaults.CacheEnabled.ToString(),
                PreferenceKeys.CacheSizeLimit => PreferenceDefaults.CacheSizeLimit.ToString(),
                PreferenceKeys.PreloadArticles => PreferenceDefaults.PreloadArticles.ToString(),
                PreferenceKeys.MaxArticlesInMemory => PreferenceDefaults.MaxArticlesInMemory.ToString(),
                PreferenceKeys.EnableImageCache => PreferenceDefaults.EnableImageCache.ToString(),
                PreferenceKeys.ImageCacheSize => PreferenceDefaults.ImageCacheSize.ToString(),

                // ===== PRIVACIDAD Y SEGURIDAD =====
                PreferenceKeys.ClearHistoryOnExit => PreferenceDefaults.ClearHistoryOnExit.ToString(),
                PreferenceKeys.SendUsageStatistics => PreferenceDefaults.SendUsageStatistics.ToString(),
                PreferenceKeys.BlockTrackers => PreferenceDefaults.BlockTrackers.ToString(),
                PreferenceKeys.HttpsOnly => PreferenceDefaults.HttpsOnly.ToString(),
                PreferenceKeys.ProxyEnabled => PreferenceDefaults.ProxyEnabled.ToString(),
                PreferenceKeys.ProxyAddress => PreferenceDefaults.ProxyAddress,
                PreferenceKeys.ProxyPort => PreferenceDefaults.ProxyPort.ToString(),

                // ===== BACKUP Y SINCRONIZACIÓN =====
                PreferenceKeys.AutoBackupEnabled => PreferenceDefaults.AutoBackupEnabled.ToString(),
                PreferenceKeys.BackupFrequency => PreferenceDefaults.BackupFrequency,
                PreferenceKeys.BackupLocation => PreferenceDefaults.BackupLocation,
                PreferenceKeys.KeepBackupCopies => PreferenceDefaults.KeepBackupCopies.ToString(),
                PreferenceKeys.SyncEnabled => PreferenceDefaults.SyncEnabled.ToString(),
                PreferenceKeys.SyncFrequency => PreferenceDefaults.SyncFrequency.ToString(),
                PreferenceKeys.LastSyncDate => "", // Vacío por defecto

                // ===== ACCESOS DIRECTOS DE TECLADO =====
                PreferenceKeys.KeyboardShortcutsEnabled => PreferenceDefaults.KeyboardShortcutsEnabled.ToString(),
                PreferenceKeys.MarkAsReadShortcut => PreferenceDefaults.MarkAsReadShortcut,
                PreferenceKeys.NextArticleShortcut => PreferenceDefaults.NextArticleShortcut,
                PreferenceKeys.PrevArticleShortcut => PreferenceDefaults.PrevArticleShortcut,

                // ===== IDIOMA Y REGIÓN =====
                PreferenceKeys.Language => PreferenceDefaults.Language,
                PreferenceKeys.DateFormat => PreferenceDefaults.DateFormat,
                PreferenceKeys.TimeFormat => PreferenceDefaults.TimeFormat,
                PreferenceKeys.FirstDayOfWeek => PreferenceDefaults.FirstDayOfWeek,

                // ===== EXPERIMENTAL Y AVANZADO =====
                PreferenceKeys.DeveloperMode => PreferenceDefaults.DeveloperMode.ToString(),
                PreferenceKeys.EnableApi => PreferenceDefaults.EnableApi.ToString(),
                PreferenceKeys.ApiPort => PreferenceDefaults.ApiPort.ToString(),
                PreferenceKeys.WebhookUrl => PreferenceDefaults.WebhookUrl,
                PreferenceKeys.EnablePlugins => PreferenceDefaults.EnablePlugins.ToString(),
                PreferenceKeys.PluginDirectory => PreferenceDefaults.PluginDirectory,

                // ===== LOGGING =====
                PreferenceKeys.LogLevel => PreferenceDefaults.LogLevel,
                PreferenceKeys.LogRetentionDays => PreferenceDefaults.LogRetentionDays.ToString(),
                PreferenceKeys.EnableVerboseLogging => PreferenceDefaults.EnableVerboseLogging.ToString(),

                _ => string.Empty
            };
        }

        /// <summary>
        /// Validates a preference value.
        /// </summary>
        public static bool ValidateValue(string key, string value)
        {
            try
            {
                return key switch
                {
                    // ===== VALIDACIONES DE ENUM/OPCIONES =====
                    PreferenceKeys.Theme => new[] { "light", "dark", "system", "custom" }.Contains(value),
                    PreferenceKeys.DefaultView => new[] { "list", "grid", "magazine", "compact" }.Contains(value),
                    PreferenceKeys.ArticleFontSize => new[] { "small", "normal", "large", "xlarge" }.Contains(value),
                    PreferenceKeys.UiDensity => new[] { "compact", "normal", "comfortable" }.Contains(value),
                    PreferenceKeys.ReaderViewMode => new[] { "full", "clean", "original" }.Contains(value),
                    PreferenceKeys.TextJustification => new[] { "left", "justify" }.Contains(value),
                    PreferenceKeys.NightModeSchedule => new[] { "sunset", "manual" }.Contains(value) ||
                                                       System.Text.RegularExpressions.Regex.IsMatch(value, @"^([01]?[0-9]|2[0-3]):[0-5][0-9]$"),
                    PreferenceKeys.NotificationPosition => new[] { "top-right", "bottom-right", "top-left", "bottom-left" }.Contains(value),
                    PreferenceKeys.BackupFrequency => new[] { "daily", "weekly", "monthly" }.Contains(value),
                    PreferenceKeys.RulesProcessingOrder => new[] { "priority", "sequential" }.Contains(value),
                    PreferenceKeys.DateFormat => new[] { "relative", "absolute" }.Contains(value),
                    PreferenceKeys.TimeFormat => new[] { "12h", "24h" }.Contains(value),
                    PreferenceKeys.FirstDayOfWeek => new[] { "monday", "sunday" }.Contains(value),
                    PreferenceKeys.Language => new[] { "system", "es", "en", "fr", "de", "it", "pt", "ru", "zh" }.Contains(value),

                    // ===== VALIDACIONES NUMÉRICAS =====
                    var k when k == PreferenceKeys.AutoMarkAsReadDelay =>
                        int.TryParse(value, out int delay) && delay >= 0 && delay <= 60,

                    var k when k == PreferenceKeys.DefaultUpdateFrequency =>
                        int.TryParse(value, out int freq) && freq >= 1 && freq <= 1440,

                    var k when k == PreferenceKeys.MaxConcurrentDownloads =>
                        int.TryParse(value, out int downloads) && downloads >= 1 && downloads <= 10,

                    var k when k == PreferenceKeys.MaxRetryAttempts =>
                        int.TryParse(value, out int retries) && retries >= 0 && retries <= 10,

                    var k when k == PreferenceKeys.ArticleRetentionDays =>
                        int.TryParse(value, out int days) && (days == 0 || (days >= 1 && days <= 365)),

                    var k when k == PreferenceKeys.KeepReadArticles =>
                        int.TryParse(value, out int keep) && keep >= 0 && keep <= 365,

                    var k when k == PreferenceKeys.NotificationDuration =>
                        int.TryParse(value, out int duration) && duration >= 1 && duration <= 60,

                    var k when k == PreferenceKeys.TagCloudMaxTags =>
                        int.TryParse(value, out int tags) && tags >= 10 && tags <= 200,

                    var k when k == PreferenceKeys.CacheSizeLimit =>
                        int.TryParse(value, out int cache) && cache >= 10 && cache <= 10000,

                    var k when k == PreferenceKeys.MaxArticlesInMemory =>
                        int.TryParse(value, out int articles) && articles >= 100 && articles <= 10000,

                    var k when k == PreferenceKeys.ImageCacheSize =>
                        int.TryParse(value, out int imgCache) && imgCache >= 10 && imgCache <= 5000,

                    var k when k == PreferenceKeys.SidebarWidth =>
                        int.TryParse(value, out int width) && width >= 100 && width <= 500,

                    var k when k == PreferenceKeys.ProxyPort =>
                        int.TryParse(value, out int port) && port >= 1 && port <= 65535,

                    var k when k == PreferenceKeys.KeepBackupCopies =>
                        int.TryParse(value, out int copies) && copies >= 1 && copies <= 100,

                    var k when k == PreferenceKeys.SyncFrequency =>
                        int.TryParse(value, out int sync) && sync >= 1 && sync <= 1440,

                    var k when k == PreferenceKeys.ApiPort =>
                        int.TryParse(value, out int apiPort) && apiPort >= 1024 && apiPort <= 65535,

                    var k when k == PreferenceKeys.LogRetentionDays =>
                        int.TryParse(value, out int logDays) && logDays >= 1 && logDays <= 365,

                    // ===== VALIDACIONES DE DECIMALES =====
                    var k when k == PreferenceKeys.LineHeight =>
                        double.TryParse(value, out double height) && height >= 1.0 && height <= 2.0,

                    // ===== VALIDACIONES DE COLOR HEX =====
                    var k when k == PreferenceKeys.AccentColor =>
                        System.Text.RegularExpressions.Regex.IsMatch(value, "^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$"),

                    // ===== VALIDACIONES DE RUTAS =====
                    var k when k == PreferenceKeys.ExternalBrowserPath ||
                               k == PreferenceKeys.BackupLocation ||
                               k == PreferenceKeys.PluginDirectory ||
                               k == PreferenceKeys.RuleNotificationSound ||
                               k == PreferenceKeys.NotificationSound =>
                        string.IsNullOrEmpty(value) || (!value.Contains("..") && !value.Contains(":|?*<\">")),

                    // ===== VALIDACIONES DE HORAS =====
                    var k when k == PreferenceKeys.DoNotDisturbHours =>
                        string.IsNullOrEmpty(value) || System.Text.RegularExpressions.Regex.IsMatch(value,
                            @"^([01]?[0-9]|2[0-3]):[0-5][0-9]-([01]?[0-9]|2[0-3]):[0-5][0-9]$"),

                    // ===== VALIDACIONES DE URL =====
                    var k when k == PreferenceKeys.WebhookUrl =>
                        string.IsNullOrEmpty(value) || Uri.TryCreate(value, UriKind.Absolute, out _),

                    var k when k == PreferenceKeys.ProxyAddress =>
                        string.IsNullOrEmpty(value) ||
                        Uri.TryCreate($"http://{value}", UriKind.Absolute, out _) ||
                        System.Net.IPAddress.TryParse(value, out _),

                    // ===== VALIDACIONES BOOLEANAS =====
                    var k when k.EndsWith("_enabled") ||
                               k.StartsWith("enable_") ||
                               k.StartsWith("show_") ||
                               k.StartsWith("auto_") ||
                               k.StartsWith("use_") ||
                               k.StartsWith("open_") ||
                               k.StartsWith("lazy_") ||
                               k.EndsWith("_only") ||
                               k.Contains("_background") ||
                               k == PreferenceKeys.NightMode ||
                               k == PreferenceKeys.MarkAsReadOnScroll ||
                               k == PreferenceKeys.RetryFailedFeeds ||
                               k == PreferenceKeys.AutoCleanupEnabled ||
                               k == PreferenceKeys.StopOnFirstMatch ||
                               k == PreferenceKeys.ProcessOldArticles ||
                               k == PreferenceKeys.GroupNotifications ||
                               k == PreferenceKeys.DoNotDisturbMode ||
                               k == PreferenceKeys.ClearHistoryOnExit ||
                               k == PreferenceKeys.SendUsageStatistics ||
                               k == PreferenceKeys.BlockTrackers ||
                               k == PreferenceKeys.HttpsOnly ||
                               k == PreferenceKeys.ProxyEnabled ||
                               k == PreferenceKeys.KeyboardShortcutsEnabled ||
                               k == PreferenceKeys.DeveloperMode ||
                               k == PreferenceKeys.EnablePlugins ||
                               k == PreferenceKeys.EnableVerboseLogging =>
                        bool.TryParse(value, out _),

                    // ===== VALIDACIONES DE TEXTO GENERAL =====
                    var k when k == PreferenceKeys.ArticleFont =>
                        !string.IsNullOrEmpty(value) && value.Length <= 50,

                    var k when k == PreferenceKeys.MarkAsReadShortcut ||
                               k == PreferenceKeys.NextArticleShortcut ||
                               k == PreferenceKeys.PrevArticleShortcut =>
                        !string.IsNullOrEmpty(value) && value.Length <= 20,

                    // ===== VALOR POR DEFECTO VÁLIDO PARA EL RESTO =====
                    _ => true
                };
            }
            catch
            {
                return false;
            }
        }
    }
}