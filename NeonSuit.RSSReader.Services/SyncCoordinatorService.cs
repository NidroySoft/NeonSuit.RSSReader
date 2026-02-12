using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;
using System.Collections.Concurrent;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of ISyncCoordinatorService.
    /// Coordinates and manages all background synchronization tasks with
    /// proper scheduling, error handling, and resource management.
    /// Implements a producer-consumer pattern for task execution.
    /// </summary>
    public class SyncCoordinatorService : ISyncCoordinatorService
    {
        private readonly ILogger _logger;
        private readonly ISettingsService _settingsService;

        // Synchronization state
        private SyncStatus _currentStatus = SyncStatus.Stopped;
        private readonly SemaphoreSlim _statusLock = new(1, 1);
        private CancellationTokenSource? _syncCancellationTokenSource;
        private Task? _syncLoopTask;

        // Task management
        private readonly Dictionary<SyncTaskType, SyncTaskInfo> _syncTasks;
        private readonly ConcurrentQueue<SyncTaskRequest> _taskQueue;
        private readonly SemaphoreSlim _queueSemaphore = new(0, int.MaxValue);
        private readonly List<Task> _workerTasks;
        private readonly int _maxConcurrentTasks;

        // Statistics and monitoring
        private readonly SyncStatistics _statistics;
        private readonly ConcurrentBag<SyncErrorInfo> _recentErrors;
        private readonly ConcurrentDictionary<SyncTaskType, SyncTaskExecutionInfo> _taskExecutionInfo;
        private DateTime? _lastSyncCompleted;
        private DateTime? _nextSyncScheduled;

        // Configuration
        private bool _isPaused;
        private int _maxSyncDurationMinutes = 30;
        private const int MAX_ERROR_HISTORY = 100;

        public SyncStatus CurrentStatus => _currentStatus;
        public bool IsSynchronizing => _currentStatus == SyncStatus.Running && !_isPaused;
        public DateTime? LastSyncCompleted => _lastSyncCompleted;
        public DateTime? NextSyncScheduled => _nextSyncScheduled;
        public SyncStatistics Statistics => _statistics;

        // Events
        public event EventHandler<SyncStatusChangedEventArgs>? OnStatusChanged;
        public event EventHandler<SyncTaskStartedEventArgs>? OnTaskStarted;
        public event EventHandler<SyncTaskCompletedEventArgs>? OnTaskCompleted;
        public event EventHandler<SyncErrorEventArgs>? OnSyncError;
        public event EventHandler<SyncProgressEventArgs>? OnSyncProgress;

        public SyncCoordinatorService(ISettingsService settingsService, ILogger logger)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<SyncCoordinatorService>();
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _syncTasks = InitializeSyncTasks();
            _taskQueue = new ConcurrentQueue<SyncTaskRequest>();
            _workerTasks = new List<Task>();
            _maxConcurrentTasks = Environment.ProcessorCount;

            _statistics = new SyncStatistics();
            _recentErrors = new ConcurrentBag<SyncErrorInfo>();
            _taskExecutionInfo = new ConcurrentDictionary<SyncTaskType, SyncTaskExecutionInfo>();

            _logger.Debug("SyncCoordinatorService initialized with {TaskCount} task types", _syncTasks.Count);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // Verificar si el token ya está cancelado
            cancellationToken.ThrowIfCancellationRequested();

            await _statusLock.WaitAsync();
            try
            {
                if (_currentStatus != SyncStatus.Stopped && _currentStatus != SyncStatus.Error)
                {
                    _logger.Warning("Cannot start SyncCoordinatorService. Current status: {Status}", _currentStatus);
                    return;
                }

                // Initialize cancellation token
                _syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Load configuration from settings
                await LoadConfigurationAsync();

                // Start worker tasks
                StartWorkerTasks();

                // Start the main sync loop
                _syncLoopTask = Task.Run(() => RunSyncLoopAsync(_syncCancellationTokenSource.Token),
                    _syncCancellationTokenSource.Token);

                await UpdateStatusAsync(SyncStatus.Running);
                _logger.Information("SyncCoordinatorService started successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to start SyncCoordinatorService");
                await UpdateStatusAsync(SyncStatus.Error);
                throw;
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public async Task StopAsync()
        {
            await _statusLock.WaitAsync();
            try
            {
                if (_currentStatus == SyncStatus.Stopped || _currentStatus == SyncStatus.Stopping)
                    return;

                await UpdateStatusAsync(SyncStatus.Stopping);

                // Cancel all ongoing operations
                _syncCancellationTokenSource?.Cancel();

                // Wait for sync loop to complete without throwing
                if (_syncLoopTask != null)
                {
                    try
                    {
                        await _syncLoopTask;
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error waiting for sync loop to stop");
                    }
                }

                // Wait for worker tasks to complete
                try
                {
                    await Task.WhenAll(_workerTasks.ToArray());
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error waiting for worker tasks to stop");
                }

                // Clean up
                _syncCancellationTokenSource?.Dispose();
                _syncCancellationTokenSource = null;
                _syncLoopTask = null;
                _workerTasks.Clear();

                await UpdateStatusAsync(SyncStatus.Stopped);
                _logger.Information("SyncCoordinatorService stopped successfully");
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public async Task PauseAsync()
        {
            await _statusLock.WaitAsync();
            try
            {
                if (_currentStatus != SyncStatus.Running)
                {
                    _logger.Warning("Cannot pause SyncCoordinatorService. Current status: {Status}", _currentStatus);
                    return;
                }

                _isPaused = true;
                _logger.Information("SyncCoordinatorService paused");
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public async Task ResumeAsync()
        {
            await _statusLock.WaitAsync();
            try
            {
                if (!_isPaused)
                    return;

                _isPaused = false;
                _logger.Information("SyncCoordinatorService resumed");

                // Trigger queue processing
                _queueSemaphore.Release();
            }
            finally
            {
                _statusLock.Release();
            }
        }

        // Manual trigger methods
        public async Task TriggerFeedSyncAsync()
        {
            await EnqueueTaskAsync(SyncTaskType.FeedUpdate, isManual: true, priority: SyncPriority.High);
        }

        public async Task TriggerSingleFeedSyncAsync(int feedId)
        {
            var request = new SyncTaskRequest
            {
                TaskType = SyncTaskType.FeedUpdate,
                IsManual = true,
                Priority = SyncPriority.High,
                Parameters = new Dictionary<string, object> { ["feedId"] = feedId }
            };

            await EnqueueTaskAsync(request);
        }

        public async Task TriggerCleanupSyncAsync()
        {
            await EnqueueTaskAsync(SyncTaskType.ArticleCleanup, isManual: true, priority: SyncPriority.Medium);
        }

        public async Task TriggerTagProcessingSyncAsync()
        {
            await EnqueueTaskAsync(SyncTaskType.TagProcessing, isManual: true, priority: SyncPriority.Medium);
        }

        public async Task TriggerBackupSyncAsync()
        {
            await EnqueueTaskAsync(SyncTaskType.BackupCreation, isManual: true, priority: SyncPriority.Low);
        }

        public async Task TriggerFullSyncAsync()
        {
            await EnqueueTaskAsync(SyncTaskType.FullSync, isManual: true, priority: SyncPriority.High);
        }

        // Configuration methods
        public async Task ConfigureTaskAsync(SyncTaskType taskType, bool enabled)
        {
            if (_syncTasks.TryGetValue(taskType, out var taskInfo))
            {
                taskInfo.Enabled = enabled;
                await SaveTaskConfigurationAsync(taskInfo);
                _logger.Information("Task {TaskType} {Action}", taskType, enabled ? "enabled" : "disabled");
            }
        }

        public async Task ConfigureTaskIntervalAsync(SyncTaskType taskType, int intervalMinutes)
        {
            if (_syncTasks.TryGetValue(taskType, out var taskInfo))
            {
                taskInfo.IntervalMinutes = Math.Max(1, intervalMinutes);
                await SaveTaskConfigurationAsync(taskInfo);
                _logger.Information("Task {TaskType} interval set to {Interval} minutes",
                    taskType, intervalMinutes);
            }
        }

        public Task SetMaxSyncDurationAsync(int maxDurationMinutes)
        {
            _maxSyncDurationMinutes = Math.Max(1, maxDurationMinutes);
            _logger.Debug("Maximum sync duration set to {Duration} minutes", maxDurationMinutes);
            return Task.CompletedTask;
        }

        public Task SetMaxConcurrentTasksAsync(int maxConcurrentTasks)
        {
            // Note: Changing this at runtime would require restarting worker tasks
            _logger.Debug("Maximum concurrent tasks set to {Count}", maxConcurrentTasks);
            return Task.CompletedTask;
        }

        // Monitoring methods
        public Task<Dictionary<SyncTaskType, SyncTaskStatus>> GetTaskStatusesAsync()
        {
            var statuses = _syncTasks.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.CurrentStatus
            );
            return Task.FromResult(statuses);
        }

        public Task<SyncTaskExecutionInfo> GetTaskExecutionInfoAsync(SyncTaskType taskType)
        {
            if (_taskExecutionInfo.TryGetValue(taskType, out var info))
                return Task.FromResult(info);

            return Task.FromResult(new SyncTaskExecutionInfo { TaskType = taskType });
        }

        public Task<List<SyncErrorInfo>> GetRecentErrorsAsync(int maxErrors = 50)
        {
            var errors = _recentErrors
                .OrderByDescending(e => e.ErrorTime)
                .Take(maxErrors)
                .ToList();

            return Task.FromResult(errors);
        }

        public Task ClearErrorHistoryAsync()
        {
            while (_recentErrors.TryTake(out _)) { }
            _logger.Debug("Error history cleared");
            return Task.CompletedTask;
        }

        // Private implementation
        private Dictionary<SyncTaskType, SyncTaskInfo> InitializeSyncTasks()
        {
            return new Dictionary<SyncTaskType, SyncTaskInfo>
            {
                [SyncTaskType.FeedUpdate] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.FeedUpdate,
                    Name = "Feed Update",
                    Enabled = true,
                    IntervalMinutes = 60,
                    Priority = SyncPriority.High,
                    MaxRetries = 3,
                    RetryDelayMinutes = 5
                },
                [SyncTaskType.TagProcessing] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.TagProcessing,
                    Name = "Tag Processing",
                    Enabled = true,
                    IntervalMinutes = 120,
                    Priority = SyncPriority.Medium,
                    MaxRetries = 2,
                    RetryDelayMinutes = 10
                },
                [SyncTaskType.ArticleCleanup] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.ArticleCleanup,
                    Name = "Article Cleanup",
                    Enabled = true,
                    IntervalMinutes = 1440, // Daily
                    Priority = SyncPriority.Low,
                    MaxRetries = 1,
                    RetryDelayMinutes = 60
                },
                [SyncTaskType.BackupCreation] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.BackupCreation,
                    Name = "Backup Creation",
                    Enabled = true,
                    IntervalMinutes = 10080, // Weekly
                    Priority = SyncPriority.Low,
                    MaxRetries = 1,
                    RetryDelayMinutes = 120
                },
                [SyncTaskType.StatisticsUpdate] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.StatisticsUpdate,
                    Name = "Statistics Update",
                    Enabled = true,
                    IntervalMinutes = 720, // 12 hours
                    Priority = SyncPriority.Low,
                    MaxRetries = 1,
                    RetryDelayMinutes = 30
                },
                [SyncTaskType.RuleProcessing] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.RuleProcessing,
                    Name = "Rule Processing",
                    Enabled = true,
                    IntervalMinutes = 30,
                    Priority = SyncPriority.Medium,
                    MaxRetries = 2,
                    RetryDelayMinutes = 5
                },
                [SyncTaskType.CacheMaintenance] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.CacheMaintenance,
                    Name = "Cache Maintenance",
                    Enabled = true,
                    IntervalMinutes = 240, // 4 hours
                    Priority = SyncPriority.Low,
                    MaxRetries = 1,
                    RetryDelayMinutes = 15
                },
                [SyncTaskType.FullSync] = new SyncTaskInfo
                {
                    TaskType = SyncTaskType.FullSync,
                    Name = "Full Synchronization",
                    Enabled = false, // Manual only by default
                    IntervalMinutes = 10080, // Weekly
                    Priority = SyncPriority.High,
                    MaxRetries = 1,
                    RetryDelayMinutes = 60
                }
            };
        }

        private async Task LoadConfigurationAsync()
        {
            try
            {
                // Load task configurations from settings
                foreach (var taskInfo in _syncTasks.Values)
                {
                    var enabledKey = $"sync_task_{taskInfo.TaskType.ToString().ToLower()}_enabled";
                    var intervalKey = $"sync_task_{taskInfo.TaskType.ToString().ToLower()}_interval";

                    taskInfo.Enabled = await _settingsService.GetBoolAsync(enabledKey, taskInfo.Enabled);
                    taskInfo.IntervalMinutes = await _settingsService.GetIntAsync(intervalKey, taskInfo.IntervalMinutes);
                }

                // Load general settings
                _maxSyncDurationMinutes = await _settingsService.GetIntAsync("sync_max_duration_minutes", 30);

                _logger.Debug("Sync configuration loaded from settings");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load sync configuration from settings");
                throw;
            }
        }

        private async Task SaveTaskConfigurationAsync(SyncTaskInfo taskInfo)
        {
            try
            {
                var enabledKey = $"sync_task_{taskInfo.TaskType.ToString().ToLower()}_enabled";
                var intervalKey = $"sync_task_{taskInfo.TaskType.ToString().ToLower()}_interval";

                await _settingsService.SetBoolAsync(enabledKey, taskInfo.Enabled);
                await _settingsService.SetIntAsync(intervalKey, taskInfo.IntervalMinutes);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save task configuration for {TaskType}", taskInfo.TaskType);
            }
        }

        private void StartWorkerTasks()
        {
            for (int i = 0; i < _maxConcurrentTasks; i++)
            {
                var workerTask = Task.Run(() => ProcessTaskQueueAsync(_syncCancellationTokenSource!.Token),
                    _syncCancellationTokenSource!.Token);
                _workerTasks.Add(workerTask);
            }

            _logger.Debug("Started {WorkerCount} worker tasks", _maxConcurrentTasks);
        }

        private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
        {
            _logger.Information("Sync loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if paused
                    if (_isPaused)
                    {
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    // Check for scheduled tasks
                    foreach (var taskInfo in _syncTasks.Values.Where(t => t.Enabled))
                    {
                        if (ShouldExecuteTask(taskInfo))
                        {
                            await EnqueueTaskAsync(taskInfo.TaskType, isManual: false, priority: taskInfo.Priority);
                            taskInfo.LastScheduled = DateTime.UtcNow;
                            taskInfo.NextScheduled = DateTime.UtcNow.AddMinutes(taskInfo.IntervalMinutes);
                        }
                    }

                    // Update next scheduled time
                    _nextSyncScheduled = _syncTasks.Values
                        .Where(t => t.Enabled && t.NextScheduled.HasValue)
                        .Select(t => t.NextScheduled!.Value)
                        .DefaultIfEmpty(DateTime.UtcNow.AddMinutes(5))
                        .Min();

                    // Wait for next check
                    var delay = TimeSpan.FromMinutes(1); // Check every minute
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected on cancellation
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in sync loop");
                    await RecordErrorAsync(SyncTaskType.FullSync, ex, isFatal: false);
                    await Task.Delay(5000, cancellationToken); // Wait before retrying
                }
            }

            _logger.Information("Sync loop stopped");
        }

        private bool ShouldExecuteTask(SyncTaskInfo taskInfo)
        {
            if (!taskInfo.Enabled || taskInfo.CurrentStatus == SyncTaskStatus.Running)
                return false;

            if (!taskInfo.LastScheduled.HasValue)
                return true;

            var nextScheduled = taskInfo.LastScheduled.Value.AddMinutes(taskInfo.IntervalMinutes);
            return DateTime.UtcNow >= nextScheduled;
        }

        private async Task EnqueueTaskAsync(SyncTaskType taskType, bool isManual, SyncPriority priority)
        {
            var request = new SyncTaskRequest
            {
                TaskType = taskType,
                IsManual = isManual,
                Priority = priority,
                EnqueuedAt = DateTime.UtcNow
            };

            await EnqueueTaskAsync(request);
        }

        private async Task EnqueueTaskAsync(SyncTaskRequest request)
        {
            _taskQueue.Enqueue(request);
            _queueSemaphore.Release();

            _logger.Debug("Task enqueued: {TaskType} (Priority: {Priority})",
                request.TaskType, request.Priority);

            await Task.CompletedTask;
        }

        private async Task ProcessTaskQueueAsync(CancellationToken cancellationToken)
        {
            _logger.Debug("Task queue processor started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSemaphore.WaitAsync(cancellationToken);

                    if (_taskQueue.TryDequeue(out var request))
                    {
                        await ExecuteSyncTaskAsync(request, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error in task queue processor");
                    await Task.Delay(1000, cancellationToken);
                }
            }

            _logger.Debug("Task queue processor stopped");
        }

        private async Task ExecuteSyncTaskAsync(SyncTaskRequest request, CancellationToken cancellationToken)
        {
            var taskInfo = _syncTasks[request.TaskType];

            // Update task status
            taskInfo.CurrentStatus = SyncTaskStatus.Running;
            taskInfo.CurrentRunStart = DateTime.UtcNow;

            // Update execution info
            var executionInfo = _taskExecutionInfo.GetOrAdd(request.TaskType,
                _ => new SyncTaskExecutionInfo { TaskType = request.TaskType });

            executionInfo.LastRunStart = DateTime.UtcNow;

            // Raise start event
            OnTaskStarted?.Invoke(this, new SyncTaskStartedEventArgs(request.TaskType, DateTime.UtcNow));

            _logger.Information("Starting sync task: {TaskType} (Manual: {IsManual})",
                request.TaskType, request.IsManual);

            bool success = false;
            string? errorMessage = null;
            Dictionary<string, object> results = new();
            DateTime startTime = DateTime.UtcNow;

            try
            {
                // Create a cancellation token with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(_maxSyncDurationMinutes));

                // Execute the task
                results = await ExecuteTaskInternalAsync(request, timeoutCts.Token);
                success = true;

                // Update statistics
                await UpdateStatisticsAsync(request.TaskType, results, DateTime.UtcNow - startTime);

                _logger.Information("Completed sync task: {TaskType} (Duration: {Duration})",
                    request.TaskType, DateTime.UtcNow - startTime);
            }
            catch (OperationCanceledException ex)
            {
                errorMessage = "Task was cancelled due to timeout or manual cancellation";
                _logger.Warning("Sync task cancelled: {TaskType} - {Reason}",
                    request.TaskType, ex.Message);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                await RecordErrorAsync(request.TaskType, ex, isFatal: false);
                _logger.Error(ex, "Sync task failed: {TaskType}", request.TaskType);

                // Handle retries
                if (request.RetryCount < taskInfo.MaxRetries)
                {
                    request.RetryCount++;
                    request.RetryAt = DateTime.UtcNow.AddMinutes(taskInfo.RetryDelayMinutes);
                    _taskQueue.Enqueue(request);
                    _queueSemaphore.Release();
                    _logger.Information("Task {TaskType} scheduled for retry {RetryCount}",
                        request.TaskType, request.RetryCount);
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
                executionInfo.TotalRuns++;
                if (success) executionInfo.SuccessfulRuns++;

                // Calculate average duration
                if (executionInfo.TotalRuns > 0 && executionInfo.LastRunDuration.HasValue)
                {
                    var totalDuration = executionInfo.AverageRunDuration * (executionInfo.TotalRuns - 1)
                                      + executionInfo.LastRunDuration.Value;
                    executionInfo.AverageRunDuration = totalDuration / executionInfo.TotalRuns;
                }

                // Update next scheduled run for automatic tasks
                if (!request.IsManual && success)
                {
                    taskInfo.LastScheduled = DateTime.UtcNow;
                    taskInfo.NextScheduled = DateTime.UtcNow.AddMinutes(taskInfo.IntervalMinutes);
                    executionInfo.NextScheduledRun = taskInfo.NextScheduled;
                }

                // Raise completion event
                OnTaskCompleted?.Invoke(this, new SyncTaskCompletedEventArgs(
                    request.TaskType, startTime, DateTime.UtcNow, success, errorMessage, results));

                // Update last sync time if this was a full sync
                if (request.TaskType == SyncTaskType.FullSync && success)
                {
                    _lastSyncCompleted = DateTime.UtcNow;
                }
            }
        }

        private async Task<Dictionary<string, object>> ExecuteTaskInternalAsync(SyncTaskRequest request, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, object>();

            // Note: This is a placeholder implementation.
            // In a real application, you would:
            // 1. Inject services for each task type (IFeedService, ITagService, etc.)
            // 2. Call the appropriate service methods
            // 3. Handle the results and update progress

            _logger.Debug("Executing sync task: {TaskType}", request.TaskType);

            // Simulate work with progress reporting
            for (int i = 0; i < 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Report progress
                OnSyncProgress?.Invoke(this, new SyncProgressEventArgs(
                    request.TaskType, "Processing", i + 1, 10));

                await Task.Delay(100, cancellationToken); // Simulate work
            }

            // Add sample results
            results["processed"] = 10;
            results["success"] = true;
            results["timestamp"] = DateTime.UtcNow;

            return results;
        }

        private async Task UpdateStatisticsAsync(SyncTaskType taskType, Dictionary<string, object> results, TimeSpan duration)
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

            _statistics.TotalSyncTime += duration;
            _statistics.AverageSyncDurationSeconds =
                _statistics.TotalSyncTime.TotalSeconds / _statistics.TotalSyncCycles;

            _statistics.LastStatisticsUpdate = DateTime.UtcNow;

            // Update specific statistics based on task type
            switch (taskType)
            {
                case SyncTaskType.FeedUpdate:
                    if (results.TryGetValue("feeds_updated", out var feedsObj) && feedsObj is int feedsUpdated)
                        _statistics.FeedsUpdated += feedsUpdated;
                    break;

                case SyncTaskType.TagProcessing:
                    if (results.TryGetValue("tags_applied", out var tagsObj) && tagsObj is int tagsApplied)
                        _statistics.TagsApplied += tagsApplied;
                    break;
            }
        }

        private async Task RecordErrorAsync(SyncTaskType taskType, Exception exception, bool isFatal)
        {
            var errorInfo = new SyncErrorInfo
            {
                ErrorTime = DateTime.UtcNow,
                TaskType = taskType,
                ErrorMessage = exception.Message,
                StackTrace = exception.StackTrace,
                IsRecoverable = !isFatal
            };

            _recentErrors.Add(errorInfo);

            // Limit error history
            while (_recentErrors.Count > MAX_ERROR_HISTORY)
            {
                _recentErrors.TryTake(out _);
            }

            // Raise error event
            OnSyncError?.Invoke(this, new SyncErrorEventArgs(taskType, exception, isFatal));

            // Log the error
            if (isFatal)
            {
                _logger.Fatal(exception, "Fatal sync error in task: {TaskType}", taskType);
            }
            else
            {
                _logger.Error(exception, "Sync error in task: {TaskType}", taskType);
            }

            await Task.CompletedTask;
        }

        private async Task UpdateStatusAsync(SyncStatus newStatus)
        {
            var previousStatus = _currentStatus;
            _currentStatus = newStatus;

            _logger.Debug("Sync status changed: {PreviousStatus} -> {NewStatus}",
                previousStatus, newStatus);

            OnStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs(previousStatus, newStatus));

            await Task.CompletedTask;
        }

        // Helper classes
        private class SyncTaskInfo
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
            public int ErrorCount { get; set; }
        }

        private class SyncTaskRequest
        {
            public SyncTaskType TaskType { get; set; }
            public bool IsManual { get; set; }
            public SyncPriority Priority { get; set; }
            public Dictionary<string, object>? Parameters { get; set; }
            public DateTime EnqueuedAt { get; set; }
            public DateTime? RetryAt { get; set; }
            public int RetryCount { get; set; }
        }

        private enum SyncPriority
        {
            Low,
            Medium,
            High,
            Critical
        }
    }
}