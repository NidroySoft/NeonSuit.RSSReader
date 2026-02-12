using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace NeonSuit.RSSReader.Core.Interfaces.Database
{
    /// <summary>
    /// Interface for the RSS Reader database context using EF Core.
    /// Provides database operations with proper async support and transaction management.
    /// </summary>
    public interface IRssReaderDbContext : IAsyncDisposable
    {
        #region DbSet Properties

        /// <summary>
        /// Gets the path to the database file.
        /// </summary>
        string? DatabasePath { get; }

        #endregion

        #region Database Management

        /// <summary>
        /// Ensures the database is created and all migrations are applied.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies any pending migrations to the database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ApplyMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a backup of the database.
        /// </summary>
        /// <param name="backupPath">Path where the backup will be saved.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task BackupAsync(string backupPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the database by deleting all data.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task ResetDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Vacuums the database to reclaim unused space.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task VacuumAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets database statistics.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Database statistics.</returns>
        Task<DatabaseStats> GetStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Transaction Management

        /// <summary>
        /// Begins a new database transaction.
        /// </summary>
        /// <param name="isolationLevel">Transaction isolation level.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The database transaction.</returns>
        Task<IDbContextTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an action within a transaction.
        /// </summary>
        /// <typeparam name="T">Return type of the action.</typeparam>
        /// <param name="action">Action to execute.</param>
        /// <param name="isolationLevel">Transaction isolation level.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the action.</returns>
        Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        #endregion

        #region Raw SQL Operations

        /// <summary>
        /// Executes a raw SQL query and returns the results.
        /// </summary>
        /// <typeparam name="T">Type of entities to return.</typeparam>
        /// <param name="sql">SQL query string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="parameters">Query parameters.</param>
        /// <returns>List of entities.</returns>
        Task<List<T>> ExecuteRawQueryAsync<T>(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters) where T : class;

        /// <summary>
        /// Executes a SQL command that doesn't return results.
        /// </summary>
        /// <param name="sql">SQL command string.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="parameters">Command parameters.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters);

        #endregion

        #region Database Models

        /// <summary>
        /// Database statistics model.
        /// </summary>
        public class DatabaseStats
        {
            /// <summary>
            /// Total size of the database in bytes.
            /// </summary>
            public long TotalSize { get; set; }

            /// <summary>
            /// Number of articles in the database.
            /// </summary>
            public int ArticleCount { get; set; }

            /// <summary>
            /// Number of feeds in the database.
            /// </summary>
            public int FeedCount { get; set; }

            /// <summary>
            /// Number of rules in the database.
            /// </summary>
            public int RuleCount { get; set; }

            /// <summary>
            /// Number of notification logs in the database.
            /// </summary>
            public int NotificationCount { get; set; }

            /// <summary>
            /// Date and time of the last backup.
            /// </summary>
            public DateTime? LastBackup { get; set; }

            /// <summary>
            /// Returns a string representation of the statistics.
            /// </summary>
            public override string ToString()
            {
                var sizeInMB = TotalSize / (1024.0 * 1024.0);
                var lastBackupStr = LastBackup?.ToString("yyyy-MM-dd HH:mm") ?? "Never";
                return $"Size: {sizeInMB:F2} MB, Articles: {ArticleCount}, Feeds: {FeedCount}, Rules: {RuleCount}, Last Backup: {lastBackupStr}";
            }
        }

        #endregion
    }
}