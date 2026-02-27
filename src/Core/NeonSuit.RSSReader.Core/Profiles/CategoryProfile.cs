// =======================================================
// Core/Profiles/CategoryProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Categories;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for Category entity mappings.
    /// Configures transformations between Category and its related DTOs.
    /// </summary>
    public class CategoryProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CategoryProfile"/> class.
        /// </summary>
        public CategoryProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region Category → CategoryDto

            CreateMap<Category, CategoryDto>()
                .ForMember(dest => dest.FeedCount,
                    opt => opt.MapFrom(src => src.Feeds == null ? 0 : src.Feeds.Count))
                .ForMember(dest => dest.UnreadCount,
                    opt => opt.MapFrom(src => CalculateUnreadCount(src)))
                .ForMember(dest => dest.Depth,
                    opt => opt.MapFrom(src => CalculateDepth(src)))
                .ForMember(dest => dest.FullPath,
                    opt => opt.MapFrom(src => BuildFullPath(src)));

            #endregion

            #region Category → CategoryTreeDto (recursive)

            CreateMap<Category, CategoryTreeDto>()
                .ForMember(dest => dest.FeedCount,
                    opt => opt.MapFrom(src => src.Feeds == null ? 0 : src.Feeds.Count))
                .ForMember(dest => dest.UnreadCount,
                    opt => opt.MapFrom(src => CalculateUnreadCount(src)))
                .ForMember(dest => dest.Depth,
                    opt => opt.MapFrom(src => CalculateDepth(src)))
                .ForMember(dest => dest.Children,
                    opt => opt.MapFrom(src => src.Subcategories))
                .ForMember(dest => dest.IsExpanded,
                    opt => opt.MapFrom(src => src.IsExpanded));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateCategoryDto → Category

            CreateMap<CreateCategoryDto, Category>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.MapFrom(_ => System.DateTime.UtcNow))
                .ForMember(dest => dest.LastModified,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Feeds,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Subcategories,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ParentCategory,
                    opt => opt.Ignore());

            #endregion

            #region UpdateCategoryDto → Category (partial updates)

            CreateMap<UpdateCategoryDto, Category>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Calculates the total unread count for a category by summing unread articles from all its feeds.
        /// </summary>
        private static int CalculateUnreadCount(Category category)
        {
            if (category?.Feeds == null)
                return 0;

            return category.Feeds
                .Where(f => f != null)
                .Sum(f => f.UnreadCount);
        }

        /// <summary>
        /// Calculates the depth of a category in the hierarchy.
        /// </summary>
        private static int CalculateDepth(Category category)
        {
            if (category == null)
                return 0;

            int depth = 0;
            var current = category.ParentCategory;
            while (current != null)
            {
                depth++;
                current = current.ParentCategory;
            }
            return depth;
        }

        /// <summary>
        /// Builds the full hierarchical path of a category.
        /// </summary>
        private static string BuildFullPath(Category category)
        {
            if (category == null)
                return string.Empty;

            var pathParts = new List<string> { category.Name };
            var current = category.ParentCategory;

            while (current != null)
            {
                pathParts.Add(current.Name);
                current = current.ParentCategory;
            }

            pathParts.Reverse();
            return string.Join(" / ", pathParts);
        }

        #endregion
    }
}