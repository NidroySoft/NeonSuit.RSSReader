using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Implementation of <see cref="IDatabaseCleanupRepository"/> for Entity Framework Core database maintenance.
    /// Provides low-level database operations for cleanup, optimization, and integrity verification.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="DatabaseCleanupRepository"/> class.
    /// </remarks>
    /// <param name="dbContext">The database context for connection management.</param>
    /// <param name="logger">The logger instance for diagnostic logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext or logger is null.</exception>
    public class DatabaseCleanupRepository(RssReaderDbContext dbContext, ILogger logger) : IDatabaseCleanupRepository
    {
        private readonly RssReaderDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly ILogger _logger = (logger ?? throw new ArgumentNullException(nameof(logger)))
        .ForContext<DatabaseCleanupRepository>();
        private readonly string? _databasePath = dbContext.DatabasePath;

        /// <inheritdoc />
        public async Task<ArticleDeletionResult> DeleteOldArticlesAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default)
        {
            if (cutoffDate > DateTime.UtcNow)
            {
                throw new ArgumentException("Cutoff date cannot be in the future", nameof(cutoffDate));
            }

            var result = new ArticleDeletionResult();

            try
            {
                // Build query using EF Core LINQ
                var query = _dbContext.Articles.Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                {
                    query = query.Where(a => !a.IsFavorite);
                }

                if (keepUnread)
                {
                    query = query.Where(a => a.Status == ArticleStatus.Read);
                }

                // Get count before deletion for reporting
                result.ArticlesFound = await query.CountAsync(cancellationToken);

                // Get date range of articles to be deleted for metadata
                if (result.ArticlesFound > 0)
                {
                    var dateRange = await query
                        .GroupBy(_ => 1)
                        .Select(g => new
                        {
                            Oldest = g.Min(a => a.PublishedDate),
                            Newest = g.Max(a => a.PublishedDate)
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (dateRange != null)
                    {
                        result.OldestArticleDeleted = dateRange.Oldest;
                        result.NewestArticleDeleted = dateRange.Newest;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Execute deletion using ExecuteDelete for better performance
                result.ArticlesDeleted = await query.ExecuteDeleteAsync(cancellationToken);

                _logger.Information(
                    "Deleted {DeletedCount} articles older than {CutoffDate} (Found: {FoundCount})",
                    result.ArticlesDeleted,
                    cutoffDate,
                    result.ArticlesFound);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Article deletion operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete old articles with cutoff date {CutoffDate}", cutoffDate);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<OrphanRemovalResult> RemoveOrphanedRecordsAsync(CancellationToken cancellationToken = default)
        {
            var result = new OrphanRemovalResult();

            try
            {
                // Remove orphaned ArticleTags (references to non-existent articles or tags)
                var orphanArticleTagsQuery = _dbContext.ArticleTags
                    .Where(at => !_dbContext.Articles.Any(a => a.Id == at.ArticleId) ||
                                 !_dbContext.Tags.Any(t => t.Id == at.TagId));

                result.OrphanedArticleTagsRemoved = await orphanArticleTagsQuery.ExecuteDeleteAsync(cancellationToken);

                // Remove orphaned articles (references to non-existent feeds)
                var orphanArticlesQuery = _dbContext.Articles
                    .Where(a => !_dbContext.Feeds.Any(f => f.Id == a.FeedId));

                result.OrphanedArticlesRemoved = await orphanArticlesQuery.ExecuteDeleteAsync(cancellationToken);

                // Remove orphaned categories if applicable
                var orphanCategoriesQuery = _dbContext.Categories
                    .Where(c => !_dbContext.Feeds.Any(f => f.CategoryId == c.Id));

                result.OrphanedCategoriesRemoved = await orphanCategoriesQuery.ExecuteDeleteAsync(cancellationToken);

                if (result.TotalRecordsRemoved > 0)
                {
                    _logger.Information(
                        "Removed {Total} orphaned records: {ArticleTags} article tags, {Articles} articles, {Categories} categories",
                        result.TotalRecordsRemoved,
                        result.OrphanedArticleTagsRemoved,
                        result.OrphanedArticlesRemoved,
                        result.OrphanedCategoriesRemoved);
                }
                else
                {
                    _logger.Debug("No orphaned records found");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Orphan removal operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove orphaned records");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<VacuumResult> VacuumDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var result = new VacuumResult();
            var startTime = DateTime.UtcNow;

            try
            {
                // Capture size before vacuum
                result.SizeBeforeBytes = await GetDatabaseSizeAsync(cancellationToken);

                // CHANGED: Use EF Core's SqlQuery for VACUUM command
                await _dbContext.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken);

                // Capture size after vacuum
                result.SizeAfterBytes = await GetDatabaseSizeAsync(cancellationToken);
                result.Duration = DateTime.UtcNow - startTime;

                _logger.Information(
                    "Database vacuum completed. Freed {FreedBytes} bytes ({FreedMB:F2} MB). Duration: {Duration}",
                    result.SpaceFreedBytes,
                    result.SpaceFreedBytes / (1024.0 * 1024.0),
                    result.Duration);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Database vacuum operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to vacuum database");
                throw;
            }
        }

        /// <inheritdoc />
        /// <inheritdoc />
        public async Task RebuildIndexesAsync(
            IEnumerable<string>? tableNames = null,
            CancellationToken cancellationToken = default)
        {
            var tables = tableNames?.ToArray() ?? new[] { "Articles", "Feeds", "Tags", "ArticleTags", "Categories" };

            // ✅ Lista blanca para prevenir SQL injection
            var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Articles", "Feeds", "Tags", "ArticleTags", "Categories"
    };

            try
            {
                foreach (var table in tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();


                    if (!allowedTables.Contains(table))
                    {
                        _logger.Warning("Attempted to rebuild index for non-allowed table: {TableName}", table);
                        continue;
                    }


#pragma warning disable EF1002 // Riesgo de SQL injection - Validado con lista blanca
                    await _dbContext.Database.ExecuteSqlRawAsync($"REINDEX {table}", cancellationToken);
#pragma warning restore EF1002

                    _logger.Debug("Rebuilt index for table: {TableName}", table);
                }

                _logger.Information("Rebuilt indexes for {Count} tables", tables.Length);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Index rebuild operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to rebuild indexes");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // CHANGED: Use EF Core's SqlQuery for ANALYZE command
                await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken);
                _logger.Debug("Database statistics updated via ANALYZE");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update database statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var result = new IntegrityCheckResult();
            var startTime = DateTime.UtcNow;

            try
            {
                // CHANGED: Use EF Core's SqlQueryRaw for PRAGMA commands
                // Note: SQLite PRAGMA commands work differently in EF Core
                var connection = _dbContext.Database.GetDbConnection();
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check";

                var integrityResult = await command.ExecuteScalarAsync(cancellationToken) as string;
                result.IsValid = string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase);

                if (!result.IsValid)
                {
                    result.Errors.Add($"Database integrity check failed: {integrityResult}");
                    _logger.Error("Database integrity check failed: {Result}", integrityResult);
                }

                // Check foreign key constraints
                command.CommandText = "PRAGMA foreign_key_check";
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                var violations = new List<ForeignKeyViolation>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    violations.Add(new ForeignKeyViolation
                    {
                        Table = reader.GetString(0),
                        RowId = reader.GetInt64(1),
                        Parent = reader.GetString(2),
                        FKey = reader.GetInt32(3).ToString()
                    });
                }

                if (violations.Any())
                {
                    result.Warnings.Add($"Found {violations.Count} foreign key violations");
                    foreach (var violation in violations)
                    {
                        result.Warnings.Add($"FK Violation: Table {violation.Table}, Row {violation.RowId}, " +
                                          $"Parent {violation.Parent}, FKey {violation.FKey}");
                    }
                    _logger.Warning("Found {Count} foreign key violations during integrity check", violations.Count);
                }

                result.CheckDuration = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check database integrity");
                result.IsValid = false;
                result.Errors.Add($"Integrity check exception: {ex.Message}");
                return result;
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }

        /// <inheritdoc />
        public async Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stats = new DatabaseStatistics();

            try
            {
                // Article statistics with age distribution
                var cutoff30Days = DateTime.UtcNow.AddDays(-30);
                var cutoff60Days = DateTime.UtcNow.AddDays(-60);
                var cutoff90Days = DateTime.UtcNow.AddDays(-90);

                var articleStats = await _dbContext.Articles
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        ReadCount = g.Count(a => a.Status == ArticleStatus.Read),
                        FavoriteCount = g.Count(a => a.IsFavorite),
                        OldestDate = g.Min(a => a.PublishedDate),
                        NewestDate = g.Max(a => a.PublishedDate),
                        OlderThan30 = g.Count(a => a.PublishedDate < cutoff30Days),
                        OlderThan60 = g.Count(a => a.PublishedDate < cutoff60Days),
                        OlderThan90 = g.Count(a => a.PublishedDate < cutoff90Days)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (articleStats != null)
                {
                    stats.TotalArticles = articleStats.Total;
                    stats.ReadArticles = articleStats.ReadCount;
                    stats.UnreadArticles = articleStats.Total - articleStats.ReadCount;
                    stats.FavoriteArticles = articleStats.FavoriteCount;
                    stats.OldestArticleDate = articleStats.OldestDate;
                    stats.NewestArticleDate = articleStats.NewestDate;
                    stats.ArticlesOlderThan30Days = articleStats.OlderThan30;
                    stats.ArticlesOlderThan60Days = articleStats.OlderThan60;
                    stats.ArticlesOlderThan90Days = articleStats.OlderThan90;
                }

                // Feed statistics
                var feedStats = await _dbContext.Feeds
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        ActiveCount = g.Count(f => f.IsActive)
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (feedStats != null)
                {
                    stats.TotalFeeds = feedStats.Total;
                    stats.ActiveFeeds = feedStats.ActiveCount;
                }

                // Tag count
                stats.TotalTags = await _dbContext.Tags.CountAsync(cancellationToken);

                // Database file size
                stats.DatabaseSizeBytes = await GetDatabaseSizeAsync(cancellationToken);
                stats.GeneratedAt = DateTime.UtcNow;

                _logger.Debug(
                    "Statistics retrieved: {Articles} articles, {Feeds} feeds, {Tags} tags, {SizeMB:F2} MB",
                    stats.TotalArticles,
                    stats.TotalFeeds,
                    stats.TotalTags,
                    stats.DatabaseSizeBytes / (1024.0 * 1024.0));

                return stats;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve database statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CleanupAnalysisResult> AnalyzeCleanupImpactAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default)
        {
            var result = new CleanupAnalysisResult
            {
                RetentionDays = (DateTime.UtcNow - cutoffDate).Days,
                CutoffDate = cutoffDate,
                WouldKeepFavorites = keepFavorites,
                WouldKeepUnread = keepUnread
            };

            try
            {
                // Build query matching the actual deletion logic
                var query = _dbContext.Articles.Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                {
                    query = query.Where(a => !a.IsFavorite);
                }

                if (keepUnread)
                {
                    query = query.Where(a => a.Status == ArticleStatus.Read);
                }

                // Count articles to delete
                result.ArticlesToDelete = await query.CountAsync(cancellationToken);

                // Count articles to keep
                var totalArticles = await _dbContext.Articles.CountAsync(cancellationToken);
                result.ArticlesToKeep = totalArticles - result.ArticlesToDelete;

                // Estimate space savings (rough heuristic: ~5KB per article average)
                const long bytesPerArticleEstimate = 5 * 1024;
                result.EstimatedSpaceFreedBytes = result.ArticlesToDelete * bytesPerArticleEstimate;

                // Distribution by feed
                var distributionQuery = query
                    .GroupBy(a => a.Feed!.Title)
                    .Select(g => new { FeedTitle = g.Key!, ArticleCount = g.Count() })
                    .OrderByDescending(g => g.ArticleCount);

                var distributions = await distributionQuery.ToListAsync(cancellationToken);
                result.ArticlesByFeed = distributions.ToDictionary(
                    d => d.FeedTitle,
                    d => d.ArticleCount);

                _logger.Debug(
                    "Cleanup analysis: {ToDelete} articles would be deleted out of {Total} total",
                    result.ArticlesToDelete,
                    totalArticles);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to analyze cleanup impact");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    return 0;
                }

                var info = new FileInfo(_databasePath);
                info.Refresh();
                return info.Length;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get database file size for path: {Path}", _databasePath);
                return 0;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateTagUsageCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // CHANGED: Use raw SQL for efficient bulk update
                var updateQuery = @"
                    UPDATE Tags 
                    SET UsageCount = COALESCE((
                        SELECT COUNT(*) 
                        FROM ArticleTags 
                        WHERE ArticleTags.TagId = Tags.Id
                    ), 0)";

                var updatedCount = await _dbContext.Database.ExecuteSqlRawAsync(updateQuery, cancellationToken);

                _logger.Debug("Updated usage counts for {Count} tags", updatedCount);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update tag usage counts");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<(DateTime? Oldest, DateTime? Newest)?> GetAffectedArticlesDateRangeAsync(
                        DateTime cutoffDate,
                        bool keepFavorites,
                        bool keepUnread,
                        CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbContext.Articles.Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                {
                    query = query.Where(a => !a.IsFavorite);
                }

                if (keepUnread)
                {
                    query = query.Where(a => a.Status == ArticleStatus.Read);
                }

                var dateRange = await query
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        HasAny = g.Any(),
                        Oldest = (DateTime?)g.Min(a => a.PublishedDate),  // Convertir a nullable
                        Newest = (DateTime?)g.Max(a => a.PublishedDate)   // Convertir a nullable
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                // Ahora Oldest es DateTime? y puedes usar HasValue
                if (dateRange == null || !dateRange.HasAny || !dateRange.Oldest.HasValue)
                {
                    return null;
                }

                return (dateRange.Oldest ?? default(DateTime), dateRange.Newest ?? default(DateTime));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get affected articles date range");
                return null;
            }
        }

        // DTOs for internal queries
        private class ForeignKeyViolation
        {
            public string Table { get; set; } = string.Empty;
            public long RowId { get; set; }
            public string Parent { get; set; } = string.Empty;
            public string FKey { get; set; } = string.Empty;
        }
    }
}