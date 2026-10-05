// =======================================================
// Data/Database/RssReaderDbContext.Maintenance.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        /// <inheritdoc/>
        public async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default)
        {
            _logger.Information("Ensuring database is created...");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await Database.EnsureCreatedAsync(cancellationToken);
                await ApplySqliteOptimizationsAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Information("Database ensured in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to ensure database creation");
                throw;
            }
        }

        private async Task ApplySqliteOptimizationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA synchronous = NORMAL;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA cache_size = -10000;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA temp_store = MEMORY;", cancellationToken);
                await Database.ExecuteSqlRawAsync("PRAGMA mmap_size = 268435456;", cancellationToken);

                _logger.Debug("SQLite optimizations applied");
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Some SQLite optimizations failed, continuing");
            }
        }

        /// <inheritdoc/>
        public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
        {
            var pendingMigrations = await Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingList = pendingMigrations.ToList();

            if (pendingList.Any())
            {
                _logger.Information("Applying {Count} pending migrations...", pendingList.Count);
                var stopwatch = Stopwatch.StartNew();

                await Database.MigrateAsync(cancellationToken);
                await ApplySqliteOptimizationsAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Information("Migrations applied in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
        }

        /// <inheritdoc/>
        public async Task BackupAsync(string backupPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                throw new ArgumentException("Backup path cannot be null or empty", nameof(backupPath));

            var fullPath = Path.GetFullPath(backupPath);
            var backupDir = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            _logger.Information("Starting database backup to: {BackupPath}", fullPath);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // SQLite's online backup API preserves committed WAL data and avoids SQL path interpolation.
                var connection = (SqliteConnection)Database.GetDbConnection();
                var alreadyOpen = connection.State == System.Data.ConnectionState.Open;
                if (!alreadyOpen) await connection.OpenAsync(cancellationToken);
                try
                {
                    var targetOptions = new SqliteConnectionStringBuilder { DataSource = fullPath };
                    await using var target = new SqliteConnection(targetOptions.ToString());
                    await target.OpenAsync(cancellationToken);
                    connection.BackupDatabase(target);
                }
                finally
                {
                    if (!alreadyOpen) await connection.CloseAsync();
                }

                stopwatch.Stop();
                var backupSize = new FileInfo(fullPath).Length;

                _logger.Information("Backup completed in {ElapsedMs}ms. Size: {BackupSize:N0} bytes",
                    stopwatch.ElapsedMilliseconds, backupSize);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Backup operation failed");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task ResetDatabaseAsync(CancellationToken cancellationToken = default)
        {
            _logger.Warning("Resetting database...");
            var stopwatch = Stopwatch.StartNew();

            await Database.EnsureDeletedAsync(cancellationToken);
            await EnsureDatabaseCreatedAsync(cancellationToken);

            stopwatch.Stop();
            _logger.Warning("Database reset completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }

        /// <inheritdoc/>
        public async Task VacuumAsync(CancellationToken cancellationToken = default)
        {
            _logger.Information("Running database vacuum...");
            var stopwatch = Stopwatch.StartNew();

            await Database.ExecuteSqlRawAsync("VACUUM;", cancellationToken);

            stopwatch.Stop();
            _logger.Information("Vacuum completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }
}