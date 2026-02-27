using NeonSuit.RSSReader.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for synchronization state and history.
    /// Persists sync task configurations, statistics, and error history.
    /// </summary>
    public interface ISyncRepository : IRepository<SyncState>
    {
        #region Task Configuration

        /// <summary>
        /// Gets all task configurations.
        /// </summary>
        Task<List<SyncTaskConfig>> GetAllTaskConfigsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific task configuration.
        /// </summary>
        Task<SyncTaskConfig?> GetTaskConfigAsync(string taskType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves or updates a task configuration.
        /// </summary>
        Task SaveTaskConfigAsync(SyncTaskConfig config, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Gets the current sync statistics.
        /// </summary>
        Task<SyncStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates sync statistics.
        /// </summary>
        Task UpdateStatisticsAsync(SyncStatistics statistics, CancellationToken cancellationToken = default);

        #endregion

        #region Error History

        /// <summary>
        /// Adds an error to the history.
        /// </summary>
        Task AddErrorAsync(SyncError error, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets recent errors with pagination.
        /// </summary>
        Task<List<SyncError>> GetRecentErrorsAsync(int limit = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears error history older than the specified date.
        /// </summary>
        Task ClearErrorsOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

        #endregion

        #region Task Execution History

        /// <summary>
        /// Records a task execution.
        /// </summary>
        Task RecordTaskExecutionAsync(SyncTaskExecution execution, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the last execution of a specific task.
        /// </summary>
        Task<SyncTaskExecution?> GetLastTaskExecutionAsync(string taskType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets execution history for a task.
        /// </summary>
        Task<List<SyncTaskExecution>> GetTaskExecutionHistoryAsync(string taskType, int limit = 20, CancellationToken cancellationToken = default);

        #endregion
    }
}