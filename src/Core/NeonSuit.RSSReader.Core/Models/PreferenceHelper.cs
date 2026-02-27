using NeonSuit.RSSReader.Core.Interfaces.Services;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Helper methods for working with application preferences.
    /// Provides categorization for UI display, default value resolution,
    /// and comprehensive validation for all preference types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class works in conjunction with:
    /// <list type="bullet">
    /// <item><see cref="PreferenceKeys"/> - Defines all available preference keys</item>
    /// <item><see cref="PreferenceDefaults"/> - Provides default values</item>
    /// <item><see cref="ISettingsService"/> - Service that uses these helpers</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods are thread-safe as the class is static and immutable.
    /// Validation uses compiled regular expressions for performance.
    /// </para>
    /// </remarks>
    public static class PreferenceHelper
    {
        // Constantes para rangos de validación
        private const int MIN_DELAY_SECONDS = 0;
        private const int MAX_DELAY_SECONDS = 60;
        private const int MIN_UPDATE_FREQUENCY = 1;
        private const int MAX_UPDATE_FREQUENCY = 1440; // 24 horas
        private const int MIN_CONCURRENT_DOWNLOADS = 1;
        private const int MAX_CONCURRENT_DOWNLOADS = 10;
        private const int MIN_RETRY_ATTEMPTS = 0;
        private const int MAX_RETRY_ATTEMPTS = 10;
        private const int MIN_RETENTION_DAYS = 1;
        private const int MAX_RETENTION_DAYS = 365;
        private const int MIN_SIDEBAR_WIDTH = 100;
        private const int MAX_SIDEBAR_WIDTH = 500;
        private const int MIN_PROXY_PORT = 1;
        private const int MAX_PROXY_PORT = 65535;
        private const int MIN_API_PORT = 1024;
        private const int MAX_API_PORT = 65535;
        private const double MIN_LINE_HEIGHT = 1.0;
        private const double MAX_LINE_HEIGHT = 2.0;

        // Expresiones regulares compiladas para mejor rendimiento
        private static readonly Regex _timeRegex = new Regex(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);
        private static readonly Regex _timeRangeRegex = new Regex(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]-([01]?[0-9]|2[0-3]):[0-5][0-9]$", RegexOptions.Compiled);
        private static readonly Regex _hexColorRegex = new Regex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);
        private static readonly Regex _safePathRegex = new Regex(@"^[^<>:""/\\|?*]*$", RegexOptions.Compiled); // Caracteres no permitidos en rutas

        #region Categorization

        /// <summary>
        /// Gets all preference keys organized by category for UI display.
        /// </summary>
        /// <returns>A dictionary where keys are category names and values are lists of preference keys in that category.</returns>
        /// <remarks>
        /// This method is designed to be used by settings UI to render categorized preference panels.
        /// Categories are localized in Spanish as the primary UI language.
        /// </remarks>
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
                ["Navegación"] = new List<string>
                {
                    PreferenceKeys.OpenLinksInApp,
                    PreferenceKeys.OpenLinksBackground,
                    PreferenceKeys.UseExternalBrowser,
                    PreferenceKeys.ExternalBrowserPath,
                    PreferenceKeys.MiddleClickOpensBackground,
                    PreferenceKeys.MarkAsReadOnScroll
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
                    PreferenceKeys.AutoCleanupEnabled,
                    PreferenceKeys.KeepFavoriteArticles,
                    PreferenceKeys.KeepUnreadArticles
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
                ["Etiquetas"] = new List<string>
                {
                    PreferenceKeys.TagCloudEnabled,
                    PreferenceKeys.TagCloudMaxTags,
                    PreferenceKeys.AutoTaggingEnabled,
                    PreferenceKeys.ShowTagColors
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
                    PreferenceKeys.SyncFrequency,
                    PreferenceKeys.LastSyncDate
                },
                ["Teclado"] = new List<string>
                {
                    PreferenceKeys.KeyboardShortcutsEnabled,
                    PreferenceKeys.MarkAsReadShortcut,
                    PreferenceKeys.NextArticleShortcut,
                    PreferenceKeys.PrevArticleShortcut
                },
                ["Idioma"] = new List<string>
                {
                    PreferenceKeys.Language,
                    PreferenceKeys.DateFormat,
                    PreferenceKeys.TimeFormat,
                    PreferenceKeys.FirstDayOfWeek
                },
                ["Avanzado"] = new List<string>
                {
                    PreferenceKeys.DeveloperMode,
                    PreferenceKeys.EnableApi,
                    PreferenceKeys.ApiPort,
                    PreferenceKeys.WebhookUrl,
                    PreferenceKeys.EnablePlugins,
                    PreferenceKeys.PluginDirectory
                },
                ["Logging"] = new List<string>
                {
                    PreferenceKeys.LogLevel,
                    PreferenceKeys.LogRetentionDays,
                    PreferenceKeys.EnableVerboseLogging
                }
            };
        }

        #endregion

        #region Type Detection

        /// <summary>
        /// Determines the data type of a preference based on its key.
        /// </summary>
        /// <param name="key">The preference key.</param>
        /// <returns>The <see cref="PreferenceType"/> of the preference.</returns>
        /// <remarks>
        /// Used by UI to render appropriate input controls (checkbox, number field, dropdown, etc.).
        /// </remarks>
        public static PreferenceType GetPreferenceType(string key)
        {
            return key switch
            {
                // Booleanos
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
                           k == PreferenceKeys.EnableVerboseLogging ||
                           k == PreferenceKeys.KeepFavoriteArticles ||
                           k == PreferenceKeys.KeepUnreadArticles => PreferenceType.Boolean,

                // Enteros (rangos)
                PreferenceKeys.AutoMarkAsReadDelay => PreferenceType.Integer,
                PreferenceKeys.DefaultUpdateFrequency => PreferenceType.Integer,
                PreferenceKeys.MaxConcurrentDownloads => PreferenceType.Integer,
                PreferenceKeys.MaxRetryAttempts => PreferenceType.Integer,
                PreferenceKeys.ArticleRetentionDays => PreferenceType.Integer,
                PreferenceKeys.KeepReadArticles => PreferenceType.Integer,
                PreferenceKeys.NotificationDuration => PreferenceType.Integer,
                PreferenceKeys.TagCloudMaxTags => PreferenceType.Integer,
                PreferenceKeys.CacheSizeLimit => PreferenceType.Integer,
                PreferenceKeys.MaxArticlesInMemory => PreferenceType.Integer,
                PreferenceKeys.ImageCacheSize => PreferenceType.Integer,
                PreferenceKeys.SidebarWidth => PreferenceType.Integer,
                PreferenceKeys.ProxyPort => PreferenceType.Integer,
                PreferenceKeys.KeepBackupCopies => PreferenceType.Integer,
                PreferenceKeys.SyncFrequency => PreferenceType.Integer,
                PreferenceKeys.ApiPort => PreferenceType.Integer,
                PreferenceKeys.LogRetentionDays => PreferenceType.Integer,

                // Decimales
                PreferenceKeys.LineHeight => PreferenceType.Decimal,

                // Colores
                PreferenceKeys.AccentColor => PreferenceType.Color,

                // Enums / Options (dropdowns)
                PreferenceKeys.Theme => PreferenceType.Option,
                PreferenceKeys.DefaultView => PreferenceType.Option,
                PreferenceKeys.ArticleFontSize => PreferenceType.Option,
                PreferenceKeys.UiDensity => PreferenceType.Option,
                PreferenceKeys.ReaderViewMode => PreferenceType.Option,
                PreferenceKeys.TextJustification => PreferenceType.Option,
                PreferenceKeys.NightModeSchedule => PreferenceType.Option,
                PreferenceKeys.NotificationPosition => PreferenceType.Option,
                PreferenceKeys.BackupFrequency => PreferenceType.Option,
                PreferenceKeys.RulesProcessingOrder => PreferenceType.Option,
                PreferenceKeys.DateFormat => PreferenceType.Option,
                PreferenceKeys.TimeFormat => PreferenceType.Option,
                PreferenceKeys.FirstDayOfWeek => PreferenceType.Option,
                PreferenceKeys.Language => PreferenceType.Option,

                // Default
                _ => PreferenceType.String
            };
        }

        /// <summary>
        /// Gets the list of valid options for an enum-based preference.
        /// </summary>
        /// <param name="key">The preference key.</param>
        /// <returns>A list of valid option strings, or empty list if not an enum.</returns>
        /// <remarks>
        /// Used by UI to populate dropdown lists for enum-type preferences.
        /// </remarks>
        public static List<string> GetEnumOptions(string key)
        {
            return key switch
            {
                PreferenceKeys.Theme => new List<string> { "light", "dark", "system", "custom" },
                PreferenceKeys.DefaultView => new List<string> { "list", "grid", "magazine", "compact" },
                PreferenceKeys.ArticleFontSize => new List<string> { "small", "normal", "large", "xlarge" },
                PreferenceKeys.UiDensity => new List<string> { "compact", "normal", "comfortable" },
                PreferenceKeys.ReaderViewMode => new List<string> { "full", "clean", "original" },
                PreferenceKeys.TextJustification => new List<string> { "left", "justify" },
                PreferenceKeys.NightModeSchedule => new List<string> { "sunset", "manual" },
                PreferenceKeys.NotificationPosition => new List<string> { "top-right", "bottom-right", "top-left", "bottom-left" },
                PreferenceKeys.BackupFrequency => new List<string> { "daily", "weekly", "monthly" },
                PreferenceKeys.RulesProcessingOrder => new List<string> { "priority", "sequential" },
                PreferenceKeys.DateFormat => new List<string> { "relative", "absolute" },
                PreferenceKeys.TimeFormat => new List<string> { "12h", "24h" },
                PreferenceKeys.FirstDayOfWeek => new List<string> { "monday", "sunday" },
                PreferenceKeys.Language => new List<string> { "system", "es", "en", "fr", "de", "it", "pt", "ru", "zh" },
                _ => new List<string>()
            };
        }

        #endregion

        #region Default Values

        /// <summary>
        /// Gets the default value for a preference key.
        /// </summary>
        /// <param name="key">The preference key.</param>
        /// <returns>The default value as a string.</returns>
        /// <remarks>
        /// Defaults are defined in <see cref="PreferenceDefaults"/>.
        /// Returns empty string for unknown keys.
        /// </remarks>
        public static string GetDefaultValue(string key)
        {
            return key switch
            {
                // UI & Appearance
                PreferenceKeys.Theme => PreferenceDefaults.Theme,
                PreferenceKeys.AccentColor => PreferenceDefaults.AccentColor,
                PreferenceKeys.DefaultView => PreferenceDefaults.DefaultView,
                PreferenceKeys.ArticleFont => PreferenceDefaults.ArticleFont,
                PreferenceKeys.ArticleFontSize => PreferenceDefaults.ArticleFontSize,
                PreferenceKeys.UiDensity => PreferenceDefaults.UiDensity,
                PreferenceKeys.ShowSidebar => PreferenceDefaults.ShowSidebar.ToString(),
                PreferenceKeys.SidebarWidth => PreferenceDefaults.SidebarWidth.ToString(),
                PreferenceKeys.AnimationEnabled => PreferenceDefaults.AnimationEnabled.ToString(),

                // Article Reading
                PreferenceKeys.AutoMarkAsRead => PreferenceDefaults.AutoMarkAsRead.ToString(),
                PreferenceKeys.AutoMarkAsReadDelay => PreferenceDefaults.AutoMarkAsReadDelay.ToString(),
                PreferenceKeys.ShowImages => PreferenceDefaults.ShowImages.ToString(),
                PreferenceKeys.LazyLoadImages => PreferenceDefaults.LazyLoadImages.ToString(),
                PreferenceKeys.ReaderViewMode => PreferenceDefaults.ReaderViewMode,
                PreferenceKeys.TextJustification => PreferenceDefaults.TextJustification,
                PreferenceKeys.LineHeight => PreferenceDefaults.LineHeight.ToString(),
                PreferenceKeys.NightMode => PreferenceDefaults.NightMode.ToString(),
                PreferenceKeys.NightModeSchedule => PreferenceDefaults.NightModeSchedule,

                // Navigation & Links
                PreferenceKeys.OpenLinksInApp => PreferenceDefaults.OpenLinksInApp.ToString(),
                PreferenceKeys.OpenLinksBackground => PreferenceDefaults.OpenLinksBackground.ToString(),
                PreferenceKeys.UseExternalBrowser => PreferenceDefaults.UseExternalBrowser.ToString(),
                PreferenceKeys.ExternalBrowserPath => PreferenceDefaults.ExternalBrowserPath,
                PreferenceKeys.MiddleClickOpensBackground => PreferenceDefaults.MiddleClickOpensBackground.ToString(),
                PreferenceKeys.MarkAsReadOnScroll => PreferenceDefaults.MarkAsReadOnScroll.ToString(),

                // Feed Management
                PreferenceKeys.DefaultUpdateFrequency => PreferenceDefaults.DefaultUpdateFrequency.ToString(),
                PreferenceKeys.UpdateOnStartup => PreferenceDefaults.UpdateOnStartup.ToString(),
                PreferenceKeys.UpdateInBackground => PreferenceDefaults.UpdateInBackground.ToString(),
                PreferenceKeys.MaxConcurrentDownloads => PreferenceDefaults.MaxConcurrentDownloads.ToString(),
                PreferenceKeys.RetryFailedFeeds => PreferenceDefaults.RetryFailedFeeds.ToString(),
                PreferenceKeys.MaxRetryAttempts => PreferenceDefaults.MaxRetryAttempts.ToString(),
                PreferenceKeys.ArticleRetentionDays => PreferenceDefaults.ArticleRetentionDays.ToString(),
                PreferenceKeys.KeepReadArticles => PreferenceDefaults.KeepReadArticles.ToString(),
                PreferenceKeys.AutoCleanupEnabled => PreferenceDefaults.AutoCleanupEnabled.ToString(),
                PreferenceKeys.KeepFavoriteArticles => PreferenceDefaults.KeepFavoriteArticles.ToString(),
                PreferenceKeys.KeepUnreadArticles => PreferenceDefaults.KeepUnreadArticles.ToString(),

                // Rule System
                PreferenceKeys.RulesEnabled => PreferenceDefaults.RulesEnabled.ToString(),
                PreferenceKeys.RulesProcessingOrder => PreferenceDefaults.RulesProcessingOrder,
                PreferenceKeys.StopOnFirstMatch => PreferenceDefaults.StopOnFirstMatch.ToString(),
                PreferenceKeys.ProcessOldArticles => PreferenceDefaults.ProcessOldArticles.ToString(),
                PreferenceKeys.RuleNotificationSound => PreferenceDefaults.RuleNotificationSound,

                // Notifications
                PreferenceKeys.NotificationsEnabled => PreferenceDefaults.NotificationsEnabled.ToString(),
                PreferenceKeys.NotificationDuration => PreferenceDefaults.NotificationDuration.ToString(),
                PreferenceKeys.NotificationSound => PreferenceDefaults.NotificationSound,
                PreferenceKeys.NotificationPosition => PreferenceDefaults.NotificationPosition,
                PreferenceKeys.ShowNotificationPreview => PreferenceDefaults.ShowNotificationPreview.ToString(),
                PreferenceKeys.GroupNotifications => PreferenceDefaults.GroupNotifications.ToString(),
                PreferenceKeys.CriticalNotificationsOnly => PreferenceDefaults.CriticalNotificationsOnly.ToString(),
                PreferenceKeys.DoNotDisturbMode => PreferenceDefaults.DoNotDisturbMode.ToString(),
                PreferenceKeys.DoNotDisturbHours => PreferenceDefaults.DoNotDisturbHours,

                // Tag System
                PreferenceKeys.TagCloudEnabled => PreferenceDefaults.TagCloudEnabled.ToString(),
                PreferenceKeys.TagCloudMaxTags => PreferenceDefaults.TagCloudMaxTags.ToString(),
                PreferenceKeys.AutoTaggingEnabled => PreferenceDefaults.AutoTaggingEnabled.ToString(),
                PreferenceKeys.ShowTagColors => PreferenceDefaults.ShowTagColors.ToString(),

                // Performance
                PreferenceKeys.CacheEnabled => PreferenceDefaults.CacheEnabled.ToString(),
                PreferenceKeys.CacheSizeLimit => PreferenceDefaults.CacheSizeLimit.ToString(),
                PreferenceKeys.PreloadArticles => PreferenceDefaults.PreloadArticles.ToString(),
                PreferenceKeys.MaxArticlesInMemory => PreferenceDefaults.MaxArticlesInMemory.ToString(),
                PreferenceKeys.EnableImageCache => PreferenceDefaults.EnableImageCache.ToString(),
                PreferenceKeys.ImageCacheSize => PreferenceDefaults.ImageCacheSize.ToString(),

                // Privacy & Security
                PreferenceKeys.ClearHistoryOnExit => PreferenceDefaults.ClearHistoryOnExit.ToString(),
                PreferenceKeys.SendUsageStatistics => PreferenceDefaults.SendUsageStatistics.ToString(),
                PreferenceKeys.BlockTrackers => PreferenceDefaults.BlockTrackers.ToString(),
                PreferenceKeys.HttpsOnly => PreferenceDefaults.HttpsOnly.ToString(),
                PreferenceKeys.ProxyEnabled => PreferenceDefaults.ProxyEnabled.ToString(),
                PreferenceKeys.ProxyAddress => PreferenceDefaults.ProxyAddress,
                PreferenceKeys.ProxyPort => PreferenceDefaults.ProxyPort.ToString(),

                // Backup & Sync
                PreferenceKeys.AutoBackupEnabled => PreferenceDefaults.AutoBackupEnabled.ToString(),
                PreferenceKeys.BackupFrequency => PreferenceDefaults.BackupFrequency,
                PreferenceKeys.BackupLocation => PreferenceDefaults.BackupLocation,
                PreferenceKeys.KeepBackupCopies => PreferenceDefaults.KeepBackupCopies.ToString(),
                PreferenceKeys.SyncEnabled => PreferenceDefaults.SyncEnabled.ToString(),
                PreferenceKeys.SyncFrequency => PreferenceDefaults.SyncFrequency.ToString(),
                PreferenceKeys.LastSyncDate => string.Empty,

                // Keyboard Shortcuts
                PreferenceKeys.KeyboardShortcutsEnabled => PreferenceDefaults.KeyboardShortcutsEnabled.ToString(),
                PreferenceKeys.MarkAsReadShortcut => PreferenceDefaults.MarkAsReadShortcut,
                PreferenceKeys.NextArticleShortcut => PreferenceDefaults.NextArticleShortcut,
                PreferenceKeys.PrevArticleShortcut => PreferenceDefaults.PrevArticleShortcut,

                // Language & Region
                PreferenceKeys.Language => PreferenceDefaults.Language,
                PreferenceKeys.DateFormat => PreferenceDefaults.DateFormat,
                PreferenceKeys.TimeFormat => PreferenceDefaults.TimeFormat,
                PreferenceKeys.FirstDayOfWeek => PreferenceDefaults.FirstDayOfWeek,

                // Advanced
                PreferenceKeys.DeveloperMode => PreferenceDefaults.DeveloperMode.ToString(),
                PreferenceKeys.EnableApi => PreferenceDefaults.EnableApi.ToString(),
                PreferenceKeys.ApiPort => PreferenceDefaults.ApiPort.ToString(),
                PreferenceKeys.WebhookUrl => PreferenceDefaults.WebhookUrl,
                PreferenceKeys.EnablePlugins => PreferenceDefaults.EnablePlugins.ToString(),
                PreferenceKeys.PluginDirectory => PreferenceDefaults.PluginDirectory,

                // Logging
                PreferenceKeys.LogLevel => PreferenceDefaults.LogLevel,
                PreferenceKeys.LogRetentionDays => PreferenceDefaults.LogRetentionDays.ToString(),
                PreferenceKeys.EnableVerboseLogging => PreferenceDefaults.EnableVerboseLogging.ToString(),

                _ => string.Empty
            };
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates a preference value according to its key's validation rules.
        /// </summary>
        /// <param name="key">The preference key.</param>
        /// <param name="value">The value to validate.</param>
        /// <returns>True if the value is valid for the specified key; otherwise, false.</returns>
        /// <remarks>
        /// Performs type-specific validation:
        /// <list type="bullet">
        /// <item>Enum values: checks against allowed options</item>
        /// <item>Numeric values: range checking and parsing</item>
        /// <item>Boolean values: verifies "true"/"false" strings</item>
        /// <item>Colors: validates hex format (#RGB, #RRGGBB, #RRGGBBAA)</item>
        /// <item>URLs: validates URI format</item>
        /// <item>Time ranges: validates HH:MM-HH:MM format</item>
        /// </list>
        /// </remarks>
        public static bool ValidateValue(string key, string value)
        {
            try
            {
                return key switch
                {
                    // Enum/Option validations
                    PreferenceKeys.Theme => new[] { "light", "dark", "system", "custom" }.Contains(value),
                    PreferenceKeys.DefaultView => new[] { "list", "grid", "magazine", "compact" }.Contains(value),
                    PreferenceKeys.ArticleFontSize => new[] { "small", "normal", "large", "xlarge" }.Contains(value),
                    PreferenceKeys.UiDensity => new[] { "compact", "normal", "comfortable" }.Contains(value),
                    PreferenceKeys.ReaderViewMode => new[] { "full", "clean", "original" }.Contains(value),
                    PreferenceKeys.TextJustification => new[] { "left", "justify" }.Contains(value),
                    PreferenceKeys.NightModeSchedule => new[] { "sunset", "manual" }.Contains(value) || _timeRegex.IsMatch(value),
                    PreferenceKeys.NotificationPosition => new[] { "top-right", "bottom-right", "top-left", "bottom-left" }.Contains(value),
                    PreferenceKeys.BackupFrequency => new[] { "daily", "weekly", "monthly" }.Contains(value),
                    PreferenceKeys.RulesProcessingOrder => new[] { "priority", "sequential" }.Contains(value),
                    PreferenceKeys.DateFormat => new[] { "relative", "absolute" }.Contains(value),
                    PreferenceKeys.TimeFormat => new[] { "12h", "24h" }.Contains(value),
                    PreferenceKeys.FirstDayOfWeek => new[] { "monday", "sunday" }.Contains(value),
                    PreferenceKeys.Language => new[] { "system", "es", "en", "fr", "de", "it", "pt", "ru", "zh" }.Contains(value),

                    // Numeric validations (con constantes)
                    var k when k == PreferenceKeys.AutoMarkAsReadDelay =>
                        int.TryParse(value, out int delay) && delay >= MIN_DELAY_SECONDS && delay <= MAX_DELAY_SECONDS,

                    var k when k == PreferenceKeys.DefaultUpdateFrequency =>
                        int.TryParse(value, out int freq) && freq >= MIN_UPDATE_FREQUENCY && freq <= MAX_UPDATE_FREQUENCY,

                    var k when k == PreferenceKeys.MaxConcurrentDownloads =>
                        int.TryParse(value, out int downloads) && downloads >= MIN_CONCURRENT_DOWNLOADS && downloads <= MAX_CONCURRENT_DOWNLOADS,

                    var k when k == PreferenceKeys.MaxRetryAttempts =>
                        int.TryParse(value, out int retries) && retries >= MIN_RETRY_ATTEMPTS && retries <= MAX_RETRY_ATTEMPTS,

                    var k when k == PreferenceKeys.ArticleRetentionDays =>
                        int.TryParse(value, out int days) && (days == 0 || (days >= MIN_RETENTION_DAYS && days <= MAX_RETENTION_DAYS)),

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
                        int.TryParse(value, out int width) && width >= MIN_SIDEBAR_WIDTH && width <= MAX_SIDEBAR_WIDTH,

                    var k when k == PreferenceKeys.ProxyPort =>
                        int.TryParse(value, out int port) && port >= MIN_PROXY_PORT && port <= MAX_PROXY_PORT,

                    var k when k == PreferenceKeys.KeepBackupCopies =>
                        int.TryParse(value, out int copies) && copies >= 1 && copies <= 100,

                    var k when k == PreferenceKeys.SyncFrequency =>
                        int.TryParse(value, out int sync) && sync >= 1 && sync <= 1440,

                    var k when k == PreferenceKeys.ApiPort =>
                        int.TryParse(value, out int apiPort) && apiPort >= MIN_API_PORT && apiPort <= MAX_API_PORT,

                    var k when k == PreferenceKeys.LogRetentionDays =>
                        int.TryParse(value, out int logDays) && logDays >= 1 && logDays <= 365,

                    // Decimal validations
                    var k when k == PreferenceKeys.LineHeight =>
                        double.TryParse(value, out double height) && height >= MIN_LINE_HEIGHT && height <= MAX_LINE_HEIGHT,

                    // Hex color validation (con regex compilado)
                    var k when k == PreferenceKeys.AccentColor => _hexColorRegex.IsMatch(value),

                    // Path validations (basic safety)
                    var k when k == PreferenceKeys.ExternalBrowserPath ||
                               k == PreferenceKeys.BackupLocation ||
                               k == PreferenceKeys.PluginDirectory ||
                               k == PreferenceKeys.RuleNotificationSound ||
                               k == PreferenceKeys.NotificationSound =>
                        string.IsNullOrEmpty(value) || (!value.Contains("..") && _safePathRegex.IsMatch(value)),

                    // Time range validation (con regex compilado)
                    var k when k == PreferenceKeys.DoNotDisturbHours =>
                        string.IsNullOrEmpty(value) || _timeRangeRegex.IsMatch(value),

                    // URL validation
                    var k when k == PreferenceKeys.WebhookUrl =>
                        string.IsNullOrEmpty(value) || Uri.TryCreate(value, UriKind.Absolute, out _),

                    var k when k == PreferenceKeys.ProxyAddress =>
                        string.IsNullOrEmpty(value) ||
                        Uri.TryCreate($"http://{value}", UriKind.Absolute, out _) ||
                        System.Net.IPAddress.TryParse(value, out _),

                    // Boolean validations (patrones automáticos)
                    var k when IsBooleanKey(k) => bool.TryParse(value, out _),

                    // Text validations
                    var k when k == PreferenceKeys.ArticleFont =>
                        !string.IsNullOrEmpty(value) && value.Length <= 50,

                    var k when k == PreferenceKeys.MarkAsReadShortcut ||
                               k == PreferenceKeys.NextArticleShortcut ||
                               k == PreferenceKeys.PrevArticleShortcut =>
                        !string.IsNullOrEmpty(value) && value.Length <= 20,

                    // Default case - accept any value
                    _ => true
                };
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if a key represents a boolean preference based on naming conventions.
        /// </summary>
        private static bool IsBooleanKey(string key)
        {
            return key.EndsWith("_enabled") ||
                   key.StartsWith("enable_") ||
                   key.StartsWith("show_") ||
                   key.StartsWith("auto_") ||
                   key.StartsWith("use_") ||
                   key.StartsWith("open_") ||
                   key.StartsWith("lazy_") ||
                   key.EndsWith("_only") ||
                   key.Contains("_background") ||
                   key == PreferenceKeys.NightMode ||
                   key == PreferenceKeys.MarkAsReadOnScroll ||
                   key == PreferenceKeys.RetryFailedFeeds ||
                   key == PreferenceKeys.AutoCleanupEnabled ||
                   key == PreferenceKeys.StopOnFirstMatch ||
                   key == PreferenceKeys.ProcessOldArticles ||
                   key == PreferenceKeys.GroupNotifications ||
                   key == PreferenceKeys.DoNotDisturbMode ||
                   key == PreferenceKeys.ClearHistoryOnExit ||
                   key == PreferenceKeys.SendUsageStatistics ||
                   key == PreferenceKeys.BlockTrackers ||
                   key == PreferenceKeys.HttpsOnly ||
                   key == PreferenceKeys.ProxyEnabled ||
                   key == PreferenceKeys.KeyboardShortcutsEnabled ||
                   key == PreferenceKeys.DeveloperMode ||
                   key == PreferenceKeys.EnablePlugins ||
                   key == PreferenceKeys.EnableVerboseLogging ||
                   key == PreferenceKeys.KeepFavoriteArticles ||
                   key == PreferenceKeys.KeepUnreadArticles;
        }

        #endregion
    }

    #region Supporting Enums

    /// <summary>
    /// Enum representing the data type of a preference.
    /// Used by UI to render appropriate input controls.
    /// </summary>
    public enum PreferenceType
    {
        /// <summary>Plain text input</summary>
        String,
        /// <summary>Checkbox for true/false</summary>
        Boolean,
        /// <summary>Number input with optional spinner</summary>
        Integer,
        /// <summary>Number input with decimal support</summary>
        Decimal,
        /// <summary>Color picker</summary>
        Color,
        /// <summary>Dropdown select from predefined options</summary>
        Option
    }

    #endregion
}