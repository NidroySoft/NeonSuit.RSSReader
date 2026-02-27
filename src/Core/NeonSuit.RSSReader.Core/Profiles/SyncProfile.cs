using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Sync;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Profiles
{
    /// <summary>
    /// AutoMapper profile for synchronization-related entities and DTOs.
    /// </summary>
    public class SyncProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SyncProfile"/> class.
        /// </summary>
        public SyncProfile()
        {
            // =========================================================================
            // SyncStatistics -> SyncStatisticsDto
            // =========================================================================
            CreateMap<SyncStatistics, SyncStatisticsDto>()
                .ForMember(dest => dest.TotalSyncTimeFormatted, opt => opt.Ignore())
                .ForMember(dest => dest.LastUpdateFormatted, opt => opt.MapFrom(src =>
                    FormatDateTime(src.LastUpdated)))
                .ForMember(dest => dest.SuccessRate, opt => opt.Ignore());

            // =========================================================================
            // SyncError -> SyncErrorInfoDto
            // =========================================================================
            CreateMap<SyncError, SyncErrorInfoDto>()
                .ForMember(dest => dest.ErrorTimeFormatted, opt => opt.MapFrom(src =>
                    FormatDateTime(src.ErrorTime)))
                .ForMember(dest => dest.TaskType, opt => opt.MapFrom(src => src.TaskType));

            // =========================================================================
            // SyncTaskExecution -> SyncTaskExecutionInfoDto
            // =========================================================================
            CreateMap<SyncTaskExecution, SyncTaskExecutionInfoDto>()
                .ForMember(dest => dest.TaskType, opt => opt.MapFrom(src => src.TaskType))
                .ForMember(dest => dest.LastRunStart, opt => opt.MapFrom(src => src.StartTime))
                .ForMember(dest => dest.LastRunEnd, opt => opt.MapFrom(src => src.EndTime))
                .ForMember(dest => dest.LastRunDurationSeconds, opt => opt.MapFrom(src => src.DurationSeconds))
                .ForMember(dest => dest.LastRunDurationFormatted, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunSuccessful, opt => opt.MapFrom(src => src.Success))
                .ForMember(dest => dest.LastRunError, opt => opt.MapFrom(src => src.ErrorMessage))
                .ForMember(dest => dest.TotalRuns, opt => opt.Ignore()) // Se calcula en el servicio
                .ForMember(dest => dest.SuccessfulRuns, opt => opt.Ignore())
                .ForMember(dest => dest.AverageRunDurationSeconds, opt => opt.Ignore())
                .ForMember(dest => dest.AverageRunDurationFormatted, opt => opt.Ignore())
                .ForMember(dest => dest.NextScheduledRun, opt => opt.Ignore())
                .ForMember(dest => dest.NextScheduledFormatted, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunResults, opt => opt.Ignore());

            // =========================================================================
            // SyncTaskConfig -> SyncTaskStatusDto (parcial)
            // =========================================================================
            CreateMap<SyncTaskConfig, SyncTaskStatusDto>()
                .ForMember(dest => dest.TaskType, opt => opt.MapFrom(src => src.TaskType))
                .ForMember(dest => dest.IsEnabled, opt => opt.MapFrom(src => src.Enabled))
                .ForMember(dest => dest.IntervalMinutes, opt => opt.MapFrom(src => src.IntervalMinutes))
                .ForMember(dest => dest.NextScheduled, opt => opt.MapFrom(src => src.NextScheduled))
                .ForMember(dest => dest.NextScheduledFormatted, opt => opt.MapFrom(src =>
                    FormatDateTime(src.NextScheduled)))
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunStart, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunEnd, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunDurationSeconds, opt => opt.Ignore())
                .ForMember(dest => dest.LastRunSuccessful, opt => opt.Ignore())
                .ForMember(dest => dest.TotalRuns, opt => opt.Ignore())
                .ForMember(dest => dest.SuccessfulRuns, opt => opt.Ignore())
                .ForMember(dest => dest.SuccessRate, opt => opt.Ignore());

            // =========================================================================
            // SyncState -> SyncStatusDto
            // =========================================================================
            CreateMap<SyncState, SyncStatusDto>()
                .ForMember(dest => dest.CurrentStatus, opt => opt.MapFrom(src => src.CurrentStatus))
                .ForMember(dest => dest.IsSynchronizing, opt => opt.Ignore())
                .ForMember(dest => dest.LastSyncCompleted, opt => opt.MapFrom(src => src.LastSyncCompleted))
                .ForMember(dest => dest.NextSyncScheduled, opt => opt.Ignore()) // Se calcula en el servicio
                .ForMember(dest => dest.LastSyncFormatted, opt => opt.MapFrom(src =>
                    FormatDateTime(src.LastSyncCompleted)))
                .ForMember(dest => dest.NextSyncFormatted, opt => opt.Ignore());

            // =========================================================================
            // ConfigureTaskDto -> SyncTaskConfig
            // =========================================================================
            CreateMap<ConfigureTaskDto, SyncTaskConfig>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.Ignore())
                .ForMember(dest => dest.Priority, opt => opt.Ignore())
                .ForMember(dest => dest.MaxRetries, opt => opt.Ignore())
                .ForMember(dest => dest.RetryDelayMinutes, opt => opt.Ignore())
                .ForMember(dest => dest.LastScheduled, opt => opt.Ignore())
                .ForMember(dest => dest.NextScheduled, opt => opt.Ignore())
                .ForMember(dest => dest.LastModified, opt => opt.Ignore());
        }

        #region Helper Methods

        /// <summary>
        /// Formats a DateTime for display.
        /// </summary>
        /// <param name="dateTime">DateTime to format.</param>
        /// <returns>Formatted string.</returns>
        private static string FormatDateTime(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return "Never";

            var now = DateTime.UtcNow;
            var diff = now - dateTime.Value;

            if (diff.TotalMinutes < 1)
                return "Just now";
            if (diff.TotalHours < 1)
                return $"{(int)diff.TotalMinutes} minutes ago";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours} hours ago";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} days ago";

            return dateTime.Value.ToString("yyyy-MM-dd HH:mm");
        }

        #endregion
    }
}