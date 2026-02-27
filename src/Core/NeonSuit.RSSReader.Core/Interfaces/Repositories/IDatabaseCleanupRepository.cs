using NeonSuit.RSSReader.Core.Models.Cleanup;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface dedicated to low-level database maintenance, cleanup, integrity verification,
    /// and performance optimization tasks for the RSS reader SQLite database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository handles operations that are not tied to specific business entities (like articles or feeds)
    /// but are essential for long-term database health, storage management, and performance.
    /// </para>
    ///
    /// <para>
    /// Typical use cases:
    /// </para>
    /// <list type="bullet">
    ///     <item>Scheduled nightly/weekly cleanup of old articles</item>
    ///     <item>Post-import vacuum and reindexing after bulk sync</item>
    ///     <item>Integrity checks before/after major migrations or backups</item>
    ///     <item>Monitoring database size and statistics for alerts (e.g., storage nearly full)</item>
    ///     <item>Dry-run analysis before destructive cleanup operations</item>
    /// </list>
    ///
    /// <para>
    /// Important behavioral guidelines for implementations:
    /// </para>
    /// <list type="bullet">
    ///     <item>All methods are asynchronous and fully support <see cref="CancellationToken"/>.</item>
    ///     <item>Operations that modify data should be executed with progress reporting.</item>
    ///     <item>Long-running tasks (VACUUM, REINDEX) may lock the database — implementations should use WAL mode.</item>
    ///     <item>Results DTOs provide detailed feedback for logging/UI.</item>
    /// </list>
    /// </remarks>
    public interface IDatabaseCleanupRepository
    {
        #region Article Cleanup Operations

        /// <summary>
        /// Deletes articles older than the specified cutoff date, with configurable protection for favorites and unread items.
        /// </summary>
        /// <param name="cutoffDate">Articles with PublicationDate &lt; this date are candidates for deletion.</param>
        /// <param name="keepFavorites">If <c>true</c>, favorited articles are excluded from deletion.</param>
        /// <param name="keepUnread">If <c>true</c>, unread articles are excluded regardless of age.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A result object with deletion statistics.</returns>
        /// <exception cref="ArgumentException">When cutoffDate is in the future.</exception>
        Task<ArticleDeletionResult> DeleteOldArticlesAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Simulates a cleanup operation and returns projected impact without modifying data.
        /// </summary>
        /// <param name="cutoffDate">Hypothetical cutoff date.</param>
        /// <param name="keepFavorites">Whether favorites would be preserved.</param>
        /// <param name="keepUnread">Whether unread would be preserved.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Analysis result with projected deletion count, and estimated space savings.</returns>
        Task<CleanupAnalysis> AnalyzeCleanupImpactAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the date range of articles that would be affected by a cleanup operation.
        /// </summary>
        /// <param name="cutoffDate">Cutoff date for analysis.</param>
        /// <param name="keepFavorites">Exclude favorites?</param>
        /// <param name="keepUnread">Exclude unread?</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple with oldest and newest publication dates of affected articles (or null if none).</returns>
        Task<(DateTime? Oldest, DateTime? Newest)?> GetAffectedArticlesDateRangeAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        #endregion

        #region Orphaned Record Removal

        /// <summary>
        /// Removes orphaned records from junction tables (e.g., ArticleTag, FeedCategory) and entities without valid parent references.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result with counts of removed records per table/type.</returns>
        Task<OrphanRemovalResult> RemoveOrphanedRecordsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Database Maintenance Operations

        /// <summary>
        /// Executes SQLite VACUUM to reclaim unused space and defragment the database file.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result with file size before/after and bytes reclaimed.</returns>
        Task<VacuumResult> VacuumDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rebuilds one or more database indexes to eliminate fragmentation and improve query performance.
        /// </summary>
        /// <param name="tableNames">Optional: specific tables to reindex. If null, reindexes main tables.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RebuildIndexesAsync(
            IEnumerable<string>? tableNames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates database statistics used by the SQLite query planner.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a full integrity check on the SQLite database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result object with overall status and any corruption/errors found.</returns>
        Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive database-level statistics for monitoring and diagnostics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Detailed statistics object.</returns>
        Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the current physical file size of the SQLite database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Size in bytes (or 0 if in-memory DB).</returns>
        Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Recalculates and updates usage/count fields on tags based on current associations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of tags whose count was updated.</returns>
        Task<int> UpdateTagUsageCountsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}