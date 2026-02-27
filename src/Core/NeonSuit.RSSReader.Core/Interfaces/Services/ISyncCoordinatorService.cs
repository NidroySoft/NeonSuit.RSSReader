using NeonSuit.RSSReader.Core.DTOs.Sync;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for synchronization coordinator that manages and coordinates background tasks.
    /// Handles feed updates, tag processing, cleanup operations, and backups with proper prioritization,
    /// error handling, and resource management for low-resource environments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all background synchronization activities including:
    /// <list type="bullet">
    /// <item>Feed update scheduling based on individual feed frequencies</item>
    /// <item>Article retention cleanup and database maintenance</item>
    /// <item>Auto-tagging rule processing and tag statistics updates</item>
    /// <item>Automated backup creation with rotation policies</item>
    /// <item>Performance monitoring and error tracking</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of internal models to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface ISyncCoordinatorService
    {
        #region Synchronization Status

        /// <summary>
        /// Gets the current overall synchronization status.
        /// </summary>
        SyncStatusDto CurrentStatus { get; }

        /// <summary>
        /// Gets aggregated statistics about synchronization performance.
        /// </summary>
        SyncStatisticsDto Statistics { get; }

        #endregion

        #region Control Methods

        /// <summary>
        /// Starts the synchronization coordinator service.
        /// Initializes all managed sync tasks and begins the synchronization loop.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is already running.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the synchronization coordinator service gracefully.
        /// Completes current tasks and cleans up resources.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Pauses all synchronization activities.
        /// Allows temporary suspension without stopping the service.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task PauseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes synchronization activities after a pause.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ResumeAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Manual Trigger Methods

        /// <summary>
        /// Manually triggers a feed update synchronization.
        /// Updates all feeds according to their configured frequencies.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerFeedSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers a specific feed update regardless of its schedule.
        /// </summary>
        /// <param name="feedId">The ID of the feed to update.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerSingleFeedSyncAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers a cleanup synchronization.
        /// Performs article retention and database maintenance.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerCleanupSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers a tag processing synchronization.
        /// Applies auto-tagging rules and updates tag statistics.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerTagProcessingSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers a backup synchronization.
        /// Creates backup copies of the database and settings.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerBackupSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually triggers a full synchronization cycle.
        /// Executes all synchronization tasks in proper order.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the service is not running or is paused.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> TriggerFullSyncAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Configuration Methods

        /// <summary>
        /// Enables or disables a specific synchronization task type.
        /// </summary>
        /// <param name="configureDto">The DTO containing task configuration.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configureDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if task type is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> ConfigureTaskAsync(ConfigureTaskDto configureDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the maximum duration for synchronization tasks.
        /// </summary>
        /// <param name="configureDto">The DTO containing max duration configuration.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configureDto"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> SetMaxSyncDurationAsync(ConfigureMaxDurationDto configureDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the maximum number of concurrent synchronization tasks.
        /// </summary>
        /// <param name="configureDto">The DTO containing concurrency configuration.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configureDto"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> SetMaxConcurrentTasksAsync(ConfigureConcurrencyDto configureDto, CancellationToken cancellationToken = default);

        #endregion

        #region Monitoring Methods

        /// <summary>
        /// Gets the current status of all managed synchronization tasks.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of task status DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<SyncTaskStatusDto>> GetTaskStatusesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information about the last execution of a specific task.
        /// </summary>
        /// <param name="taskType">The type of synchronization task.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>Detailed execution information DTO for the specified task.</returns>
        /// <exception cref="ArgumentException">Thrown if task type is invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncTaskExecutionInfoDto> GetTaskExecutionInfoAsync(string taskType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recent synchronization errors.
        /// </summary>
        /// <param name="maxErrors">Maximum number of errors to retrieve. Default: 50.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of recent synchronization error DTOs.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="maxErrors"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<SyncErrorInfoDto>> GetRecentErrorsAsync(int maxErrors = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the error history.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A DTO containing the result of the action.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<SyncActionResultDto> ClearErrorHistoryAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Events

        /// <summary>
        /// Raised when the overall synchronization status changes.
        /// </summary>
        event EventHandler<SyncStatusDto> OnStatusChanged;

        /// <summary>
        /// Raised when a synchronization task starts.
        /// </summary>
        event EventHandler<SyncProgressDto> OnTaskStarted;

        /// <summary>
        /// Raised when a synchronization task completes.
        /// </summary>
        event EventHandler<SyncTaskExecutionInfoDto> OnTaskCompleted;

        /// <summary>
        /// Raised when a synchronization error occurs.
        /// </summary>
        event EventHandler<SyncErrorInfoDto> OnSyncError;

        /// <summary>
        /// Raised when synchronization progress is made during long-running tasks.
        /// </summary>
        event EventHandler<SyncProgressDto> OnSyncProgress;

        #endregion
    }
}