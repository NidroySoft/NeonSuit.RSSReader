using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Implementation of <see cref="ISyncRepository"/> for managing synchronization state and history.
    /// </summary>
    internal class SyncRepository : BaseRepository<SyncState>, ISyncRepository
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SyncRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public SyncRepository(RSSReaderDbContext context, ILogger logger) : base(context, logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
            _logger.Debug("SyncRepository initialized");
#endif
        }

        #endregion

        #region ISyncRepository Implementation

        /// <inheritdoc />
        public async Task<List<SyncTaskConfig>> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all task configurations");
                return await _context.Set<SyncTaskConfig>()
                    .AsNoTracking()
                    .OrderBy(t => t.TaskType)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllTaskConfigsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve task configurations");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SyncTaskConfig?> GetTaskConfigAsync(string taskType, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskType))
                {
                    _logger.Warning("Attempted to get task config with empty task type");
                    return null;
                }

                _logger.Debug("Retrieving task configuration for: {TaskType}", taskType);
                return await _context.Set<SyncTaskConfig>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TaskType == taskType, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTaskConfigAsync operation was cancelled for: {TaskType}", taskType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve task configuration for: {TaskType}", taskType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SaveTaskConfigAsync(SyncTaskConfig config, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(config);

            try
            {
                var existing = await _context.Set<SyncTaskConfig>()
                    .FirstOrDefaultAsync(t => t.TaskType == config.TaskType, cancellationToken)
                    .ConfigureAwait(false);

                if (existing == null)
                {
                    _logger.Debug("Creating new task configuration for: {TaskType}", config.TaskType);
                    config.LastModified = DateTime.UtcNow;
                    await _context.Set<SyncTaskConfig>().AddAsync(config, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.Debug("Updating task configuration for: {TaskType}", config.TaskType);
                    existing.Enabled = config.Enabled;
                    existing.IntervalMinutes = config.IntervalMinutes;
                    existing.Priority = config.Priority;
                    existing.MaxRetries = config.MaxRetries;
                    existing.RetryDelayMinutes = config.RetryDelayMinutes;
                    existing.LastScheduled = config.LastScheduled;
                    existing.NextScheduled = config.NextScheduled;
                    existing.LastModified = DateTime.UtcNow;
                    _context.Entry(existing).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SaveTaskConfigAsync operation was cancelled for: {TaskType}", config.TaskType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save task configuration for: {TaskType}", config.TaskType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SyncStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving sync statistics");
                var stats = await _context.Set<SyncStatistics>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return stats ?? new SyncStatistics();
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetStatisticsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve sync statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateStatisticsAsync(SyncStatistics statistics, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(statistics);

            try
            {
                var existing = await _context.Set<SyncStatistics>()
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                statistics.LastUpdated = DateTime.UtcNow;

                if (existing == null)
                {
                    _logger.Debug("Creating new sync statistics");
                    await _context.Set<SyncStatistics>().AddAsync(statistics, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.Debug("Updating sync statistics");
                    existing.TotalSyncCycles = statistics.TotalSyncCycles;
                    existing.SuccessfulSyncs = statistics.SuccessfulSyncs;
                    existing.FailedSyncs = statistics.FailedSyncs;
                    existing.AverageSyncDurationSeconds = statistics.AverageSyncDurationSeconds;
                    existing.TotalSyncTimeSeconds = statistics.TotalSyncTimeSeconds;
                    existing.ArticlesProcessed = statistics.ArticlesProcessed;
                    existing.FeedsUpdated = statistics.FeedsUpdated;
                    existing.TagsApplied = statistics.TagsApplied;
                    existing.LastUpdated = statistics.LastUpdated;
                    _context.Entry(existing).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateStatisticsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update sync statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task AddErrorAsync(SyncError error, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(error);

            try
            {
                _logger.Debug("Adding sync error for task: {TaskType}", error.TaskType);
                error.ErrorTime = DateTime.UtcNow;
                await _context.Set<SyncError>().AddAsync(error, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AddErrorAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add sync error");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<SyncError>> GetRecentErrorsAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            try
            {
                if (limit < 1)
                {
                    _logger.Warning("Invalid limit for recent errors: {Limit}, using default 50", limit);
                    limit = 50;
                }

                _logger.Debug("Retrieving recent {Limit} sync errors", limit);
                return await _context.Set<SyncError>()
                    .AsNoTracking()
                    .Where(e => !e.Resolved)
                    .OrderByDescending(e => e.ErrorTime)
                    .Take(limit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetRecentErrorsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent sync errors");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ClearErrorsOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Clearing sync errors older than: {CutoffDate}", cutoffDate);
                await _context.Set<SyncError>()
                    .Where(e => e.ErrorTime < cutoffDate)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ClearErrorsOlderThanAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clear old sync errors");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RecordTaskExecutionAsync(SyncTaskExecution execution, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);

            try
            {
                _logger.Debug("Recording task execution for: {TaskType}, RequestId: {RequestId}",
                    execution.TaskType, execution.RequestId);
                await _context.Set<SyncTaskExecution>().AddAsync(execution, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RecordTaskExecutionAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record task execution");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SyncTaskExecution?> GetLastTaskExecutionAsync(string taskType, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskType))
                {
                    _logger.Warning("Attempted to get last execution with empty task type");
                    return null;
                }

                _logger.Debug("Retrieving last execution for task: {TaskType}", taskType);
                return await _context.Set<SyncTaskExecution>()
                    .AsNoTracking()
                    .Where(e => e.TaskType == taskType)
                    .OrderByDescending(e => e.StartTime)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetLastTaskExecutionAsync operation was cancelled for: {TaskType}", taskType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve last execution for task: {TaskType}", taskType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<SyncTaskExecution>> GetTaskExecutionHistoryAsync(string taskType, int limit = 20, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskType))
                {
                    _logger.Warning("Attempted to get execution history with empty task type");
                    return new List<SyncTaskExecution>();
                }

                if (limit < 1)
                {
                    _logger.Warning("Invalid limit for execution history: {Limit}, using default 20", limit);
                    limit = 20;
                }

                _logger.Debug("Retrieving execution history for task: {TaskType}, limit: {Limit}", taskType, limit);
                return await _context.Set<SyncTaskExecution>()
                    .AsNoTracking()
                    .Where(e => e.TaskType == taskType)
                    .OrderByDescending(e => e.StartTime)
                    .Take(limit)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTaskExecutionHistoryAsync operation was cancelled for: {TaskType}", taskType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve execution history for task: {TaskType}", taskType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateTaskScheduleAsync(string taskType, DateTime? lastScheduled, DateTime? nextScheduled, CancellationToken cancellationToken = default)
        {
            try
            {
                var config = await GetTaskConfigAsync(taskType, cancellationToken).ConfigureAwait(false);
                if (config != null)
                {
                    config.LastScheduled = lastScheduled;
                    config.NextScheduled = nextScheduled;
                    config.LastModified = DateTime.UtcNow;
                    await SaveTaskConfigAsync(config, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update task schedule for: {TaskType}", taskType);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ResolveErrorAsync(int errorId, string? resolutionNotes = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var error = await _context.Set<SyncError>()
                    .FirstOrDefaultAsync(e => e.Id == errorId, cancellationToken)
                    .ConfigureAwait(false);

                if (error != null)
                {
                    error.Resolved = true;
                    error.ResolutionNotes = resolutionNotes;
                    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.Debug("Error {ErrorId} marked as resolved", errorId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resolve error ID: {ErrorId}", errorId);
                throw;
            }
        }

        #endregion
    }
}