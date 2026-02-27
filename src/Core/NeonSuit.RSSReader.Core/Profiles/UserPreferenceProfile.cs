// =======================================================
// Core/Profiles/UserPreferenceProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Preferences;
using NeonSuit.RSSReader.Core.Models;
using System;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for UserPreferences entity mappings.
    /// Configures transformations between UserPreferences and its related DTOs.
    /// </summary>
    public class UserPreferenceProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserPreferenceProfile"/> class.
        /// </summary>
        public UserPreferenceProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region UserPreferences → PreferenceDto (complete)

            CreateMap<UserPreferences, PreferenceDto>()
                .ForMember(dest => dest.BoolValue,
                    opt => opt.MapFrom(src => src.BoolValue))
                .ForMember(dest => dest.IntValue,
                    opt => opt.MapFrom(src => src.IntValue))
                .ForMember(dest => dest.DoubleValue,
                    opt => opt.MapFrom(src => src.DoubleValue))
                .ForMember(dest => dest.Type,
                    opt => opt.MapFrom(src => GetPreferenceType(src.Key)))
                .ForMember(dest => dest.DisplayName,
                    opt => opt.MapFrom(src => FormatDisplayName(src.Key)))
                .ForMember(dest => dest.Description,
                    opt => opt.Ignore()) // Not stored in entity, would need lookup
                .ForMember(dest => dest.Category,
                    opt => opt.MapFrom(src => GetCategoryFromKey(src.Key)));

            #endregion

            #region UserPreferences → PreferenceSummaryDto (lightweight)

            CreateMap<UserPreferences, PreferenceSummaryDto>()
                .ForMember(dest => dest.Type,
                    opt => opt.MapFrom(src => GetPreferenceType(src.Key)))
                .ForMember(dest => dest.DisplayName,
                    opt => opt.MapFrom(src => FormatDisplayName(src.Key)))
                .ForMember(dest => dest.Category,
                    opt => opt.MapFrom(src => GetCategoryFromKey(src.Key)));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region UpdatePreferenceDto → UserPreferences

            // Note: This mapping is typically used when updating an existing entity
            CreateMap<UpdatePreferenceDto, UserPreferences>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastModified,
                    opt => opt.MapFrom(_ => DateTime.UtcNow));

            #endregion

            #region Create from Key-Value pair (for imports)

            CreateMap<KeyValuePair<string, string>, UserPreferences>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Key,
                    opt => opt.MapFrom(src => src.Key))
                .ForMember(dest => dest.Value,
                    opt => opt.MapFrom(src => src.Value))
                .ForMember(dest => dest.LastModified,
                    opt => opt.MapFrom(_ => DateTime.UtcNow));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Determines the preference type based on key naming conventions.
        /// </summary>
        private static PreferenceType GetPreferenceType(string key)
        {
            return key switch
            {
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

                var k when k == PreferenceKeys.AutoMarkAsReadDelay ||
                           k == PreferenceKeys.DefaultUpdateFrequency ||
                           k == PreferenceKeys.MaxConcurrentDownloads ||
                           k == PreferenceKeys.MaxRetryAttempts ||
                           k == PreferenceKeys.ArticleRetentionDays ||
                           k == PreferenceKeys.KeepReadArticles ||
                           k == PreferenceKeys.NotificationDuration ||
                           k == PreferenceKeys.TagCloudMaxTags ||
                           k == PreferenceKeys.CacheSizeLimit ||
                           k == PreferenceKeys.MaxArticlesInMemory ||
                           k == PreferenceKeys.ImageCacheSize ||
                           k == PreferenceKeys.SidebarWidth ||
                           k == PreferenceKeys.ProxyPort ||
                           k == PreferenceKeys.KeepBackupCopies ||
                           k == PreferenceKeys.SyncFrequency ||
                           k == PreferenceKeys.ApiPort ||
                           k == PreferenceKeys.LogRetentionDays => PreferenceType.Integer,

                var k when k == PreferenceKeys.LineHeight => PreferenceType.Decimal,

                var k when k == PreferenceKeys.AccentColor => PreferenceType.Color,

                var k when k == PreferenceKeys.Theme ||
                           k == PreferenceKeys.DefaultView ||
                           k == PreferenceKeys.ArticleFontSize ||
                           k == PreferenceKeys.UiDensity ||
                           k == PreferenceKeys.ReaderViewMode ||
                           k == PreferenceKeys.TextJustification ||
                           k == PreferenceKeys.NightModeSchedule ||
                           k == PreferenceKeys.NotificationPosition ||
                           k == PreferenceKeys.BackupFrequency ||
                           k == PreferenceKeys.RulesProcessingOrder ||
                           k == PreferenceKeys.DateFormat ||
                           k == PreferenceKeys.TimeFormat ||
                           k == PreferenceKeys.FirstDayOfWeek ||
                           k == PreferenceKeys.Language => PreferenceType.Option,

                _ => PreferenceType.String
            };
        }

        /// <summary>
        /// Formats a preference key into a human-readable display name.
        /// </summary>
        private static string FormatDisplayName(string key)
        {
            // Convert "auto_mark_as_read" to "Auto Mark As Read"
            var words = key.Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        /// <summary>
        /// Extracts category from preference key (first part before first underscore).
        /// </summary>
        private static string? GetCategoryFromKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            var firstUnderscore = key.IndexOf('_');
            return firstUnderscore > 0 ? key.Substring(0, firstUnderscore) : "General";
        }

        #endregion
    }
}