// =======================================================
// Core/Profiles/RuleConditionProfile.cs
// =======================================================

using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Rules;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using System;
using System.Linq;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for RuleCondition entity mappings.
    /// Configures transformations between RuleCondition and its related DTOs.
    /// </summary>
    public class RuleConditionProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleConditionProfile"/> class.
        /// </summary>
        public RuleConditionProfile()
        {
            // =========================================================================
            // ENTITY → DTO MAPPINGS (READ OPERATIONS)
            // =========================================================================

            #region RuleCondition → RuleConditionDto

            CreateMap<RuleCondition, RuleConditionDto>()
                .ForMember(dest => dest.HumanReadable,
                    opt => opt.MapFrom(src => src.HumanReadable))
                .ForMember(dest => dest.IsValid,
                    opt => opt.MapFrom(src => src.IsValid))
                .ForMember(dest => dest.FieldDisplayName,
                    opt => opt.MapFrom(src => src.FieldDisplayName))
                .ForMember(dest => dest.OperatorDisplayName,
                    opt => opt.MapFrom(src => src.OperatorDisplayName));

            #endregion

            #region RuleCondition Grouping (for advanced UI)

            CreateMap<IGrouping<int, RuleCondition>, RuleConditionGroupDto>()
                .ForMember(dest => dest.GroupId,
                    opt => opt.MapFrom(src => src.Key))
                .ForMember(dest => dest.Conditions,
                    opt => opt.MapFrom(src => src.OrderBy(c => c.Order).ToList()))
                .ForMember(dest => dest.HumanReadable,
                    opt => opt.MapFrom(src => BuildGroupHumanReadable(src)));

            #endregion

            // =========================================================================
            // DTO → ENTITY MAPPINGS (WRITE OPERATIONS)
            // =========================================================================

            #region CreateRuleConditionDto → RuleCondition

            CreateMap<CreateRuleConditionDto, RuleCondition>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.Rule,
                    opt => opt.Ignore());

            #endregion

            #region UpdateRuleConditionDto → RuleCondition (partial updates)

            CreateMap<UpdateRuleConditionDto, RuleCondition>()
                .ForMember(dest => dest.Id,
                    opt => opt.Ignore())
                .ForMember(dest => dest.RuleId,
                    opt => opt.Ignore()) // RuleId cannot be changed (would orphan)
                .ForMember(dest => dest.Rule,
                    opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            #endregion
        }

        #region Private Helper Methods

        /// <summary>
        /// Builds a human-readable representation of a condition group.
        /// </summary>
        private static string BuildGroupHumanReadable(IGrouping<int, RuleCondition> group)
        {
            var conditions = group.OrderBy(c => c.Order).ToList();
            if (conditions.Count == 0)
                return string.Empty;

            if (conditions.Count == 1)
                return conditions[0].HumanReadable;

            var operatorText = DetermineGroupOperator(conditions) == LogicalOperator.AND ? "Y" : "O";
            return $"({string.Join($" {operatorText} ", conditions.Select(c => c.HumanReadable))})";
        }

        /// <summary>
        /// Determines the logical operator for a group based on its conditions.
        /// </summary>
        private static LogicalOperator DetermineGroupOperator(List<RuleCondition> conditions)
        {
            if (conditions.Count <= 1)
                return LogicalOperator.AND;

            // Use the CombineWithNext of the first condition as the group operator
            return conditions[0].CombineWithNext;
        }

        #endregion
    }
}