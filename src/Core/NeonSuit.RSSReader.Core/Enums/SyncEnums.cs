namespace NeonSuit.RSSReader.Core.Enums
{
    /// <summary>
    /// Represents the overall synchronization status.
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>Service is stopped.</summary>
        Stopped,
        /// <summary>Service is starting up.</summary>
        Starting,
        /// <summary>Service is running normally.</summary>
        Running,
        /// <summary>Service is paused (no new tasks start).</summary>
        Paused,
        /// <summary>Service is shutting down.</summary>
        Stopping,
        /// <summary>Service is in error state.</summary>
        Error
    }

    /// <summary>
    /// Types of synchronization tasks managed by the coordinator.
    /// </summary>
    public enum SyncTaskType
    {
        /// <summary>Feed content updates and article fetching.</summary>
        FeedUpdate,
        /// <summary>Auto-tagging rule processing.</summary>
        TagProcessing,
        /// <summary>Article retention cleanup.</summary>
        ArticleCleanup,
        /// <summary>Database and settings backup creation.</summary>
        BackupCreation,
        /// <summary>Statistics recalculation.</summary>
        StatisticsUpdate,
        /// <summary>Rule engine processing.</summary>
        RuleProcessing,
        /// <summary>Cache maintenance (image cache, etc.).</summary>
        CacheMaintenance,
        /// <summary>Full synchronization cycle.</summary>
        FullSync
    }

    /// <summary>
    /// Status of an individual synchronization task.
    /// </summary>
    public enum SyncTaskStatus
    {
        /// <summary>Task is idle, waiting for schedule.</summary>
        Idle,
        /// <summary>Task is scheduled to run soon.</summary>
        Scheduled,
        /// <summary>Task is currently running.</summary>
        Running,
        /// <summary>Task completed successfully.</summary>
        Completed,
        /// <summary>Task failed with error.</summary>
        Failed,
        /// <summary>Task was cancelled.</summary>
        Cancelled,
        /// <summary>Task is disabled in configuration.</summary>
        Disabled
    }

    /// <summary>
    /// Priority levels for task scheduling.
    /// </summary>
    public enum SyncPriority
    {
        /// <summary>Low priority - runs when system is idle.</summary>
        Low,
        /// <summary>Medium priority - normal background tasks.</summary>
        Medium,
        /// <summary>High priority - user-triggered or critical tasks.</summary>
        High,
        /// <summary>Critical priority - must run immediately.</summary>
        Critical
    }
}