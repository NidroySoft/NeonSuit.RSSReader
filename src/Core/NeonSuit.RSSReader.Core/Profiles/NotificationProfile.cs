// =======================================================
// Core/Profiles/NotificationProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Notifications;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for NotificationLog entity mappings.
    /// Configures transformations between NotificationLog and its related DTOs.
    /// </summary>
    public class NotificationProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationProfile"/> class.
        /// </summary>
        public NotificationProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region NotificationLog → NotificationDto (complete)

            CreateMap<NotificationLog, NotificationDto>()
                .ForMember(dest => dest.ArticleTitle,
                    opt => opt.MapFrom(src => src.Article == null ? "Unknown Article" : src.Article.Title))
                .ForMember(dest => dest.RuleName,
                    opt => opt.MapFrom(src => src.Rule == null ? null : src.Rule.Name))
                .ForMember(dest => dest.TimeAgo,
                    opt => opt.MapFrom(src => GetTimeAgo(src.SentAt)))
                .ForMember(dest => dest.ResponseTimeSeconds,
                    opt => opt.MapFrom(src => CalculateResponseTime(src)));

            #endregion

            #region NotificationLog → NotificationSummaryDto (lightweight)

            CreateMap<NotificationLog, NotificationSummaryDto>()
                .ForMember(dest => dest.ArticleTitle,
                    opt => opt.MapFrom(src => src.Article == null ? "Unknown Article" : src.Article.Title))
                .ForMember(dest => dest.TimeAgo,
                    opt => opt.MapFrom(src => GetTimeAgo(src.SentAt)))
                .ForMember(dest => dest.Message,
                    opt => opt.MapFrom(src => TruncateMessage(src.Message, 100)));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateNotificationDto → NotificationLog

            CreateMap<CreateNotificationDto, NotificationLog>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.SentAt,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Action,
                    opt => opt.MapFrom(_ => NotificationAction.None))
                .ForMember(dest => dest.ActionAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Delivered,
                    opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Error,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Article,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Rule,
                    opt => opt.Ignore());

            #endregion

            #region UpdateNotificationActionDto → NotificationLog (partial)

            // Note: This is typically handled by service, not AutoMapper
            // because it requires loading the entity first.
            // Included here for completeness if needed.

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Generates a human-readable "time ago" string from a UTC date.
        /// </summary>
        private static string GetTimeAgo(DateTime date)
        {
            var diff = DateTime.UtcNow - date;

            return diff.TotalMinutes switch
            {
                < 1 => "Just now",
                < 60 => $"{(int)diff.TotalMinutes} min ago",
                < 1440 => $"{(int)diff.TotalHours} hours ago",
                _ => $"{(int)diff.TotalDays} days ago"
            };
        }

        /// <summary>
        /// Calculates response time in seconds between sent and user action.
        /// </summary>
        private static double? CalculateResponseTime(NotificationLog notification)
        {
            if (notification.ActionAt.HasValue)
            {
                return (notification.ActionAt.Value - notification.SentAt).TotalSeconds;
            }
            return null;
        }

        /// <summary>
        /// Truncates a message to the specified maximum length.
        /// </summary>
        private static string TruncateMessage(string message, int maxLength)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            if (message.Length <= maxLength)
                return message;

            return message.Substring(0, maxLength - 3) + "...";
        }

        #endregion
    }
}