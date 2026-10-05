using AutoMapper;
using NeonSuit.RSSReader.Core.Models.Cleanup;
using NeonSuit.RSSReader.Core.DTOs.Cleanup;
using NeonSuit.RSSReader.Core.DTOs.System;

namespace NeonSuit.RSSReader.Core.Profiles;

/// <summary>Mappings used by database maintenance service contracts.</summary>
public sealed class CleanupProfile : Profile
{
    /// <summary>Configures cleanup mappings.</summary>
    public CleanupProfile()
    {
        CreateMap<CleanupConfiguration, CleanupConfigurationDto>().ReverseMap();
        CreateMap<CleanupResult, CleanupResultDto>();
        CreateMap<CleanupAnalysis, CleanupAnalysisDto>();
        CreateMap<ImageCacheCleanupResult, ImageCacheCleanupResultDto>();
        CreateMap<DatabaseStatistics, DatabaseStatisticsDto>()
            .ForMember(d => d.TotalArticleCount, o => o.MapFrom(s => s.TotalArticles))
            .ForMember(d => d.ReadArticleCount, o => o.MapFrom(s => s.ReadArticles))
            .ForMember(d => d.UnreadArticleCount, o => o.MapFrom(s => s.UnreadArticles))
            .ForMember(d => d.FavoriteArticleCount, o => o.MapFrom(s => s.FavoriteArticles))
            .ForMember(d => d.FeedCount, o => o.MapFrom(s => s.TotalFeeds))
            .ForMember(d => d.CategoryCount, o => o.MapFrom(s => s.TotalCategories))
            .ForMember(d => d.ImageCacheCount, o => o.Ignore())
            .ForMember(d => d.ImageCacheSizeBytes, o => o.Ignore())
            .ForMember(d => d.FragmentationPercent, o => o.Ignore())
            .ForMember(d => d.LastCleanupTimestamp, o => o.Ignore())
            .ForMember(d => d.LastCleanupResult, o => o.Ignore());
        CreateMap<IntegrityCheckResult, IntegrityCheckResultDto>();
    }
}
