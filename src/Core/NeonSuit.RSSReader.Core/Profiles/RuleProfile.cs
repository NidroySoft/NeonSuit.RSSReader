// =======================================================
// Core/Profiles/RuleProfile.cs (CORREGIDO)
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Models;
using System.Text.Json;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for Rule entity mappings.
    /// Configures transformations between Rule and its related DTOs.
    /// </summary>
    public class RuleProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleProfile"/> class.
        /// </summary>
        public RuleProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region Rule → RuleDto (complete)

            CreateMap<Rule, RuleDto>()
                .ForMember(dest => dest.FeedIds,
                    opt => opt.MapFrom(src => ParseIntList(src.FeedIds)))
                .ForMember(dest => dest.CategoryIds,
                    opt => opt.MapFrom(src => ParseIntList(src.CategoryIds)))
                .ForMember(dest => dest.TagIds,
                    opt => opt.MapFrom(src => ParseIntList(src.TagIds)))
                .ForMember(dest => dest.HumanReadableCondition,
                    opt => opt.MapFrom(src => src.HumanReadableCondition))
                .ForMember(dest => dest.HealthStatus,
                    opt => opt.MapFrom(src => src.HealthStatus));

            #endregion

            #region Rule → RuleSummaryDto (lightweight)

            CreateMap<Rule, RuleSummaryDto>()
                .ForMember(dest => dest.Summary,
                    opt => opt.MapFrom(src => src.Summary))
                .ForMember(dest => dest.HealthStatus,
                    opt => opt.MapFrom(src => src.HealthStatus))
                .ForMember(dest => dest.LastMatchTimeAgo,
                    opt => opt.MapFrom(src => GetTimeAgo(src.LastMatchDate)));

            #endregion

            #region Rule → RuleHealthDto (monitoring)

            CreateMap<Rule, RuleHealthDto>()
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.HealthStatus))
                .ForMember(dest => dest.TimeSinceLastMatch,
                    opt => opt.MapFrom(src => GetTimeAgo(src.LastMatchDate)))
                .ForMember(dest => dest.MatchesLast24h,
                    opt => opt.Ignore()) // Calculated by service
                .ForMember(dest => dest.MatchesLast7d,
                    opt => opt.Ignore())
                .ForMember(dest => dest.MatchesLast30d,
                    opt => opt.Ignore())
                .ForMember(dest => dest.AverageMatchesPerDay,
                    opt => opt.Ignore());

            #endregion

            #region RuleCondition → RuleConditionDto

            CreateMap<RuleCondition, RuleConditionDto>()
                .ForMember(dest => dest.HumanReadable,
                    opt => opt.MapFrom(src => src.HumanReadable));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateRuleDto → Rule

            CreateMap<CreateRuleDto, Rule>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.FeedIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.FeedIds)))
                .ForMember(dest => dest.CategoryIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.CategoryIds)))
                .ForMember(dest => dest.TagIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.TagIds)))
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.LastModified,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.MatchCount,
                    opt => opt.MapFrom(_ => 0))
                .ForMember(dest => dest.LastMatchDate,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Conditions,
                    opt => opt.Ignore())
                .ForMember(dest => dest.NotificationLogs,
                    opt => opt.Ignore());

            #endregion

            #region UpdateRuleDto → Rule (partial updates)

            CreateMap<UpdateRuleDto, Rule>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt,
                    opt => opt.Ignore())
                .ForMember(dest => dest.LastModified,
                    opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.FeedIds,
                    opt => opt.Condition(src => src.FeedIds != null))
                .ForMember(dest => dest.FeedIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.FeedIds)))
                .ForMember(dest => dest.CategoryIds,
                    opt => opt.Condition(src => src.CategoryIds != null))
                .ForMember(dest => dest.CategoryIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.CategoryIds)))
                .ForMember(dest => dest.TagIds,
                    opt => opt.Condition(src => src.TagIds != null))
                .ForMember(dest => dest.TagIds,
                    opt => opt.MapFrom(src => SerializeIntList(src.TagIds)))
                .ForMember(dest => dest.MatchCount,
                    opt => opt.Condition(src => src.ResetMatchCount == true))
                .ForMember(dest => dest.MatchCount,
                    opt => opt.MapFrom(_ => 0))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Parses a JSON string into a list of integers.
        /// </summary>
        private static List<int> ParseIntList(string json)
        {
            if (string.IsNullOrEmpty(json))
                return new List<int>();

            try
            {
                return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Serializes a list of integers to a JSON string.
        /// Handles null input by returning an empty array "[]".
        /// </summary>
        private static string SerializeIntList(List<int>? list)
        {
            if (list == null || list.Count == 0)
                return "[]";

            try
            {
                return JsonSerializer.Serialize(list);
            }
            catch
            {
                return "[]";
            }
        }

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
                >= 30 => $"{(int)(diff.TotalDays / 30)} months ago",
                >= 7 => $"{(int)(diff.TotalDays / 7)} weeks ago",
                >= 1 => $"{(int)diff.TotalDays} days ago",
                >= 1.0 / 24 => $"{(int)diff.TotalHours} hours ago",
                _ => "Just now"
            };
        }

        #endregion
    }
}