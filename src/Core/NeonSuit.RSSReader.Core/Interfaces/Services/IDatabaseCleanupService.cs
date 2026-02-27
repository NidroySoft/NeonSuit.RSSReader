using NeonSuit.RSSReader.Core.DTOs.Cleanup;
using NeonSuit.RSSReader.Core.DTOs.System;
using NeonSuit.RSSReader.Core.Models.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for database maintenance and cleanup operations.
    /// Orchestrates cleanup workflows, manages configuration, and coordinates
    /// between business rules and data access layers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all database maintenance operations including:
    /// <list type="bullet">
    /// <item>Automated and manual cleanup of old articles and logs</item>
    /// <item>Image cache management to prevent disk space exhaustion</item>
    /// <item>Database integrity verification and statistics gathering</item>
    /// <item>Configuration management for cleanup policies</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// Implementations must ensure:
    /// <list type="bullet">
    /// <item>All cleanup operations are transactional and can be rolled back on failure</item>
    /// <item>Resource-intensive operations respect cancellation tokens and report progress</item>
    /// <item>Image cache cleanup uses efficient file system operations with proper error handling</item>
    /// <item>Protected items (starred/favorite) are never deleted regardless of age</item>
    /// <item>All operations are logged with appropriate detail levels for debugging</item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IDatabaseCleanupService
    {
        #region Core Cleanup Operations

        /// <summary>
        /// Performs a complete database cleanup cycle based on current configuration.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing the comprehensive results of all cleanup operations.</returns>
        /// <exception cref="InvalidOperationException">Thrown when cleanup is disabled in configuration.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CleanupResultDto> PerformCleanupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes the potential impact of a cleanup operation without executing it.
        /// </summary>
        /// <param name="retentionDays">Number of days to use as retention threshold for the analysis.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing projected impact metrics.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="retentionDays"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CleanupAnalysisDto> AnalyzeCleanupAsync(int retentionDays, CancellationToken cancellationToken = default);

        #endregion

        #region Configuration Management

        /// <summary>
        /// Retrieves the current cleanup configuration settings.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing the current configuration.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CleanupConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the cleanup configuration settings.
        /// </summary>
        /// <param name="configuration">The new configuration to apply.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if configuration values are invalid.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task UpdateConfigurationAsync(CleanupConfigurationDto configuration, CancellationToken cancellationToken = default);

        #endregion

        #region Specialized Cleanup Operations

        /// <summary>
        /// Performs cleanup of the image cache directory based on age and size constraints.
        /// </summary>
        /// <param name="maxAgeDays">Maximum age in days for cached images.</param>
        /// <param name="maxSizeMB">Maximum total size in megabytes for the cache.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing the cleanup results.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxAgeDays"/> is less than 1 or <paramref name="maxSizeMB"/> is less than 50.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<ImageCacheCleanupResultDto> CleanupImageCacheAsync(
            int maxAgeDays = 30,
            int maxSizeMB = 500,
            CancellationToken cancellationToken = default);

        #endregion

        #region Monitoring and Diagnostics

        /// <summary>
        /// Retrieves current database statistics for monitoring and diagnostics.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing current database metrics.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<DatabaseStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies database integrity and returns detailed results.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> containing integrity verification results.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<IntegrityCheckResultDto> CheckIntegrityAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Events

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

        #endregion
    }
}