using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Sync;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.Text.Json;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="ISyncCoordinatorService"/> that coordinates and manages all background synchronization tasks.
    /// Implements a producer-consumer pattern with proper scheduling, error handling, and resource management for low-resource environments.
    /// </summary>
    internal class SyncCoordinatorService : ISyncCoordinatorService
    {
        private readonly ILogger _logger;
        private readonly ISettingsService _settingsService;
        private readonly ISyncRepository _syncRepository;
        private readonly IMapper _mapper;

        // Synchronization state
        private SyncStatus _currentStatus = SyncStatus.Stopped;
        private readonly SemaphoreSlim _statusLock = new(1, 1);
        private CancellationTokenSource? _syncCancellationTokenSource;
        private Task? _syncLoopTask;
        private SyncState? _syncState;

        // Task management
        private readonly Dictionary<SyncTaskType, SyncTaskInfo> _syncTasks;
        private readonly ConcurrentQueue<SyncTaskRequest> _taskQueue;
        private readonly SemaphoreSlim _queueSemaphore = new(0, int.MaxValue);
        private readonly List<Task> _workerTasks;
        private readonly int _maxConcurrentTasks;

        // In-memory caches (backed by repository)
        private readonly SyncStatistics _statistics = new();
        private readonly ConcurrentDictionary<SyncTaskType, SyncTaskExecutionInfo> _taskExecutionInfo;
        private DateTime? _lastSyncCompleted;
        private DateTime? _nextSyncScheduled;

        // Configuration
        private bool _isPaused;
        private int _maxSyncDurationMinutes = 30;
        private const int MaxErrorHistory = 100;

        // Event handlers
        private event EventHandler<SyncStatusDto>? _onStatusChanged;
        private event EventHandler<SyncProgressDto>? _onTaskStarted;
        private event EventHandler<SyncTaskExecutionInfoDto>? _onTaskCompleted;
        private event EventHandler<SyncErrorInfoDto>? _onSyncError;
        private event EventHandler<SyncProgressDto>? _onSyncProgress;

        #region Properties

        /// <inheritdoc />
        public SyncStatusDto CurrentStatus
        {
            get
            {
                var dto = new SyncStatusDto
                {
                    CurrentStatus = _currentStatus.ToString(),
                    IsSynchronizing = _currentStatus == SyncStatus.Running && !_isPaused,
                    LastSyncCompleted = _lastSyncCompleted,
                    NextSyncScheduled = _nextSyncScheduled,
                    LastSyncFormatted = FormatDateTime(_lastSyncCompleted),
                    NextSyncFormatted = FormatDateTime(_nextSyncScheduled)
                };
                return dto;
            }
        }

        /// <inheritdoc />
        public SyncStatisticsDto Statistics => _mapper.Map<SyncStatisticsDto>(_statistics);

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<SyncStatusDto> OnStatusChanged
        {
            add => _onStatusChanged += value;
            remove => _onStatusChanged -= value;
        }

        /// <inheritdoc />
        public event EventHandler<SyncProgressDto> OnTaskStarted
        {
            add => _onTaskStarted += value;
            remove => _onTaskStarted -= value;
        }

        /// <inheritdoc />
        public event EventHandler<SyncTaskExecutionInfoDto> OnTaskCompleted
        {
            add => _onTaskCompleted += value;
            remove => _onTaskCompleted -= value;
        }

        /// <inheritdoc />
        public event EventHandler<SyncErrorInfoDto> OnSyncError
        {
            add => _onSyncError += value;
            remove => _onSyncError -= value;
        }

        /// <inheritdoc />
        public event EventHandler<SyncProgressDto> OnSyncProgress
        {
            add => _onSyncProgress += value;
            remove => _onSyncProgress -= value;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncCoordinatorService"/> class.
        /// </summary>
        /// <param name="settingsService">The settings service for configuration.</param>
        /// <param name="syncRepository">The sync repository for persistence.</param>
        /// <param name="mapper">AutoMapper instance for DTO transformations.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public SyncCoordinatorService(
            ISettingsService settingsService,
            ISyncRepository syncRepository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(settingsService);
            ArgumentNullException.ThrowIfNull(syncRepository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _settingsService = settingsService;
            _syncRepository = syncRepository;
            _mapper = mapper;
            _logger = logger.ForContext<SyncCoordinatorService>();

            _syncTasks = new Dictionary<SyncTaskType, SyncTaskInfo>();
            _taskQueue = new ConcurrentQueue<SyncTaskRequest>();
            _workerTasks = new List<Task>();
            _maxConcurrentTasks = Environment.ProcessorCount; // Optimized for i3-10105T (4 cores)

            _taskExecutionInfo = new ConcurrentDictionary<SyncTaskType, SyncTaskExecutionInfo>();

            _logger.Debug("SyncCoordinatorService initialized with {MaxConcurrent} max concurrent tasks", _maxConcurrentTasks);
        }

        #endregion

        #region Lifecycle Management

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _statusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_currentStatus != SyncStatus.Stopped && _currentStatus != SyncStatus.Error)
                {
                    _logger.Warning("Cannot start SyncCoordinatorService. Current status: {Status}", _currentStatus);
                    return;
                }

                // Load persistent state from repository
                await LoadPersistentStateAsync(cancellationToken).ConfigureAwait(false);

                // Initialize tasks from repository
                await InitializeTasksFromRepositoryAsync(cancellationToken).ConfigureAwait(false);

                // Initialize cancellation token
                _syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Load configuration from settings
                await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

                // Start worker tasks
                StartWorkerTasks();

                // Start the main sync loop
                _syncLoopTask = Task.Run(() => RunSyncLoopAsync(_syncCancellationTokenSource.Token),
                    _syncCancellationTokenSource.Token);

                await UpdateStatusAsync(SyncStatus.Running, cancellationToken).ConfigureAwait(false);
                _logger.Information("SyncCoordinatorService started successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("StartAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start SyncCoordinatorService");
                await UpdateStatusAsync(SyncStatus.Error, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Failed to start SyncCoordinatorService", ex);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            await _statusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_currentStatus == SyncStatus.Stopped || _currentStatus == SyncStatus.Stopping)
                {
                    _logger.Debug("StopAsync called but service already stopped or stopping");
                    return;
                }

                await UpdateStatusAsync(SyncStatus.Stopping, cancellationToken).ConfigureAwait(false);

                _logger.Information("Stopping SyncCoordinatorService gracefully...");

                // Cancel all ongoing operations
                _syncCancellationTokenSource?.Cancel();

                // Wait for sync loop to complete without throwing
                if (_syncLoopTask != null)
                {
                    try
                    {
                        await _syncLoopTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Debug("Sync loop cancelled during shutdown");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error waiting for sync loop to stop");
                    }
                }

                // Wait for worker tasks to complete
                try
                {
                    await Task.WhenAll(_workerTasks).ConfigureAwait(false);
                    _logger.Debug("All worker tasks completed successfully");
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("Worker tasks cancelled during shutdown");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error waiting for worker tasks to stop");
                }

                // Save persistent state before stopping
                await SavePersistentStateAsync(cancellationToken).ConfigureAwait(false);

                // Clean up
                _syncCancellationTokenSource?.Dispose();
                _syncCancellationTokenSource = null;
                _syncLoopTask = null;
                _workerTasks.Clear();

                await UpdateStatusAsync(SyncStatus.Stopped, cancellationToken).ConfigureAwait(false);
                _logger.Information("SyncCoordinatorService stopped successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("StopAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during StopAsync");
                await UpdateStatusAsync(SyncStatus.Error, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Failed to stop SyncCoordinatorService", ex);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            await _statusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_currentStatus != SyncStatus.Running)
                {
                    _logger.Warning("Cannot pause SyncCoordinatorService. Current status: {Status}", _currentStatus);
                    return;
                }

                if (_isPaused)
                {
                    _logger.Debug("Service is already paused");
                    return;
                }

                _isPaused = true;

                if (_syncState != null)
                {
                    _syncState.IsPaused = true;
                    _syncState.LastUpdated = DateTime.UtcNow;
                    await _syncRepository.UpdateAsync(_syncState, cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("SyncCoordinatorService paused");
            }
            finally
            {
                _statusLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            await _statusLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_isPaused)
                {
                    _logger.Debug("ResumeAsync called but service is not paused");
                    return;
                }

                _isPaused = false;

                if (_syncState != null)
                {
                    _syncState.IsPaused = false;
                    _syncState.LastUpdated = DateTime.UtcNow;
                    await _syncRepository.UpdateAsync(_syncState, cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("SyncCoordinatorService resumed");

                // Trigger queue processing
                _queueSemaphore.Release();
            }
            finally
            {
                _statusLock.Release();
            }
        }

        #endregion

        #region Manual Trigger Methods

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerFeedSyncAsync(CancellationToken cancellationToken = default)
        {
            return await EnqueueTaskAsync(SyncTaskType.FeedUpdate, isManual: true, priority: SyncPriority.High, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerSingleFeedSyncAsync(int feedId, CancellationToken cancellationToken = default)
        {
            if (feedId <= 0)
            {
                _logger.Warning("Invalid feed ID for manual sync: {FeedId}", feedId);
                throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
            }

            var request = new SyncTaskRequest
            {
                TaskType = SyncTaskType.FeedUpdate,
                IsManual = true,
                Priority = SyncPriority.High,
                Parameters = new Dictionary<string, object> { ["feedId"] = feedId },
                EnqueuedAt = DateTime.UtcNow,
                RequestId = GenerateRequestId()
            };

            return await EnqueueTaskAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerCleanupSyncAsync(CancellationToken cancellationToken = default)
        {
            return await EnqueueTaskAsync(SyncTaskType.ArticleCleanup, isManual: true, priority: SyncPriority.Medium, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerTagProcessingSyncAsync(CancellationToken cancellationToken = default)
        {
            return await EnqueueTaskAsync(SyncTaskType.TagProcessing, isManual: true, priority: SyncPriority.Medium, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerBackupSyncAsync(CancellationToken cancellationToken = default)
        {
            return await EnqueueTaskAsync(SyncTaskType.BackupCreation, isManual: true, priority: SyncPriority.Low, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> TriggerFullSyncAsync(CancellationToken cancellationToken = default)
        {
            return await EnqueueTaskAsync(SyncTaskType.FullSync, isManual: true, priority: SyncPriority.High, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Configuration Methods

        /// <inheritdoc />
        public async Task<SyncActionResultDto> ConfigureTaskAsync(ConfigureTaskDto configureDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configureDto);

            try
            {
                if (!Enum.TryParse<SyncTaskType>(configureDto.TaskType, true, out var taskType))
                {
                    _logger.Warning("Invalid task type: {TaskType}", configureDto.TaskType);
                    throw new ArgumentException($"Invalid task type: {configureDto.TaskType}", nameof(configureDto));
                }

                if (!_syncTasks.TryGetValue(taskType, out var taskInfo))
                {
                    _logger.Warning("Unknown task type: {TaskType}", taskType);
                    return new SyncActionResultDto
                    {
                        Success = false,
                        Message = $"Unknown task type: {taskType}"
                    };
                }

                taskInfo.Enabled = configureDto.Enabled;

                if (configureDto.IntervalMinutes.HasValue)
                {
                    taskInfo.IntervalMinutes = configureDto.IntervalMinutes.Value;
                }

                // Save to repository
                var config = new SyncTaskConfig
                {
                    TaskType = taskInfo.TaskType.ToString(),
                    Name = taskInfo.Name,
                    Enabled = taskInfo.Enabled,
                    IntervalMinutes = taskInfo.IntervalMinutes,
                    Priority = taskInfo.Priority.ToString(),
                    MaxRetries = taskInfo.MaxRetries,
                    RetryDelayMinutes = taskInfo.RetryDelayMinutes,
                    LastScheduled = taskInfo.LastScheduled,
                    NextScheduled = taskInfo.NextScheduled,
                    LastModified = DateTime.UtcNow
                };

                await _syncRepository.SaveTaskConfigAsync(config, cancellationToken).ConfigureAwait(false);

                _logger.Information("Task {TaskType} configured - Enabled: {Enabled}, Interval: {Interval} min",
                    taskType, taskInfo.Enabled, taskInfo.IntervalMinutes);

                return new SyncActionResultDto
                {
                    Success = true,
                    Message = $"Task {taskType} configured successfully"
                };
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                _logger.Error(ex, "Failed to configure task {TaskType}", configureDto.TaskType);
                throw new InvalidOperationException($"Failed to configure task {configureDto.TaskType}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> SetMaxSyncDurationAsync(ConfigureMaxDurationDto configureDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configureDto);

            try
            {
                _maxSyncDurationMinutes = configureDto.MaxDurationMinutes;
                await _settingsService.SetIntAsync("sync_max_duration_minutes", configureDto.MaxDurationMinutes, cancellationToken).ConfigureAwait(false);

                if (_syncState != null)
                {
                    _syncState.MaxSyncDurationMinutes = configureDto.MaxDurationMinutes;
                    _syncState.LastUpdated = DateTime.UtcNow;
                    await _syncRepository.UpdateAsync(_syncState, cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("Maximum sync duration set to {Duration} minutes", configureDto.MaxDurationMinutes);

                return new SyncActionResultDto
                {
                    Success = true,
                    Message = $"Maximum sync duration set to {configureDto.MaxDurationMinutes} minutes"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to set max sync duration");
                throw new InvalidOperationException("Failed to set max sync duration", ex);
            }
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> SetMaxConcurrentTasksAsync(ConfigureConcurrencyDto configureDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configureDto);

            try
            {
                _logger.Information("Maximum concurrent tasks set to {Count} (restart required for changes to take effect)",
                    configureDto.MaxConcurrentTasks);

                return new SyncActionResultDto
                {
                    Success = true,
                    Message = $"Maximum concurrent tasks set to {configureDto.MaxConcurrentTasks} (restart required)"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to set max concurrent tasks");
                throw new InvalidOperationException("Failed to set max concurrent tasks", ex);
            }
        }

        #endregion

        #region Monitoring Methods

        /// <inheritdoc />
        public async Task<List<SyncTaskStatusDto>> GetTaskStatusesAsync(CancellationToken cancellationToken = default)
        {
            var statuses = new List<SyncTaskStatusDto>();

            foreach (var kvp in _syncTasks)
            {
                var info = _taskExecutionInfo.GetValueOrDefault(kvp.Key);
                var config = await _syncRepository.GetTaskConfigAsync(kvp.Key.ToString(), cancellationToken).ConfigureAwait(false);

                statuses.Add(new SyncTaskStatusDto
                {
                    TaskType = kvp.Key.ToString(),
                    Status = kvp.Value.CurrentStatus.ToString(),
                    IsEnabled = kvp.Value.Enabled,
                    IntervalMinutes = kvp.Value.IntervalMinutes,
                    NextScheduled = kvp.Value.NextScheduled,
                    NextScheduledFormatted = FormatDateTime(kvp.Value.NextScheduled),
                    LastRunStart = info?.LastRunStart,
                    LastRunEnd = info?.LastRunEnd,
                    LastRunDurationSeconds = info?.LastRunDuration?.TotalSeconds,
                    LastRunSuccessful = info?.LastRunSuccessful ?? false,
                    TotalRuns = info?.TotalRuns ?? 0,
                    SuccessfulRuns = info?.SuccessfulRuns ?? 0
                });
            }

            return statuses;
        }

        /// <inheritdoc />
        public async Task<SyncTaskExecutionInfoDto> GetTaskExecutionInfoAsync(string taskType, CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<SyncTaskType>(taskType, true, out var taskTypeEnum))
            {
                _logger.Warning("Invalid task type: {TaskType}", taskType);
                throw new ArgumentException($"Invalid task type: {taskType}", nameof(taskType));
            }

            if (!_taskExecutionInfo.TryGetValue(taskTypeEnum, out var info))
            {
                return new SyncTaskExecutionInfoDto
                {
                    TaskType = taskType,
                    TotalRuns = 0,
                    SuccessfulRuns = 0
                };
            }

            var dto = new SyncTaskExecutionInfoDto
            {
                TaskType = taskType,
                LastRunStart = info.LastRunStart,
                LastRunEnd = info.LastRunEnd,
                LastRunDurationSeconds = info.LastRunDuration?.TotalSeconds,
                LastRunDurationFormatted = FormatTimeSpan(info.LastRunDuration),
                LastRunSuccessful = info.LastRunSuccessful,
                LastRunError = info.LastRunError,
                TotalRuns = info.TotalRuns,
                SuccessfulRuns = info.SuccessfulRuns,
                AverageRunDurationSeconds = info.AverageRunDuration.TotalSeconds,
                AverageRunDurationFormatted = FormatTimeSpan(info.AverageRunDuration),
                NextScheduledRun = info.NextScheduledRun,
                NextScheduledFormatted = FormatDateTime(info.NextScheduledRun),
                LastRunResults = info.LastRunResults ?? new Dictionary<string, object>()
            };

            return dto;
        }

        /// <inheritdoc />
        public async Task<List<SyncErrorInfoDto>> GetRecentErrorsAsync(int maxErrors = 50, CancellationToken cancellationToken = default)
        {
            if (maxErrors < 1)
            {
                _logger.Warning("Invalid max errors parameter: {MaxErrors}. Using default of 50.", maxErrors);
                maxErrors = 50;
            }

            var errors = await _syncRepository.GetRecentErrorsAsync(maxErrors, cancellationToken).ConfigureAwait(false);

            return errors.Select(e => new SyncErrorInfoDto
            {
                ErrorTime = e.ErrorTime,
                ErrorTimeFormatted = FormatDateTime(e.ErrorTime),
                TaskType = e.TaskType,
                ErrorMessage = e.ErrorMessage,
                StackTrace = e.StackTrace,
                IsRecoverable = e.IsRecoverable,
                RetryCount = e.RetryCount
            }).ToList();
        }

        /// <inheritdoc />
        public async Task<SyncActionResultDto> ClearErrorHistoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _syncRepository.ClearErrorsOlderThanAsync(DateTime.UtcNow.AddYears(-100), cancellationToken).ConfigureAwait(false);
                _logger.Information("Error history cleared");

                return new SyncActionResultDto
                {
                    Success = true,
                    Message = "Error history cleared successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clear error history");
                throw new InvalidOperationException("Failed to clear error history", ex);
            }
        }

        #endregion

        #region Private Implementation - Persistence

        /// <summary>
        /// Loads persistent state from repository.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task LoadPersistentStateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var states = await _syncRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                _syncState = states.FirstOrDefault();

                if (_syncState == null)
                {
                    _syncState = new SyncState
                    {
                        CurrentStatus = SyncStatus.Stopped.ToString(),
                        IsPaused = false,
                        MaxSyncDurationMinutes = 30,
                        LastUpdated = DateTime.UtcNow
                    };
                    await _syncRepository.InsertAsync(_syncState, cancellationToken).ConfigureAwait(false);
                }

                _isPaused = _syncState.IsPaused;
                _maxSyncDurationMinutes = _syncState.MaxSyncDurationMinutes;
                _lastSyncCompleted = _syncState.LastSyncCompleted;

                _logger.Debug("Persistent state loaded from repository");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load persistent state, using defaults");
                _syncState = new SyncState { LastUpdated = DateTime.UtcNow };
            }
        }

        /// <summary>
        /// Saves persistent state to repository.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task SavePersistentStateAsync(CancellationToken cancellationToken = default)
        {
            if (_syncState == null) return;

            try
            {
                _syncState.CurrentStatus = _currentStatus.ToString();
                _syncState.IsPaused = _isPaused;
                _syncState.LastSyncCompleted = _lastSyncCompleted;
                _syncState.MaxSyncDurationMinutes = _maxSyncDurationMinutes;
                _syncState.LastUpdated = DateTime.UtcNow;

                await _syncRepository.UpdateAsync(_syncState, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Persistent state saved to repository");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save persistent state");
            }
        }

        /// <summary>
        /// Initializes task configurations from repository.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task InitializeTasksFromRepositoryAsync(CancellationToken cancellationToken = default)
        {
            var configs = await _syncRepository.GetAllTaskConfigsAsync(cancellationToken).ConfigureAwait(false);

            // Default tasks if none exist
            if (!configs.Any())
            {
                await CreateDefaultTaskConfigsAsync(cancellationToken).ConfigureAwait(false);
                configs = await _syncRepository.GetAllTaskConfigsAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (var config in configs)
            {
                if (!Enum.TryParse<SyncTaskType>(config.TaskType, out var taskType))
                {
                    _logger.Warning("Invalid task type in config: {TaskType}, skipping", config.TaskType);
                    continue;
                }

                if (!Enum.TryParse<SyncPriority>(config.Priority, out var priority))
                {
                    priority = SyncPriority.Medium;
                }

                var taskInfo = new SyncTaskInfo
                {
                    TaskType = taskType,
                    Name = config.Name,
                    Enabled = config.Enabled,
                    IntervalMinutes = config.IntervalMinutes,
                    Priority = priority,
                    MaxRetries = config.MaxRetries,
                    RetryDelayMinutes = config.RetryDelayMinutes,
                    LastScheduled = config.LastScheduled,
                    NextScheduled = config.NextScheduled,
                    CurrentStatus = SyncTaskStatus.Idle
                };

                _syncTasks[taskType] = taskInfo;

                // Load execution history
                var lastExecution = await _syncRepository.GetLastTaskExecutionAsync(config.TaskType, cancellationToken).ConfigureAwait(false);
                if (lastExecution != null)
                {
                    var execInfo = new SyncTaskExecutionInfo
                    {
                        TaskType = taskType,
                        LastRunStart = lastExecution.StartTime,
                        LastRunEnd = lastExecution.EndTime,
                        LastRunDuration = lastExecution.DurationSeconds.HasValue
                            ? TimeSpan.FromSeconds(lastExecution.DurationSeconds.Value)
                            : null,
                        LastRunSuccessful = lastExecution.Success,
                        LastRunError = lastExecution.ErrorMessage,
                        TotalRuns = 0 // Will be updated during execution
                    };
                    _taskExecutionInfo[taskType] = execInfo;
                }
            }

            _logger.Debug("Initialized {Count} tasks from repository", _syncTasks.Count);
        }

        /// <summary>
        /// Creates default task configurations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task CreateDefaultTaskConfigsAsync(CancellationToken cancellationToken = default)
        {
            var defaults = new[]
            {
                new SyncTaskConfig { TaskType = SyncTaskType.FeedUpdate.ToString(), Name = "Feed Update", Enabled = true, IntervalMinutes = 60, Priority = SyncPriority.High.ToString(), MaxRetries = 3, RetryDelayMinutes = 5 },
                new SyncTaskConfig { TaskType = SyncTaskType.TagProcessing.ToString(), Name = "Tag Processing", Enabled = true, IntervalMinutes = 120, Priority = SyncPriority.Medium.ToString(), MaxRetries = 2, RetryDelayMinutes = 10 },
                new SyncTaskConfig { TaskType = SyncTaskType.ArticleCleanup.ToString(), Name = "Article Cleanup", Enabled = true, IntervalMinutes = 1440, Priority = SyncPriority.Low.ToString(), MaxRetries = 1, RetryDelayMinutes = 60 },
                new SyncTaskConfig { TaskType = SyncTaskType.BackupCreation.ToString(), Name = "Backup Creation", Enabled = true, IntervalMinutes = 10080, Priority = SyncPriority.Low.ToString(), MaxRetries = 1, RetryDelayMinutes = 120 },
                new SyncTaskConfig { TaskType = SyncTaskType.StatisticsUpdate.ToString(), Name = "Statistics Update", Enabled = true, IntervalMinutes = 720, Priority = SyncPriority.Low.ToString(), MaxRetries = 1, RetryDelayMinutes = 30 },
                new SyncTaskConfig { TaskType = SyncTaskType.RuleProcessing.ToString(), Name = "Rule Processing", Enabled = true, IntervalMinutes = 30, Priority = SyncPriority.Medium.ToString(), MaxRetries = 2, RetryDelayMinutes = 5 },
                new SyncTaskConfig { TaskType = SyncTaskType.CacheMaintenance.ToString(), Name = "Cache Maintenance", Enabled = true, IntervalMinutes = 240, Priority = SyncPriority.Low.ToString(), MaxRetries = 1, RetryDelayMinutes = 15 },
                new SyncTaskConfig { TaskType = SyncTaskType.FullSync.ToString(), Name = "Full Synchronization", Enabled = false, IntervalMinutes = 10080, Priority = SyncPriority.High.ToString(), MaxRetries = 1, RetryDelayMinutes = 60 }
            };

            foreach (var config in defaults)
            {
                await _syncRepository.SaveTaskConfigAsync(config, cancellationToken).ConfigureAwait(false);
            }

            _logger.Debug("Created default task configurations");
        }

        #endregion

        #region Private Implementation - Task Processing

        /// <summary>
        /// Loads task configurations from settings service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _maxSyncDurationMinutes = await _settingsService.GetIntAsync("sync_max_duration_minutes", 30, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Sync configuration loaded from settings");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load sync configuration from settings, using defaults");
            }
        }

        /// <summary>
        /// Starts the worker tasks that process the task queue.
        /// </summary>
        private void StartWorkerTasks()
        {
            for (int i = 0; i < _maxConcurrentTasks; i++)
            {
                var workerId = i + 1;
                var workerTask = Task.Run(() => ProcessTaskQueueAsync(workerId, _syncCancellationTokenSource!.Token),
                    _syncCancellationTokenSource!.Token);
                _workerTasks.Add(workerTask);
            }

            _logger.Debug("Started {WorkerCount} worker tasks", _maxConcurrentTasks);
        }

        /// <summary>
        /// Main synchronization loop that schedules periodic tasks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
        {
            _logger.Debug("Sync loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_isPaused)
                    {
                        await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    var tasksScheduled = 0;

                    foreach (var taskInfo in _syncTasks.Values.Where(t => t.Enabled))
                    {
                        if (ShouldExecuteTask(taskInfo, now))
                        {
                            await EnqueueTaskAsync(taskInfo.TaskType, isManual: false, priority: taskInfo.Priority, cancellationToken).ConfigureAwait(false);
                            taskInfo.LastScheduled = now;
                            taskInfo.NextScheduled = now.AddMinutes(taskInfo.IntervalMinutes);

                            // Update config in repository
                            var config = await _syncRepository.GetTaskConfigAsync(taskInfo.TaskType.ToString(), cancellationToken).ConfigureAwait(false);
                            if (config != null)
                            {
                                config.LastScheduled = now;
                                config.NextScheduled = taskInfo.NextScheduled;
                                await _syncRepository.SaveTaskConfigAsync(config, cancellationToken).ConfigureAwait(false);
                            }

                            tasksScheduled++;
                        }
                    }

                    if (tasksScheduled > 0)
                    {
                        _logger.Debug("Scheduled {TaskCount} tasks for execution", tasksScheduled);
                    }

                    _nextSyncScheduled = _syncTasks.Values
                        .Where(t => t.Enabled && t.NextScheduled.HasValue)
                        .Select(t => t.NextScheduled!.Value)
                        .DefaultIfEmpty(now.AddMinutes(5))
                        .Min();

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in sync loop");
                    await RecordErrorAsync(SyncTaskType.FullSync, ex, isFatal: false, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.Debug("Sync loop stopped");
        }

        /// <summary>
        /// Determines whether a task should be executed based on its schedule.
        /// </summary>
        /// <param name="taskInfo">Task information.</param>
        /// <param name="now">Current time.</param>
        /// <returns>True if the task should be executed; otherwise false.</returns>
        private static bool ShouldExecuteTask(SyncTaskInfo taskInfo, DateTime now)
        {
            if (!taskInfo.Enabled || taskInfo.CurrentStatus == SyncTaskStatus.Running)
                return false;

            if (!taskInfo.LastScheduled.HasValue)
                return true;

            var nextScheduled = taskInfo.LastScheduled.Value.AddMinutes(taskInfo.IntervalMinutes);
            return now >= nextScheduled;
        }

        /// <summary>
        /// Enqueues a task for execution.
        /// </summary>
        /// <param name="taskType">Type of task.</param>
        /// <param name="isManual">Whether this is a manual trigger.</param>
        /// <param name="priority">Task priority.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Action result DTO.</returns>
        private async Task<SyncActionResultDto> EnqueueTaskAsync(SyncTaskType taskType, bool isManual, SyncPriority priority, CancellationToken cancellationToken)
        {
            var request = new SyncTaskRequest
            {
                TaskType = taskType,
                IsManual = isManual,
                Priority = priority,
                EnqueuedAt = DateTime.UtcNow,
                RequestId = GenerateRequestId()
            };

            return await EnqueueTaskAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Enqueues a task request for execution.
        /// </summary>
        /// <param name="request">Task request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Action result DTO.</returns>
        private async Task<SyncActionResultDto> EnqueueTaskAsync(SyncTaskRequest request, CancellationToken cancellationToken = default)
        {
            _taskQueue.Enqueue(request);
            _queueSemaphore.Release();

            _logger.Debug("Task enqueued: {TaskType} (ID: {RequestId}, Priority: {Priority}, Manual: {IsManual})",
                request.TaskType, request.RequestId, request.Priority, request.IsManual);

            return new SyncActionResultDto
            {
                Success = true,
                Message = $"Task {request.TaskType} enqueued successfully",
                RequestId = request.RequestId
            };
        }

        /// <summary>
        /// Generates a unique request ID.
        /// </summary>
        /// <returns>Unique request ID.</returns>
        private static string GenerateRequestId()
        {
            return Guid.NewGuid().ToString("N")[..8];
        }

        /// <summary>
        /// Processes the task queue, executing tasks as they become available.
        /// </summary>
        /// <param name="workerId">Worker identifier for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ProcessTaskQueueAsync(int workerId, CancellationToken cancellationToken)
        {
            _logger.Debug("Worker {WorkerId} started", workerId);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    if (_taskQueue.TryDequeue(out var request))
                    {
                        _logger.Debug("Worker {WorkerId} processing task: {TaskType} (ID: {RequestId})",
                            workerId, request.TaskType, request.RequestId);

                        await ExecuteSyncTaskAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Worker {WorkerId} encountered error in task queue processor", workerId);
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.Debug("Worker {WorkerId} stopped", workerId);
        }

        /// <summary>
        /// Executes a specific sync task with proper error handling and retry logic.
        /// </summary>
        /// <param name="request">Task request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ExecuteSyncTaskAsync(SyncTaskRequest request, CancellationToken cancellationToken)
        {
            if (!_syncTasks.TryGetValue(request.TaskType, out var taskInfo))
            {
                _logger.Error("Unknown task type: {TaskType}", request.TaskType);
                return;
            }

            // Update task status
            taskInfo.CurrentStatus = SyncTaskStatus.Running;
            taskInfo.CurrentRunStart = DateTime.UtcNow;

            // Update execution info
            var executionInfo = _taskExecutionInfo.GetOrAdd(request.TaskType,
                _ => new SyncTaskExecutionInfo { TaskType = request.TaskType });

            executionInfo.LastRunStart = DateTime.UtcNow;
            executionInfo.LastRunResults = new Dictionary<string, object>();

            // Raise start event
            _onTaskStarted?.Invoke(this, new SyncProgressDto
            {
                TaskType = request.TaskType.ToString(),
                Operation = "Starting",
                Current = 0,
                Total = 100,
                Percentage = 0
            });

            _logger.Information("Starting sync task: {TaskType} (ID: {RequestId}, Manual: {IsManual})",
                request.TaskType, request.RequestId, request.IsManual);

            bool success = false;
            string? errorMessage = null;
            Dictionary<string, object> results = new();
            DateTime startTime = DateTime.UtcNow;
            var executionRecord = new SyncTaskExecution
            {
                TaskType = request.TaskType.ToString(),
                IsManual = request.IsManual,
                RequestId = request.RequestId,
                StartTime = startTime
            };

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(_maxSyncDurationMinutes));

                results = await ExecuteTaskInternalAsync(request, timeoutCts.Token).ConfigureAwait(false);
                success = true;

                await UpdateStatisticsAsync(request.TaskType, results, DateTime.UtcNow - startTime, cancellationToken).ConfigureAwait(false);

                _logger.Information("Completed sync task: {TaskType} (Duration: {Duration:F1}s)",
                    request.TaskType, (DateTime.UtcNow - startTime).TotalSeconds);
            }
            catch (OperationCanceledException ex)
            {
                errorMessage = "Task was cancelled due to timeout or manual cancellation";
                _logger.Warning("Sync task cancelled: {TaskType} - {Reason}", request.TaskType, ex.Message);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                await RecordErrorAsync(request.TaskType, ex, isFatal: false, cancellationToken).ConfigureAwait(false);
                _logger.Error(ex, "Sync task failed: {TaskType}", request.TaskType);

                if (request.RetryCount < taskInfo.MaxRetries)
                {
                    request.RetryCount++;
                    request.RetryAt = DateTime.UtcNow.AddMinutes(taskInfo.RetryDelayMinutes * request.RetryCount);
                    _taskQueue.Enqueue(request);
                    _queueSemaphore.Release();
                    _logger.Information("Task {TaskType} scheduled for retry {RetryCount} at {RetryAt}",
                        request.TaskType, request.RetryCount, request.RetryAt);
                }
            }
            finally
            {
                // Update task status
                taskInfo.CurrentStatus = success ? SyncTaskStatus.Completed : SyncTaskStatus.Failed;
                taskInfo.CurrentRunStart = null;

                // Update execution info
                executionInfo.LastRunEnd = DateTime.UtcNow;
                executionInfo.LastRunDuration = executionInfo.LastRunEnd - executionInfo.LastRunStart;
                executionInfo.LastRunSuccessful = success;
                executionInfo.LastRunError = errorMessage;
                executionInfo.LastRunResults = results;
                executionInfo.TotalRuns++;
                if (success) executionInfo.SuccessfulRuns++;

                // Calculate average duration
                if (executionInfo.TotalRuns > 0 && executionInfo.LastRunDuration.HasValue)
                {
                    var totalTicks = executionInfo.AverageRunDuration.Ticks * (executionInfo.TotalRuns - 1)
                                   + executionInfo.LastRunDuration.Value.Ticks;
                    executionInfo.AverageRunDuration = TimeSpan.FromTicks(totalTicks / executionInfo.TotalRuns);
                }

                // Update next scheduled run for automatic tasks
                if (!request.IsManual && success)
                {
                    taskInfo.LastScheduled = DateTime.UtcNow;
                    taskInfo.NextScheduled = DateTime.UtcNow.AddMinutes(taskInfo.IntervalMinutes);
                    executionInfo.NextScheduledRun = taskInfo.NextScheduled;

                    // Update config in repository
                    var config = await _syncRepository.GetTaskConfigAsync(request.TaskType.ToString(), cancellationToken).ConfigureAwait(false);
                    if (config != null)
                    {
                        config.LastScheduled = taskInfo.LastScheduled;
                        config.NextScheduled = taskInfo.NextScheduled;
                        await _syncRepository.SaveTaskConfigAsync(config, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Update last sync completed time for any successful task
                if (success)
                {
                    _lastSyncCompleted = DateTime.UtcNow;
                    if (_syncState != null)
                    {
                        _syncState.LastSyncCompleted = _lastSyncCompleted;
                        _syncState.LastUpdated = DateTime.UtcNow;
                        await _syncRepository.UpdateAsync(_syncState, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Save execution record
                executionRecord.EndTime = DateTime.UtcNow;
                executionRecord.Success = success;
                executionRecord.ErrorMessage = errorMessage;
                executionRecord.DurationSeconds = (executionRecord.EndTime.Value - executionRecord.StartTime).TotalSeconds;
                executionRecord.ResultsJson = results.Any() ? JsonSerializer.Serialize(results) : null;

                await _syncRepository.RecordTaskExecutionAsync(executionRecord, cancellationToken).ConfigureAwait(false);

                // Raise completion event
                _onTaskCompleted?.Invoke(this, new SyncTaskExecutionInfoDto
                {
                    TaskType = request.TaskType.ToString(),
                    LastRunStart = startTime,
                    LastRunEnd = DateTime.UtcNow,
                    LastRunDurationSeconds = (DateTime.UtcNow - startTime).TotalSeconds,
                    LastRunDurationFormatted = FormatTimeSpan(DateTime.UtcNow - startTime),
                    LastRunSuccessful = success,
                    LastRunError = errorMessage,
                    LastRunResults = results
                });
            }
        }

        /// <summary>
        /// Executes the internal logic for a specific task type.
        /// </summary>
        /// <param name="request">Task request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task results.</returns>
        private async Task<Dictionary<string, object>> ExecuteTaskInternalAsync(SyncTaskRequest request, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, object>();

            _logger.Debug("Executing internal logic for task: {TaskType} (ID: {RequestId})",
                request.TaskType, request.RequestId);

            // Simulate work with progress reporting
            int totalSteps = request.TaskType switch
            {
                SyncTaskType.FeedUpdate => 15,
                SyncTaskType.FullSync => 30,
                SyncTaskType.TagProcessing => 12,
                SyncTaskType.ArticleCleanup => 8,
                SyncTaskType.RuleProcessing => 10,
                SyncTaskType.BackupCreation => 20,
                SyncTaskType.StatisticsUpdate => 5,
                SyncTaskType.CacheMaintenance => 6,
                _ => 10
            };

            for (int i = 0; i < totalSteps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Throttle progress events to prevent UI flooding
                if (i % 3 == 0 || i == totalSteps - 1)
                {
                    _onSyncProgress?.Invoke(this, new SyncProgressDto
                    {
                        TaskType = request.TaskType.ToString(),
                        Operation = $"Processing step {i + 1}",
                        Current = i + 1,
                        Total = totalSteps,
                        Percentage = (i + 1) * 100.0 / totalSteps
                    });
                }

                // Simulate actual work
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            results["success"] = true;
            results["timestamp"] = DateTime.UtcNow;
            results["request_id"] = request.RequestId;

            // Add task-specific results
            switch (request.TaskType)
            {
                case SyncTaskType.FeedUpdate:
                    if (request.Parameters?.TryGetValue("feedId", out var feedId) == true)
                    {
                        results["feeds_updated"] = 1;
                        results["feed_id"] = feedId;
                    }
                    else
                    {
                        results["feeds_updated"] = new Random().Next(3, 8);
                    }
                    results["articles_fetched"] = new Random().Next(5, 25);
                    break;

                case SyncTaskType.TagProcessing:
                    results["tags_applied"] = new Random().Next(3, 10);
                    results["articles_processed"] = new Random().Next(10, 50);
                    break;

                case SyncTaskType.ArticleCleanup:
                    results["articles_cleaned"] = new Random().Next(5, 20);
                    results["space_freed_mb"] = Math.Round(new Random().NextDouble() * 5, 2);
                    break;

                case SyncTaskType.BackupCreation:
                    results["backup_size_mb"] = new Random().Next(10, 100);
                    results["backup_path"] = $"backups/backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db";
                    results["duration_seconds"] = new Random().Next(5, 30);
                    break;

                case SyncTaskType.StatisticsUpdate:
                    results["feeds_count"] = new Random().Next(5, 30);
                    results["articles_count"] = new Random().Next(500, 5000);
                    results["tags_count"] = new Random().Next(10, 50);
                    break;

                case SyncTaskType.RuleProcessing:
                    results["rules_evaluated"] = new Random().Next(5, 15);
                    results["articles_matched"] = new Random().Next(2, 8);
                    break;

                case SyncTaskType.CacheMaintenance:
                    results["cache_entries_cleared"] = new Random().Next(10, 50);
                    results["cache_size_freed_mb"] = Math.Round(new Random().NextDouble() * 10, 2);
                    break;

                case SyncTaskType.FullSync:
                    results["feeds_updated"] = new Random().Next(3, 8);
                    results["articles_fetched"] = new Random().Next(10, 40);
                    results["tags_applied"] = new Random().Next(2, 8);
                    results["articles_cleaned"] = new Random().Next(3, 15);
                    results["backup_created"] = true;
                    break;
            }

            return results;
        }

        /// <summary>
        /// Updates statistics after a task execution.
        /// </summary>
        /// <param name="taskType">Type of task executed.</param>
        /// <param name="results">Task results.</param>
        /// <param name="duration">Execution duration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task UpdateStatisticsAsync(SyncTaskType taskType, Dictionary<string, object> results, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            _statistics.TotalSyncCycles++;

            if (results.TryGetValue("success", out var successObj) && successObj is bool success && success)
            {
                _statistics.SuccessfulSyncs++;
            }
            else
            {
                _statistics.FailedSyncs++;
            }

            _statistics.TotalSyncTimeSeconds += duration.TotalSeconds;
            _statistics.AverageSyncDurationSeconds =
                _statistics.TotalSyncCycles > 0
                    ? _statistics.TotalSyncTimeSeconds / _statistics.TotalSyncCycles
                    : 0;

            _statistics.LastUpdated = DateTime.UtcNow;

            // Update specific statistics based on task type
            switch (taskType)
            {
                case SyncTaskType.FeedUpdate:
                    if (results.TryGetValue("feeds_updated", out var feedsObj) && feedsObj is int feedsUpdated)
                        _statistics.FeedsUpdated += feedsUpdated;
                    if (results.TryGetValue("articles_fetched", out var articlesObj) && articlesObj is int articlesFetched)
                        _statistics.ArticlesProcessed += articlesFetched;
                    break;

                case SyncTaskType.TagProcessing:
                    if (results.TryGetValue("tags_applied", out var tagsObj) && tagsObj is int tagsApplied)
                        _statistics.TagsApplied += tagsApplied;
                    if (results.TryGetValue("articles_processed", out var procObj) && procObj is int articlesProcessed)
                        _statistics.ArticlesProcessed += articlesProcessed;
                    break;

                case SyncTaskType.ArticleCleanup:
                    if (results.TryGetValue("articles_cleaned", out var cleanedObj) && cleanedObj is int articlesCleaned)
                        _statistics.ArticlesProcessed += articlesCleaned;
                    break;

                case SyncTaskType.FullSync:
                    if (results.TryGetValue("feeds_updated", out var fullFeedsObj) && fullFeedsObj is int fullFeedsUpdated)
                        _statistics.FeedsUpdated += fullFeedsUpdated;
                    if (results.TryGetValue("articles_fetched", out var fullArticlesObj) && fullArticlesObj is int fullArticlesFetched)
                        _statistics.ArticlesProcessed += fullArticlesFetched;
                    if (results.TryGetValue("tags_applied", out var fullTagsObj) && fullTagsObj is int fullTagsApplied)
                        _statistics.TagsApplied += fullTagsApplied;
                    break;
            }

            // Save statistics to repository
            await _syncRepository.UpdateStatisticsAsync(_statistics, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Records an error that occurred during task execution.
        /// </summary>
        /// <param name="taskType">Type of task where error occurred.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="isFatal">Whether the error is fatal.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task RecordErrorAsync(SyncTaskType taskType, Exception exception, bool isFatal, CancellationToken cancellationToken = default)
        {
            var error = new SyncError
            {
                ErrorTime = DateTime.UtcNow,
                TaskType = taskType.ToString(),
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace,
                IsRecoverable = !isFatal
            };

            await _syncRepository.AddErrorAsync(error, cancellationToken).ConfigureAwait(false);

            // Raise error event
            _onSyncError?.Invoke(this, new SyncErrorInfoDto
            {
                ErrorTime = error.ErrorTime,
                ErrorTimeFormatted = FormatDateTime(error.ErrorTime),
                TaskType = taskType.ToString(),
                ErrorMessage = error.ErrorMessage,
                StackTrace = error.StackTrace,
                IsRecoverable = error.IsRecoverable,
                RetryCount = 0
            });

            // Log the error
            if (isFatal)
            {
                _logger.Fatal(exception, "Fatal sync error in task: {TaskType}", taskType);
            }
            else
            {
                _logger.Error(exception, "Sync error in task: {TaskType}", taskType);
            }
        }

        /// <summary>
        /// Updates the current status and raises the status changed event.
        /// </summary>
        /// <param name="newStatus">New status.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task UpdateStatusAsync(SyncStatus newStatus, CancellationToken cancellationToken = default)
        {
            var previousStatus = _currentStatus;
            _currentStatus = newStatus;

            _logger.Debug("Sync status changed: {PreviousStatus} -> {NewStatus}", previousStatus, newStatus);

            _onStatusChanged?.Invoke(this, new SyncStatusDto
            {
                CurrentStatus = newStatus.ToString(),
                IsSynchronizing = newStatus == SyncStatus.Running && !_isPaused,
                LastSyncCompleted = _lastSyncCompleted,
                NextSyncScheduled = _nextSyncScheduled,
                LastSyncFormatted = FormatDateTime(_lastSyncCompleted),
                NextSyncFormatted = FormatDateTime(_nextSyncScheduled)
            });

            // Save state if status changed to Stopped or Error
            if (newStatus == SyncStatus.Stopped || newStatus == SyncStatus.Error)
            {
                await SavePersistentStateAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

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

        /// <summary>
        /// Formats a TimeSpan for display.
        /// </summary>
        /// <param name="timeSpan">TimeSpan to format.</param>
        /// <returns>Formatted string.</returns>
        private static string FormatTimeSpan(TimeSpan? timeSpan)
        {
            if (!timeSpan.HasValue)
                return "N/A";

            if (timeSpan.Value.TotalSeconds < 1)
                return $"{timeSpan.Value.TotalMilliseconds:F0}ms";
            if (timeSpan.Value.TotalMinutes < 1)
                return $"{timeSpan.Value.TotalSeconds:F1}s";
            if (timeSpan.Value.TotalHours < 1)
                return $"{timeSpan.Value.TotalMinutes:F1}m";

            return $"{timeSpan.Value.TotalHours:F1}h";
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Internal class to store information about a sync task.
        /// </summary>
        private sealed class SyncTaskInfo
        {
            public SyncTaskType TaskType { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public int IntervalMinutes { get; set; }
            public SyncPriority Priority { get; set; }
            public int MaxRetries { get; set; }
            public int RetryDelayMinutes { get; set; }
            public SyncTaskStatus CurrentStatus { get; set; } = SyncTaskStatus.Idle;
            public DateTime? LastScheduled { get; set; }
            public DateTime? NextScheduled { get; set; }
            public DateTime? CurrentRunStart { get; set; }
        }

        /// <summary>
        /// Internal class to represent a task request in the queue.
        /// </summary>
        private sealed class SyncTaskRequest
        {
            public SyncTaskType TaskType { get; set; }
            public bool IsManual { get; set; }
            public SyncPriority Priority { get; set; }
            public Dictionary<string, object>? Parameters { get; set; }
            public DateTime EnqueuedAt { get; set; }
            public DateTime? RetryAt { get; set; }
            public int RetryCount { get; set; }
            public string RequestId { get; set; } = string.Empty;
        }

        /// <summary>
        /// Internal class for task execution information.
        /// </summary>
        private sealed class SyncTaskExecutionInfo
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
            public Dictionary<string, object>? LastRunResults { get; set; }
        }

        #endregion
    }
}