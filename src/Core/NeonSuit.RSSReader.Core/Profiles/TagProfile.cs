// =======================================================
// Core/Profiles/TagProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Models;
using System;
using System.Linq;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for Tag entity mappings.
    /// Configures transformations between Tag and its related DTOs.
    /// </summary>
    public class TagProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TagProfile"/> class.
        /// </summary>
        public TagProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region Tag → TagDto (complete)

            CreateMap<Tag, TagDto>()
                .ForMember(dest => dest.DarkColor,
                    opt => opt.MapFrom(src => CalculateDarkColor(src.Color)))
                .ForMember(dest => dest.DisplayName,
                    opt => opt.MapFrom(src => src.IsPinned ? $"📌 {src.Name}" : src.Name))
                .ForMember(dest => dest.LastUsedTimeAgo,
                    opt => opt.MapFrom(src => GetTimeAgo(src.LastUsedAt)));

            #endregion

            #region Tag → TagSummaryDto (lightweight)

            CreateMap<Tag, TagSummaryDto>()
                .ForMember(dest => dest.DisplayName,
                    opt => opt.MapFrom(src => src.IsPinned ? $"📌 {src.Name}" : src.Name));

            #endregion

            #region Tag → TagCloudDto (for tag cloud visualization)

            CreateMap<Tag, TagCloudDto>()
                .ForMember(dest => dest.Weight,
                    opt => opt.MapFrom(src => src.UsageCount))
                .ForMember(dest => dest.SizeClass,
                    opt => opt.Ignore()) // Calculated by service based on min/max weights
                .ForMember(dest => dest.NormalizedWeight,
                    opt => opt.Ignore()); // Calculated by service

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateTagDto → Tag

            CreateMap<CreateTagDto, Tag>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Color,
                    opt => opt.MapFrom(src => string.IsNullOrEmpty(src.Color) ? "#3498db" : src.Color))
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.LastUsedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UsageCount,
                    opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.ArticleTags,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Articles,
                    opt => opt.Ignore());

            #endregion

            #region UpdateTagDto → Tag (partial updates)

            CreateMap<UpdateTagDto, Tag>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastUsedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.UsageCount,
                    opt => opt.Ignore())
                .ForMember(dest => dest.ArticleTags,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Articles,
                    opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Calculates a darker shade of the given hex color.
        /// </summary>
        private static string CalculateDarkColor(string hexColor)
        {
            const string fallbackDark = "#2c3e50";

            if (string.IsNullOrWhiteSpace(hexColor) || hexColor.Length < 7 || !hexColor.StartsWith("#"))
                return fallbackDark;

            try
            {
                var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
                var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
                var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

                r = (int)(r * 0.7);
                g = (int)(g * 0.7);
                b = (int)(b * 0.7);

                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return fallbackDark;
            }
        }

        /// <summary>
        /// Generates a human-readable "time ago" string.
        /// </summary>
        private static string GetTimeAgo(DateTime? date)
        {
            if (!date.HasValue)
                return "Never used";

            var span = DateTime.UtcNow - date.Value;

            return span.TotalDays switch
            {
                < 1 => "Today",
                < 2 => "Yesterday",
                < 7 => $"{(int)span.TotalDays} days ago",
                < 30 => $"{(int)(span.TotalDays / 7)} weeks ago",
                < 365 => $"{(int)(span.TotalDays / 30)} months ago",
                _ => $"{(int)(span.TotalDays / 365)} years ago"
            };
        }

        #endregion
    }
}