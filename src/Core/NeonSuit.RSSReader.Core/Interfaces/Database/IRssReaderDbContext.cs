// =======================================================
// Core/Interfaces/Database/IRssReaderDbContext.cs
// =======================================================

using Microsoft.EntityFrameworkCore.Storage;
using NeonSuit.RSSReader.Core.DTOs.System;
using System.Data;

namespace NeonSuit.RSSReader.Core.Interfaces.Database
{
    /// <summary>
    /// Core interface for the RSS Reader's Entity Framework Core database context.
    /// Defines standardized access to database operations, transaction management,
    /// raw SQL execution, maintenance tasks, and basic statistics — with full async support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface abstracts the underlying DbContext implementation and is intended to be:
    /// </para>
    /// <list type="bullet">
    ///     <item>Implemented by the actual DbContext class (e.g., RssReaderDbContext : DbContext, IRssReaderDbContext)</item>
    ///     <item>Injected via dependency injection into services, repositories, and background workers</item>
    ///     <item>Used consistently across the application to ensure proper async/await usage and transaction safety</item>
    /// </list>
    ///
    /// <para>
    /// Key design principles:
    /// </para>
    /// <list type="bullet">
    ///     <item>All mutating operations should prefer transactional execution (via ExecuteInTransactionAsync)</item>
    ///     <item>Raw SQL methods are provided for performance-critical or SQLite-specific operations (VACUUM, PRAGMA, etc.)</item>
    ///     <item>Maintenance methods (Backup, Vacuum, Reset) are exposed for admin/debug flows and automated cleanup</item>
    ///     <item>Statistics are lightweight and suitable for periodic health checks or UI dashboard display</item>
    /// </list>
    ///
    /// <para>
    /// Important notes for implementers and consumers:
    /// </para>
    /// <list type="bullet">
    ///     <item>The context is expected to use SQLite (based on DatabasePath and Vacuum/Backup methods)</item>
    ///     <item>Implementations should handle connection pooling, retry logic, and concurrency appropriately</item>
    ///     <item>Do not call SaveChanges/SaveChangesAsync directly on the underlying DbContext from consumers — use transactional wrappers</item>
    ///     <item>Cancellation support is mandatory on all async methods</item>
    /// </list>
    /// </remarks>
    public interface IRSSReaderDbContext : IAsyncDisposable
    {
        #region Database File & Connection Info

        /// <summary>
        /// Gets the full file-system path to the SQLite database file.
        /// </summary>
        /// <remarks>
        /// Used for backup operations, file existence checks, and diagnostics.
        /// Returns null if using in-memory database (testing scenarios).
        /// </remarks>
        string? DatabasePath { get; }

        #endregion

        #region Database Lifecycle & Maintenance

        /// <summary>
        /// Ensures the database file exists and all pending migrations are applied.
        /// Safe to call on application startup.
        /// </summary>
        Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicitly applies any pending EF Core migrations to the database.
        /// </summary>
        /// <remarks>
        /// Usually called internally by EnsureDatabaseCreatedAsync, but exposed for manual migration scenarios.
        /// </remarks>
        Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a full backup copy of the SQLite database file.
        /// </summary>
        /// <param name="backupPath">Target path where the backup file will be saved (must include .db extension).</param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// Uses SQLite backup API for consistency and minimal locking.
        /// Throws if target path exists or is inaccessible.
        /// </remarks>
        Task BackupAsync(string backupPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all data from all tables (truncate-like behavior) while preserving schema.
        /// Intended for development, testing, or user-initiated "clear all data" actions.
        /// </summary>
        /// <remarks>
        /// Should be wrapped in a transaction and confirmed by user.
        /// Does not drop/recreate the database file.
        /// </remarks>
        Task ResetDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes SQLite VACUUM command to reclaim unused space and defragment the database file.
        /// </summary>
        /// <remarks>
        /// Useful after bulk deletes (e.g., article cleanup, archiving).
        /// May take significant time on large databases — run in background.
        /// </remarks>
        Task VacuumAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves lightweight database statistics (size, counts, last backup).
        /// </summary>
        /// <returns>A <see cref="DatabaseStatsDto"/> object with current metrics.</returns>
        /// <remarks>
        /// Should be fast enough for periodic UI refresh or health monitoring.
        /// Counts are approximate when called concurrently with writes.
        /// </remarks>
        Task<DatabaseStatsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Transaction Management

        /// <summary>
        /// Begins a new database transaction with the specified isolation level.
        /// </summary>
        /// <param name="isolationLevel">Defaults to ReadCommitted — safest for most operations.</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        /// <returns>An <see cref="IDbContextTransaction"/> that must be committed or rolled back.</returns>
        /// <remarks>
        /// Prefer <see cref="ExecuteInTransactionAsync{T}"/> for most use cases.
        /// Manual transaction usage requires proper disposal.
        /// </remarks>
        Task<IDbContextTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the provided async action inside a new transaction.
        /// Automatically commits on success or rolls back on exception.
        /// </summary>
        /// <typeparam name="T">Return type of the operation (use Unit/Task for void operations).</typeparam>
        /// <param name="action">The async lambda or method to execute transactionally.</param>
        /// <param name="isolationLevel">Transaction isolation level (default: ReadCommitted).</param>
        /// <param name="cancellationToken"> Cancellation token.</param>
        /// <returns>The result returned by the action.</returns>
        /// <remarks>
        /// Recommended pattern for all write operations that should be atomic.
        /// Example: await db.ExecuteInTransactionAsync(async () => { await db.Articles.AddAsync(...); await db.SaveChangesAsync(); });
        /// </remarks>
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        #endregion

        #region Raw SQL Execution

        /// <summary>
        /// Executes a raw SQL SELECT query and maps results to a list of entities of type T.
        /// </summary>
        /// <typeparam name="T">Entity type with properties matching column names.</typeparam>
        /// <param name="sql">Parameterized SQL query (use {0}, {1} placeholders or named parameters).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="parameters">Values to bind to the query parameters.</param>
        /// <returns>List of materialized entities.</returns>
        /// <remarks>
        /// Use for complex queries or performance-critical reads that cannot be expressed via LINQ.
        /// Does not track entities by default.
        /// </remarks>
        Task<List<T>> ExecuteRawQueryAsync<T>(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters) where T : class;

        /// <summary>
        /// Executes a non-query SQL command (INSERT, UPDATE, DELETE, PRAGMA, etc.).
        /// </summary>
        /// <param name="sql">Parameterized SQL command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="parameters">Values to bind.</param>
        /// <returns>Number of rows affected.</returns>
        /// <remarks>
        /// Ideal for bulk operations, schema changes, or SQLite-specific commands.
        /// </remarks>
        Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters);

        #endregion
    }
}