// =======================================================
// Data/Database/RssReaderDbContext.RawSql.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace NeonSuit.RSSReader.Data.Database
{
    internal partial class RSSReaderDbContext
    {
        /// <inheritdoc/>
        public async Task<List<T>> ExecuteRawQueryAsync<T>(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters) where T : class
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sql));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Set<T>()
                    .FromSqlRaw(sql, parameters)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                stopwatch.Stop();
                _logger.Debug("Raw query executed in {ElapsedMs}ms. Rows: {RowCount}",
                    stopwatch.ElapsedMilliseconds, result.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Raw query failed");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> ExecuteSqlCommandAsync(
            string sql,
            CancellationToken cancellationToken = default,
            params object[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL command cannot be null or empty", nameof(sql));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var rowsAffected = await Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

                stopwatch.Stop();
                _logger.Debug("SQL command executed in {ElapsedMs}ms. Rows affected: {RowsAffected}",
                    stopwatch.ElapsedMilliseconds, rowsAffected);

                return rowsAffected;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SQL command failed");
                throw;
            }
        }
    }
}