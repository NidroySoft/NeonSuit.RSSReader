using NeonSuit.RSSReader.Core.Models;

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
    ///     <item>All methods are asynchronous and fully support <see cref="CancellationToken"/> for timeout and user cancellation.</item>
    ///     <item>Operations that modify data (delete, vacuum, reindex) should be executed in background threads or with progress reporting.</item>
    ///     <item>Long-running tasks (VACUUM, REINDEX on large DBs) may lock the database — implementations should use WAL mode and warn users.</item>
    ///     <item>Results DTOs (<see cref="ArticleDeletionResult"/>, <see cref="VacuumResult"/>, etc.) provide detailed feedback for logging/UI.</item>
    ///     <item>Exceptions should be specific (e.g., <see cref="InvalidOperationException"/> for locked DB, <see cref="SqliteException"/> for SQLite errors).</item>
    /// </list>
    ///
    /// <para>
    /// Business rules & invariants:
    /// </para>
    /// <list type="bullet">
    ///     <item>Old article deletion respects user preferences (keep starred/favorites, keep unread).</item>
    ///     <item>VACUUM and REINDEX are expensive — schedule during low-usage periods or on demand.</item>
    ///     <item>Integrity checks should be non-destructive and report issues without fixing them automatically.</item>
    ///     <item>Tag usage counts must be kept in sync after bulk deletions or tag removals.</item>
    /// </list>
    ///
    /// <para>
    /// Performance & safety notes:
    /// </para>
    /// <list type="bullet">
    ///     <item>Use PRAGMA statements where needed (e.g., PRAGMA integrity_check, PRAGMA wal_checkpoint).</item>
    ///     <item>AnalyzeCleanupImpactAsync should be fast (COUNT queries only, no DELETE).</item>
    ///     <item>Monitor operation duration — timeout long-running tasks if user cancels.</item>
    /// </list>
    /// </remarks>
    public interface IDatabaseCleanupRepository
    {
        /// <summary>
        /// Deletes articles older than the specified cutoff date, with configurable protection for favorites and unread items.
        /// </summary>
        /// <param name="cutoffDate">Articles with PublicationDate &lt; this date are candidates for deletion.</param>
        /// <param name="keepFavorites">If <c>true</c>, starred/favorited articles are excluded from deletion.</param>
        /// <param name="keepUnread">If <c>true</c>, unread articles are excluded regardless of age.</param>
        /// <param name="cancellationToken">Token to cancel the long-running operation.</param>
        /// <returns>A result object with deletion statistics and metadata.</returns>
        /// <remarks>
        /// <para>Business impact:</para>
        /// <list type="bullet">
        ///     <item>Reduces database size and improves query performance over time.</item>
        ///     <item>Preserves important content based on user preferences.</item>
        ///     <item>Triggers counter updates and potential events (e.g., ArticlesDeleted).</item>
        /// </list>
        ///
        /// <para>Implementation notes:</para>
        /// <list type="bullet">
        ///     <item>Uses batched DELETE statements to avoid locking the entire table.</item>
        ///     <item>Transactional — rolls back on failure or cancellation.</item>
        ///     <item>Logs detailed stats for audit trail.</item>
        /// </list>
        ///
        /// <para>Exceptions:</para>
        /// <list type="bullet">
        ///     <item><see cref="ArgumentException"/> if cutoffDate is in the future.</item>
        ///     <item><see cref="OperationCanceledException"/> on cancellation.</item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentException">When cutoffDate is in the future.</exception>
        Task<ArticleDeletionResult> DeleteOldArticlesAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes orphaned records from junction tables (e.g., ArticleTag, FeedCategory) and entities without valid parent references.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result with counts of removed records per table/type.</returns>
        /// <remarks>
        /// <para>Purpose:</para>
        /// <list type="bullet">
        ///     <item>Cleans up after deletions, failed imports, or schema changes.</item>
        ///     <item>Ensures referential integrity when foreign keys are not enforced.</item>
        /// </list>
        ///
        /// <para>Typical orphans:</para>
        /// <list type="bullet">
        ///     <item>ArticleTag entries without matching Article or Tag</item>
        ///     <item>Feed entries with invalid CategoryId</item>
        ///     <item>Notification logs without Article reference</item>
        /// </list>
        ///
        /// <para>Operation is read-only scan + targeted deletes — safe to run periodically.</para>
        /// </remarks>
        Task<OrphanRemovalResult> RemoveOrphanedRecordsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes SQLite VACUUM to reclaim unused space and defragment the database file.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token (may not fully support mid-operation cancel in SQLite).</param>
        /// <returns>Result with file size before/after and bytes reclaimed.</returns>
        /// <remarks>
        /// <para>When to call:</para>
        /// <list type="bullet">
        ///     <item>After large deletions (bulk cleanup, unsubscribe)</item>
        ///     <item>Periodically (monthly) on growing databases</item>
        ///     <item>Before creating backups to reduce backup size</item>
        /// </list>
        ///
        /// <para>Behavior:</para>
        /// <list type="bullet">
        ///     <item>Rewrites entire database file — can take minutes on large DBs.</item>
        ///     <item>Blocks other writes during execution (exclusive lock).</item>
        ///     <item>Should run in background with user notification.</item>
        /// </list>
        /// </remarks>
        Task<VacuumResult> VacuumDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rebuilds one or more database indexes to eliminate fragmentation and improve query performance.
        /// </summary>
        /// <param name="tableNames">Optional: specific tables to reindex. If null, reindexes all main tables (Articles, Feeds, etc.).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// <para>Recommended after:</para>
        /// <list type="bullet">
        ///     <item>Bulk inserts/updates/deletes</item>
        ///     <item>VACUUM</item>
        ///     <item>Schema changes or migrations</item>
        /// </list>
        ///
        /// <para>May take significant time — run during low-usage periods.</para>
        /// </remarks>
        Task RebuildIndexesAsync(
            IEnumerable<string>? tableNames = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates database statistics used by the SQLite query planner.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// <para>Improves query execution plans after significant data changes.</para>
        /// <para>Equivalent to PRAGMA analysis_limit=...; ANALYZE;</para>
        /// <para>Fast operation — safe to run frequently (after cleanup, weekly).</para>
        /// </remarks>
        Task UpdateStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a full integrity check on the SQLite database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result object with overall status and any corruption/errors found.</returns>
        /// <remarks>
        /// <para>Runs PRAGMA integrity_check (full scan) and PRAGMA quick_check.</para>
        /// <para>Non-destructive — reports issues but does not repair.</para>
        /// <para>Use before backups or after crashes/suspicious behavior.</para>
        /// </remarks>
        Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves comprehensive database-level statistics for monitoring and diagnostics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Detailed statistics object (file size, page counts, table stats, etc.).</returns>
        /// <remarks>
        /// <para>Aggregates PRAGMA statements (page_size, page_count, freelist_count, etc.)</para>
        /// <para>Fast — suitable for periodic health checks or admin dashboard.</para>
        /// </remarks>
        Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Simulates a cleanup operation and returns projected impact without modifying data.
        /// </summary>
        /// <param name="cutoffDate">Hypothetical cutoff date.</param>
        /// <param name="keepFavorites">Whether favorites would be preserved.</param>
        /// <param name="keepUnread">Whether unread would be preserved.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Analysis result with projected deletion count, affected date range, estimated space savings.</returns>
        /// <remarks>
        /// <para>Used in UI confirmation dialogs ("This will delete X articles, free Y MB").</para>
        /// <para>Fast COUNT queries only — no DELETE.</para>
        /// </remarks>
        Task<CleanupAnalysisResult> AnalyzeCleanupImpactAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the current physical file size of the SQLite database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Size in bytes (or -1 if in-memory DB).</returns>
        /// <remarks>
        /// Uses FileInfo or PRAGMA page_count * page_size.
        /// Used for storage warnings and progress tracking.
        /// </remarks>
        Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Recalculates and updates usage/count fields on tags based on current associations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of tags whose count was updated.</returns>
        /// <remarks>
        /// <para>Should run after bulk deletions or tag removals.</para>
        /// <para>Maintains Tag.UsageCount for sorting/popularity features.</para>
        /// <para>Uses efficient GROUP BY + UPDATE FROM pattern.</para>
        /// </remarks>
        Task<int> UpdateTagUsageCountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries the date range of articles that would be affected by a cleanup operation.
        /// </summary>
        /// <param name="cutoffDate">Cutoff date for analysis.</param>
        /// <param name="keepFavorites">Exclude favorites?</param>
        /// <param name="keepUnread">Exclude unread?</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple with oldest and newest publication dates of affected articles (or null if none).</returns>
        /// <remarks>
        /// <para>Helps UI show "Deleting articles from 2024-01 to 2025-06".</para>
        /// <para>Fast MIN/MAX query with WHERE clause.</para>
        /// </remarks>
        Task<(DateTime? Oldest, DateTime? Newest)?> GetAffectedArticlesDateRangeAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default);
    }
}