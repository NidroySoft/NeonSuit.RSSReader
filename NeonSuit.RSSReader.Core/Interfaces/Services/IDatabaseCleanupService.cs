using System;
using System.Threading;
using System.Threading.Tasks;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for database maintenance and cleanup operations.
    /// Orchestrates cleanup workflows, manages configuration, and coordinates
    /// between business rules and data access layers.
    /// </summary>
    public interface IDatabaseCleanupService
    {
        /// <summary>
        /// Performs a complete database cleanup cycle based on current configuration.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{CleanupResult}"/> containing the comprehensive results of all cleanup operations.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown when cleanup is disabled in configuration.</exception>
        Task<CleanupResult> PerformCleanupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes the potential impact of a cleanup operation without executing it.
        /// </summary>
        /// <param name="retentionDays">Number of days to use as retention threshold for the analysis.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{CleanupAnalysis}"/> containing projected impact metrics.
        /// </returns>
        Task<CleanupAnalysis> AnalyzeCleanupAsync(int retentionDays, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the current cleanup configuration settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{CleanupConfiguration}"/> containing the current configuration.
        /// </returns>
        Task<CleanupConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the cleanup configuration settings.
        /// </summary>
        /// <param name="configuration">The new configuration to apply.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
        Task UpdateConfigurationAsync(CleanupConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs cleanup of the image cache directory based on age and size constraints.
        /// </summary>
        /// <param name="maxAgeDays">Maximum age in days for cached images.</param>
        /// <param name="maxSizeMB">Maximum total size in megabytes for the cache.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{ImageCacheCleanupResult}"/> containing the cleanup results.
        /// </returns>
        Task<ImageCacheCleanupResult> CleanupImageCacheAsync(
            int maxAgeDays = 30,
            int maxSizeMB = 500,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves current database statistics for monitoring and diagnostics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{DatabaseStatistics}"/> containing current database metrics.
        /// </returns>
        Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies database integrity and returns detailed results.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{IntegrityCheckResult}"/> containing integrity verification results.
        /// </returns>
        Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event raised when a cleanup operation is starting.
        /// </summary>
        event EventHandler<CleanupStartingEventArgs>? OnCleanupStarting;

        /// <summary>
        /// Event raised when cleanup progress is updated.
        /// </summary>
        event EventHandler<CleanupProgressEventArgs>? OnCleanupProgress;

        /// <summary>
        /// Event raised when a cleanup operation is completed.
        /// </summary>
        event EventHandler<CleanupCompletedEventArgs>? OnCleanupCompleted;
    }
}