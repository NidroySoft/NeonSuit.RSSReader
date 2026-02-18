using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for a synchronization coordinator service.
    /// Manages and coordinates various background synchronization tasks 
    /// (feed updates, tag processing, cleanup, backups) with proper prioritization,
    /// error handling, and resource management.
    /// </summary>
    public interface ISyncCoordinatorService
    {
        // Synchronization Status
        /// <summary>
        /// Gets the current synchronization status.
        /// </summary>
        SyncStatus CurrentStatus { get; }

        /// <summary>
        /// Gets whether any synchronization task is currently running.
        /// </summary>
        bool IsSynchronizing { get; }

        /// <summary>
        /// Gets the time when the last synchronization cycle completed.
        /// </summary>
        DateTime? LastSyncCompleted { get; }

        /// <summary>
        /// Gets the time when the next synchronization is scheduled.
        /// </summary>
        DateTime? NextSyncScheduled { get; }

        /// <summary>
        /// Gets statistics about synchronization performance.
        /// </summary>
        SyncStatistics Statistics { get; }

        // Control Methods
        /// <summary>
        /// Starts the synchronization coordinator service.
        /// Initializes all managed sync tasks and begins the synchronization loop.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the service.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the synchronization coordinator service gracefully.
        /// Completes current tasks and cleans up resources.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Pauses all synchronization activities.
        /// Allows temporary suspension without stopping the service.
        /// </summary>
        Task PauseAsync();

        /// <summary>
        /// Resumes synchronization activities after a pause.
        /// </summary>
        Task ResumeAsync();

        // Manual Trigger Methods
        /// <summary>
        /// Manually triggers a feed update synchronization.
        /// Updates all feeds according to their configured frequencies.
        /// </summary>
        Task TriggerFeedSyncAsync();

        /// <summary>
        /// Manually triggers a specific feed update.
        /// </summary>
        /// <param name="feedId">The ID of the feed to update.</param>
        Task TriggerSingleFeedSyncAsync(int feedId);

        /// <summary>
        /// Manually triggers a cleanup synchronization.
        /// Performs article retention and database maintenance.
        /// </summary>
        Task TriggerCleanupSyncAsync();

        /// <summary>
        /// Manually triggers a tag processing synchronization.
        /// Applies auto-tagging rules and updates tag statistics.
        /// </summary>
        Task TriggerTagProcessingSyncAsync();

        /// <summary>
        /// Manually triggers a backup synchronization.
        /// Creates backup copies of the database and settings.
        /// </summary>
        Task TriggerBackupSyncAsync();

        /// <summary>
        /// Manually triggers a full synchronization cycle.
        /// Executes all synchronization tasks in proper order.
        /// </summary>
        Task TriggerFullSyncAsync();

        // Configuration Methods
        /// <summary>
        /// Enables or disables a specific synchronization task type.
        /// </summary>
        /// <param name="taskType">The type of synchronization task to configure.</param>
        /// <param name="enabled">Whether the task should be enabled.</param>
        Task ConfigureTaskAsync(SyncTaskType taskType, bool enabled);

        /// <summary>
        /// Sets the interval for a specific synchronization task.
        /// </summary>
        /// <param name="taskType">The type of synchronization task to configure.</param>
        /// <param name="intervalMinutes">The interval in minutes.</param>
        Task ConfigureTaskIntervalAsync(SyncTaskType taskType, int intervalMinutes);

        /// <summary>
        /// Sets the maximum duration for synchronization tasks.
        /// Tasks exceeding this duration will be cancelled.
        /// </summary>
        /// <param name="maxDurationMinutes">Maximum duration in minutes.</param>
        Task SetMaxSyncDurationAsync(int maxDurationMinutes);

        /// <summary>
        /// Sets the maximum number of concurrent synchronization tasks.
        /// </summary>
        /// <param name="maxConcurrentTasks">Maximum concurrent tasks allowed.</param>
        Task SetMaxConcurrentTasksAsync(int maxConcurrentTasks);

        // Monitoring Methods
        /// <summary>
        /// Gets the status of all managed synchronization tasks.
        /// </summary>
        Task<Dictionary<SyncTaskType, SyncTaskStatus>> GetTaskStatusesAsync();

        /// <summary>
        /// Gets detailed information about the last execution of a specific task.
        /// </summary>
        Task<SyncTaskExecutionInfo> GetTaskExecutionInfoAsync(SyncTaskType taskType);

        /// <summary>
        /// Gets recent synchronization errors.
        /// </summary>
        /// <param name="maxErrors">Maximum number of errors to retrieve.</param>
        Task<List<SyncErrorInfo>> GetRecentErrorsAsync(int maxErrors = 50);

        /// <summary>
        /// Clears the error history.
        /// </summary>
        Task ClearErrorHistoryAsync();

        // Events
        /// <summary>
        /// Raised when synchronization status changes.
        /// </summary>
        event EventHandler<SyncStatusChangedEventArgs> OnStatusChanged;

        /// <summary>
        /// Raised when a synchronization task starts.
        /// </summary>
        event EventHandler<SyncTaskStartedEventArgs> OnTaskStarted;

        /// <summary>
        /// Raised when a synchronization task completes.
        /// </summary>
        event EventHandler<SyncTaskCompletedEventArgs> OnTaskCompleted;

        /// <summary>
        /// Raised when a synchronization error occurs.
        /// </summary>
        event EventHandler<SyncErrorEventArgs> OnSyncError;

        /// <summary>
        /// Raised when synchronization progress is made.
        /// </summary>
        event EventHandler<SyncProgressEventArgs> OnSyncProgress;
    }

    /// <summary>
    /// Represents the overall synchronization status.
    /// </summary>
    public enum SyncStatus
    {
        Stopped,
        Starting,
        Running,
        Paused,
        Stopping,
        Error
    }

    /// <summary>
    /// Types of synchronization tasks managed by the coordinator.
    /// </summary>
    public enum SyncTaskType
    {
        FeedUpdate,
        TagProcessing,
        ArticleCleanup,
        BackupCreation,
        StatisticsUpdate,
        RuleProcessing,
        CacheMaintenance,
        FullSync
    }

    /// <summary>
    /// Status of an individual synchronization task.
    /// </summary>
    public enum SyncTaskStatus
    {
        Idle,
        Scheduled,
        Running,
        Completed,
        Failed,
        Cancelled,
        Disabled
    }

    /// <summary>
    /// Statistics about synchronization performance.
    /// </summary>
    public class SyncStatistics
    {
        public int TotalSyncCycles { get; set; }
        public int SuccessfulSyncs { get; set; }
        public int FailedSyncs { get; set; }
        public double AverageSyncDurationSeconds { get; set; }
        public TimeSpan TotalSyncTime { get; set; }
        public int ArticlesProcessed { get; set; }
        public int FeedsUpdated { get; set; }
        public int TagsApplied { get; set; }
        public DateTime LastStatisticsUpdate { get; set; }
    }

    /// <summary>
    /// Information about a synchronization task execution.
    /// </summary>
    public class SyncTaskExecutionInfo
    {
        public SyncTaskType TaskType { get; set; }
        public DateTime? LastRunStart { get; set; }
        public DateTime? LastRunEnd { get; set; }
        public TimeSpan? LastRunDuration { get; set; }
        public bool LastRunSuccessful { get; set; }
        public string? LastRunError { get; set; }
        public int TotalRuns { get; set; }
        public int SuccessfulRuns { get; set; }
        public TimeSpan AverageRunDuration { get; set; }
        public DateTime? NextScheduledRun { get; set; }
    }

    /// <summary>
    /// Information about a synchronization error.
    /// </summary>
    public class SyncErrorInfo
    {
        public DateTime ErrorTime { get; set; }
        public SyncTaskType TaskType { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public bool IsRecoverable { get; set; }
        public int RetryCount { get; set; }
    }

    // Event Arguments
    public class SyncStatusChangedEventArgs : EventArgs
    {
        public SyncStatus PreviousStatus { get; }
        public SyncStatus NewStatus { get; }

        public SyncStatusChangedEventArgs(SyncStatus previousStatus, SyncStatus newStatus)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
    }

    public class SyncTaskStartedEventArgs : EventArgs
    {
        public SyncTaskType TaskType { get; }
        public DateTime StartTime { get; }

        public SyncTaskStartedEventArgs(SyncTaskType taskType, DateTime startTime)
        {
            TaskType = taskType;
            StartTime = startTime;
        }
    }

    public class SyncTaskCompletedEventArgs : EventArgs
    {
        public SyncTaskType TaskType { get; }
        public DateTime StartTime { get; }
        public DateTime EndTime { get; }
        public TimeSpan Duration { get; }
        public bool Success { get; }
        public string? ErrorMessage { get; }
        public Dictionary<string, object> Results { get; }

        public SyncTaskCompletedEventArgs(SyncTaskType taskType, DateTime startTime, DateTime endTime, bool success,
                                         string? errorMessage = null, Dictionary<string, object>? results = null)
        {
            TaskType = taskType;
            StartTime = startTime;
            EndTime = endTime;
            Duration = endTime - startTime;
            Success = success;
            ErrorMessage = errorMessage;
            Results = results ?? new Dictionary<string, object>();
        }
    }

    public class SyncErrorEventArgs : EventArgs
    {
        public SyncTaskType TaskType { get; }
        public Exception Exception { get; }
        public bool IsFatal { get; }

        public SyncErrorEventArgs(SyncTaskType taskType, Exception exception, bool isFatal = false)
        {
            TaskType = taskType;
            Exception = exception;
            IsFatal = isFatal;
        }
    }

    public class SyncProgressEventArgs : EventArgs
    {
        public SyncTaskType TaskType { get; }
        public string Operation { get; }
        public int Current { get; }
        public int Total { get; }
        public double Percentage => Total > 0 ? (Current * 100.0) / Total : 0;

        public SyncProgressEventArgs(SyncTaskType taskType, string operation, int current, int total)
        {
            TaskType = taskType;
            Operation = operation;
            Current = current;
            Total = total;
        }
    }
}