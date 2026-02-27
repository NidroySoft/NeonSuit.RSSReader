using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Article;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for Article entity mappings.
    /// Configures transformations between Article and its related DTOs.
    /// </summary>
    public class ArticleProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleProfile"/> class.
        /// Sets up all mapping configurations for Article entity.
        /// </summary>
        public ArticleProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region Basic Summary DTO (for lists)

            CreateMap<Article, ArticleSummaryDto>()
                .ForMember(dest => dest.FeedTitle,
                    opt => opt.MapFrom(src => src.Feed == null ? "Unknown Feed" : src.Feed.Title))
                .ForMember(dest => dest.FeedIconUrl,
                    opt => opt.MapFrom(src => src.Feed == null ? null : src.Feed.IconUrl))
                .ForMember(dest => dest.TagCount,
                    opt => opt.MapFrom(src => src.ArticleTags == null ? 0 : src.ArticleTags.Count))
                .ForMember(dest => dest.HasEnclosure,
                    opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.EnclosureUrl)));

            #endregion

            #region List Item DTO (ultra-light for virtualized lists)

            CreateMap<Article, ArticleListItemDto>()
                .ForMember(dest => dest.IsUnread,
                    opt => opt.MapFrom(src => src.Status == ArticleStatus.Unread))
                .ForMember(dest => dest.HasImage,
                    opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ImageUrl)))
                .ForMember(dest => dest.FeedTitle,
                    opt => opt.MapFrom(src => src.Feed == null ? "Unknown Feed" : src.Feed.Title));

            #endregion

            #region Detail DTO (full article view)

            CreateMap<Article, ArticleDetailDto>()
                .ForMember(dest => dest.FeedTitle,
                    opt => opt.MapFrom(src => src.Feed == null ? "Unknown Feed" : src.Feed.Title))
                .ForMember(dest => dest.FeedWebsiteUrl,
                    opt => opt.MapFrom(src => src.Feed == null ? string.Empty : src.Feed.WebsiteUrl))
                .ForMember(dest => dest.FeedIconUrl,
                    opt => opt.MapFrom(src => src.Feed == null ? null : src.Feed.IconUrl))
                .ForMember(dest => dest.Tags,
                    opt => opt.MapFrom(src => ExtractTagNames(src)))
                .ForMember(dest => dest.IsAudio,
                    opt => opt.MapFrom(src => IsAudioType(src.EnclosureType ?? string.Empty)))
                .ForMember(dest => dest.IsVideo,
                    opt => opt.MapFrom(src => IsVideoType(src.EnclosureType ?? string.Empty)));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region Create DTO → Entity

            CreateMap<CreateArticleDto, Article>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.AddedDate, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => ArticleStatus.Unread))
                .ForMember(dest => dest.IsStarred, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.IsFavorite, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.IsNotified, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.ProcessedByRules, opt => opt.MapFrom(_ => false))
                .ForMember(dest => dest.ReadPercentage, opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.ContentHash, opt => opt.Ignore())
                .ForMember(dest => dest.Feed, opt => opt.Ignore())
                .ForMember(dest => dest.NotificationLogs, opt => opt.Ignore())
                .ForMember(dest => dest.ArticleTags, opt => opt.Ignore());

            #endregion

            #region Update DTO → Entity (partial updates)

            CreateMap<UpdateArticleDto, Article>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion

            #region State DTO → Entity (quick state changes)

            CreateMap<ArticleStateDto, Article>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Extracts tag names from an article's ArticleTags collection.
        /// </summary>
        /// <param name="article">The source article.</param>
        /// <returns>A list of non-empty tag names.</returns>
        private static List<string> ExtractTagNames(Article article)
        {
            if (article == null || article.ArticleTags == null)
            {
                return new List<string>();
            }

            var tagNames = new List<string>();
            foreach (var articleTag in article.ArticleTags)
            {
                if (articleTag != null && articleTag.Tag != null)
                {
                    var tagName = articleTag.Tag.Name;
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        tagNames.Add(tagName);
                    }
                }
            }
            return tagNames;
        }

        /// <summary>
        /// Determines if an enclosure type is audio format.
        /// </summary>
        /// <param name="enclosureType">The MIME type of the enclosure.</param>
        /// <returns>True if the type starts with "audio/".</returns>
        private static bool IsAudioType(string enclosureType)
        {
            return !string.IsNullOrEmpty(enclosureType) && enclosureType.StartsWith("audio/");
        }

        /// <summary>
        /// Determines if an enclosure type is video format.
        /// </summary>
        /// <param name="enclosureType">The MIME type of the enclosure.</param>
        /// <returns>True if the type starts with "video/".</returns>
        private static bool IsVideoType(string enclosureType)
        {
            return !string.IsNullOrEmpty(enclosureType) && enclosureType.StartsWith("video/");
        }

        #endregion
    }
}