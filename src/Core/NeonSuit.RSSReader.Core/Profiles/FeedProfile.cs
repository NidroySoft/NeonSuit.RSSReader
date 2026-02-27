// =======================================================
// Core/Profiles/FeedProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for Feed entity mappings.
    /// Configures transformations between Feed and its related DTOs.
    /// </summary>
    public class FeedProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeedProfile"/> class.
        /// </summary>
        public FeedProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region Feed → FeedDto (complete)

            CreateMap<Feed, FeedDto>()
                .ForMember(dest => dest.CategoryName,
                    opt => opt.MapFrom(src => src.Category == null ? null : src.Category.Name))
                .ForMember(dest => dest.EffectiveRetentionDays,
                    opt => opt.MapFrom(src => src.ArticleRetentionDays ?? 30))
                .ForMember(dest => dest.HealthStatus,
                    opt => opt.MapFrom(src => src.HealthStatus))
                .ForMember(dest => dest.UnreadCount,
                    opt => opt.MapFrom(src => src.UnreadCount));

            #endregion

            #region Feed → FeedSummaryDto (lightweight)

            CreateMap<Feed, FeedSummaryDto>()
                .ForMember(dest => dest.CategoryName,
                    opt => opt.MapFrom(src => src.Category == null ? null : src.Category.Name))
                .ForMember(dest => dest.HealthStatus,
                    opt => opt.MapFrom(src => src.HealthStatus))
                .ForMember(dest => dest.LastUpdatedDisplay,
                    opt => opt.MapFrom(src => GetTimeAgo(src.LastUpdated)))
                .ForMember(dest => dest.UnreadCount,
                    opt => opt.MapFrom(src => src.UnreadCount));

            #endregion

            #region Feed → FeedHealthDto (monitoring)

            CreateMap<Feed, FeedHealthDto>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.HealthStatus))
                .ForMember(dest => dest.TimeSinceLastUpdate,
                    opt => opt.MapFrom(src => GetTimeAgo(src.LastUpdated)))
                .ForMember(dest => dest.NextUpdate,
                    opt => opt.MapFrom(src => src.NextUpdateSchedule));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateFeedDto → Feed

            CreateMap<CreateFeedDto, Feed>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdated,
                    opt => opt.Ignore())
                .ForMember(dest => dest.NextUpdateSchedule,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.FailureCount,
                    opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.LastError,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ETag,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastModifiedHeader,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastFullSync,
                    opt => opt.Ignore())
                .ForMember(dest => dest.TotalArticleCount,
                    opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.IsUpdating,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UnreadCount,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Articles,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Category,
                    opt => opt.Ignore());

            #endregion

            #region UpdateFeedDto → Feed (partial updates)

            CreateMap<UpdateFeedDto, Feed>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Url,
                    opt => opt.Ignore()) // URL cannot be changed
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastUpdated,
                    opt => opt.Ignore()) // Updated by sync logic
                .ForMember(dest => dest.NextUpdateSchedule,
                    opt => opt.Ignore()) // Updated by scheduler
                .ForMember(dest => dest.FailureCount,
                    opt => opt.Condition(src => src.ResetFailureCount == true))
                .ForMember(dest => dest.FailureCount,
                    opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.LastError,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ETag,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastModifiedHeader,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastFullSync,
                    opt => opt.Ignore())
                .ForMember(dest => dest.TotalArticleCount,
                    opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Generates a human-readable "time ago" string from a date.
        /// </summary>
        private static string GetTimeAgo(DateTime? date)
        {
            if (!date.HasValue)
                return "Never";

            var diff = DateTime.UtcNow - date.Value;

            return diff.TotalDays switch
            {
                >= 7 => $"{(int)(diff.TotalDays / 7)} weeks ago",
                >= 1 => $"{(int)diff.TotalDays} days ago",
                >= 1.0 / 24 => $"{(int)diff.TotalHours} hours ago",
                _ => "Just now"
            };
        }

        #endregion
    }
}