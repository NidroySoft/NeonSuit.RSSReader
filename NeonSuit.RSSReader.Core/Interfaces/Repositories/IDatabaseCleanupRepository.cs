using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for database maintenance and cleanup operations.
    /// Handles low-level database maintenance tasks including data retention,
    /// orphan removal, vacuum operations, and integrity checks.
    /// </summary>
    public interface IDatabaseCleanupRepository
    {
        /// <summary>
        /// Deletes articles older than the specified cutoff date with optional filters.
        /// </summary>
        /// <param name="cutoffDate">The date threshold for article deletion.</param>
        /// <param name="keepFavorites">If true, favorite articles are preserved regardless of age.</param>
        /// <param name="keepUnread">If true, unread articles are preserved regardless of age.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{ArticleDeletionResult}"/> containing the number of deleted articles
        /// and metadata about the deletion operation.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when cutoffDate is in the future.</exception>
        Task<ArticleDeletionResult> DeleteOldArticlesAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes orphaned records from junction tables and entities without parent references.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{OrphanRemovalResult}"/> containing statistics about removed records.
        /// </returns>
        Task<OrphanRemovalResult> RemoveOrphanedRecordsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reclaims storage space by rebuilding the database file (VACUUM operation).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{VacuumResult}"/> containing space statistics before and after the operation.
        /// </returns>
        Task<VacuumResult> VacuumDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rebuilds database indexes for optimal query performance.
        /// </summary>
        /// <param name="tableNames">Optional list of specific tables to reindex. If null, all main tables are processed.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RebuildIndexesAsync(
            IEnumerable<string>? tableNames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates database statistics for query optimizer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs database integrity verification.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{IntegrityCheckResult}"/> containing the integrity status and any errors found.
        /// </returns>
        Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive database statistics for analysis and monitoring.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{DatabaseStatistics}"/> containing detailed database metrics.
        /// </returns>
        Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyzes the impact of a cleanup operation without performing actual deletions.
        /// </summary>
        /// <param name="cutoffDate">The date threshold for analysis.</param>
        /// <param name="keepFavorites">Whether favorite articles would be preserved.</param>
        /// <param name="keepUnread">Whether unread articles would be preserved.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A <see cref="Task{CleanupAnalysisResult}"/> containing projected deletion counts and space estimates.
        /// </returns>
        Task<CleanupAnalysisResult> AnalyzeCleanupImpactAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current physical size of the database file in bytes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The size of the database file in bytes.</returns>
        Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates tag usage counts based on current article associations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The number of tags updated.</returns>
        Task<int> UpdateTagUsageCountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the date range of articles that would be affected by a cleanup operation.
        /// </summary>
        /// <param name="cutoffDate">The cutoff date for the query.</param>
        /// <param name="keepFavorites">Whether to exclude favorites from the query.</param>
        /// <param name="keepUnread">Whether to exclude unread articles from the query.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>
        /// A tuple containing the oldest and newest publication dates of affected articles,
        /// or null if no articles would be affected.
        /// </returns>
        Task<(DateTime? Oldest, DateTime? Newest)?> GetAffectedArticlesDateRangeAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);
    }
}