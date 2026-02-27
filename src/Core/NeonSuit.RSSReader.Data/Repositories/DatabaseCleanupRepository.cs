using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Cleanup;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Implementation of <see cref="IDatabaseCleanupRepository"/> for Entity Framework Core database maintenance.
    /// Provides low-level database operations for cleanup, optimization, and integrity verification,
    /// specifically tailored for SQLite databases on resource-constrained environments.
    /// </summary>
    internal class DatabaseCleanupRepository : IDatabaseCleanupRepository
    {
        private readonly RSSReaderDbContext _dbContext;
        private readonly ILogger _logger;
        private readonly string? _databasePath;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseCleanupRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The Serilog logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public DatabaseCleanupRepository(RSSReaderDbContext context, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _dbContext = context;
            _logger = logger.ForContext<DatabaseCleanupRepository>();
            _databasePath = context.DatabasePath;

#if DEBUG
            _logger.Debug("DatabaseCleanupRepository initialized");
#endif
        }

        #endregion

        #region Article Cleanup Operations

        /// <inheritdoc />
        public async Task<ArticleDeletionResult> DeleteOldArticlesAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default)
        {
            if (cutoffDate > DateTime.UtcNow)
            {
                _logger.Warning("Attempted to delete articles with future cutoff date: {CutoffDate}", cutoffDate);
                throw new ArgumentException("Cutoff date cannot be in the future", nameof(cutoffDate));
            }

            var result = new ArticleDeletionResult();

            try
            {
                // Build query for articles to be deleted
                var query = _dbContext.Articles
                    .AsNoTracking()
                    .Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                    query = query.Where(a => !a.IsFavorite);

                if (keepUnread)
                    query = query.Where(a => a.Status == ArticleStatus.Read);

                // Get count before deletion
                var articlesToDelete = await query.CountAsync(cancellationToken).ConfigureAwait(false);
                result.ArticlesFound = articlesToDelete;
                result.TotalArticlesBefore = await _dbContext.Articles.CountAsync(cancellationToken).ConfigureAwait(false);

                // Get date range of articles to be deleted
                if (articlesToDelete > 0)
                {
                    var dateRange = await query
                        .GroupBy(_ => 1)
                        .Select(g => new
                        {
                            Oldest = g.Min(a => a.PublishedDate),
                            Newest = g.Max(a => a.PublishedDate)
                        })
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (dateRange != null)
                    {
                        result.OldestArticleDeleted = dateRange.Oldest;
                        result.NewestArticleDeleted = dateRange.Newest;
                    }
                }

                result.CutoffDateUsed = cutoffDate;
                cancellationToken.ThrowIfCancellationRequested();

                // Execute deletion using ExecuteDeleteAsync
                result.ArticlesDeleted = await _dbContext.Articles
                    .Where(a => query.Select(q => q.Id).Contains(a.Id))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information(
                    "Deleted {DeletedCount} articles older than {CutoffDate:yyyy-MM-dd} (Found: {FoundCount})",
                    result.ArticlesDeleted, cutoffDate, result.ArticlesFound);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteOldArticlesAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete old articles");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CleanupAnalysis> AnalyzeCleanupImpactAsync(
            DateTime cutoffDate,
            bool keepFavorites,
            bool keepUnread,
            CancellationToken cancellationToken = default)
        {
            var result = new CleanupAnalysis
            {
                RetentionDays = (DateTime.UtcNow - cutoffDate).Days,
                CutoffDate = cutoffDate,
                WouldKeepFavorites = keepFavorites,
                WouldKeepUnread = keepUnread
            };

            try
            {
                var query = _dbContext.Articles
                    .AsNoTracking()
                    .Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                    query = query.Where(a => !a.IsFavorite);

                if (keepUnread)
                    query = query.Where(a => a.Status == ArticleStatus.Read);

                result.ArticlesToDelete = await query.CountAsync(cancellationToken).ConfigureAwait(false);
                result.ArticlesFound = result.ArticlesToDelete;

                var totalArticles = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                result.ArticlesToKeep = totalArticles - result.ArticlesToDelete;

                // Get distribution by feed
                var feedDistribution = await query
                    .GroupBy(a => a.FeedId)
                    .Select(g => new { FeedId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.FeedId, g => g.Count, cancellationToken)
                    .ConfigureAwait(false);

                result.ArticlesByFeed = feedDistribution;

                // Estimate space savings (rough heuristic: ~5KB per article)
                const long bytesPerArticleEstimate = 5 * 1024;
                result.EstimatedSpaceFreedBytes = result.ArticlesToDelete * bytesPerArticleEstimate;

                _logger.Debug("Cleanup analysis completed: {ToDelete} articles would be deleted",
                    result.ArticlesToDelete);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AnalyzeCleanupImpactAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to analyze cleanup impact");
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
                var query = _dbContext.Articles
                    .AsNoTracking()
                    .Where(a => a.PublishedDate < cutoffDate);

                if (keepFavorites)
                    query = query.Where(a => !a.IsFavorite);

                if (keepUnread)
                    query = query.Where(a => a.Status == ArticleStatus.Read);

                var dateRange = await query
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        HasAny = g.Any(),
                        Oldest = (DateTime?)g.Min(a => a.PublishedDate),
                        Newest = (DateTime?)g.Max(a => a.PublishedDate)
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (dateRange == null || !dateRange.HasAny || !dateRange.Oldest.HasValue)
                {
                    return null;
                }

                return (dateRange.Oldest, dateRange.Newest);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAffectedArticlesDateRangeAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get affected articles date range");
                throw;
            }
        }

        #endregion

        #region Orphaned Record Removal

        /// <inheritdoc />
        public async Task<OrphanRemovalResult> RemoveOrphanedRecordsAsync(CancellationToken cancellationToken = default)
        {
            var result = new OrphanRemovalResult();

            try
            {
                // Remove orphaned ArticleTags
                var orphanArticleTagsQuery = _dbContext.ArticleTags
                    .Where(at => !_dbContext.Articles.Any(a => a.Id == at.ArticleId) ||
                                 !_dbContext.Tags.Any(t => t.Id == at.TagId));

                result.OrphanedArticleTagsRemoved = await orphanArticleTagsQuery
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Remove orphaned articles
                var orphanArticlesQuery = _dbContext.Articles
                    .Where(a => !_dbContext.Feeds.Any(f => f.Id == a.FeedId));

                result.OrphanedArticlesRemoved = await orphanArticlesQuery
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Remove orphaned categories
                var orphanCategoriesQuery = _dbContext.Categories
                    .Where(c => !_dbContext.Feeds.Any(f => f.CategoryId == c.Id) &&
                                !_dbContext.Categories.Any(child => child.ParentCategoryId == c.Id));

                result.OrphanedCategoriesRemoved = await orphanCategoriesQuery
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("Removed {Count} orphaned records", result.TotalRecordsRemoved);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RemoveOrphanedRecordsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove orphaned records");
                throw;
            }
        }

        #endregion

        #region Database Maintenance Operations

        /// <inheritdoc />
        public async Task<VacuumResult> VacuumDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var result = new VacuumResult();
            var startTime = DateTime.UtcNow;

            try
            {
                result.SizeBeforeBytes = await GetDatabaseSizeAsync(cancellationToken).ConfigureAwait(false);

                await _dbContext.Database.ExecuteSqlRawAsync("VACUUM", cancellationToken).ConfigureAwait(false);

                result.SizeAfterBytes = await GetDatabaseSizeAsync(cancellationToken).ConfigureAwait(false);
                result.Duration = DateTime.UtcNow - startTime;

                _logger.Information("Database vacuum completed. Freed {FreedMB:F2} MB",
                    result.SpaceFreedBytes / (1024.0 * 1024.0));

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("VacuumDatabaseAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to vacuum database");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RebuildIndexesAsync(
            IEnumerable<string>? tableNames = null,
            CancellationToken cancellationToken = default)
        {
            var tables = tableNames?.ToArray() ?? new[] { "Articles", "Feeds", "Tags", "ArticleTags", "Categories" };

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
                        _logger.Warning("Skipping non-allowed table: '{TableName}'", table);
                        continue;
                    }

#pragma warning disable EF1002 // SQL injection risk mitigated by whitelist
                    await _dbContext.Database.ExecuteSqlRawAsync($"REINDEX {table}", cancellationToken)
                        .ConfigureAwait(false);
#pragma warning restore EF1002

                    _logger.Debug("Rebuilt index for table: '{TableName}'", table);
                }

                _logger.Information("Rebuilt indexes for {Count} tables", tables.Length);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RebuildIndexesAsync cancelled");
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
                await _dbContext.Database.ExecuteSqlRawAsync("ANALYZE", cancellationToken).ConfigureAwait(false);
                _logger.Information("Database statistics updated");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateStatisticsAsync cancelled");
                throw;
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
                var connection = _dbContext.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                // Check database integrity
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA integrity_check";
                    var integrityResult = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
                    result.IsValid = string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase);

                    if (!result.IsValid && integrityResult != null)
                        result.Errors.Add($"Integrity check failed: {integrityResult}");
                }

                result.CheckDuration = DateTime.UtcNow - startTime;
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CheckIntegrityAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check database integrity");
                result.IsValid = false;
                result.Errors.Add($"Exception: {ex.Message}");
                return result;
            }
        }

        /// <inheritdoc />
        public async Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var stats = new DatabaseStatistics();

            try
            {
                stats.GeneratedAt = DateTime.UtcNow;

                // Article statistics
                stats.TotalArticles = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                stats.ReadArticles = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(a => a.Status == ArticleStatus.Read, cancellationToken)
                    .ConfigureAwait(false);

                stats.UnreadArticles = stats.TotalArticles - stats.ReadArticles;

                stats.FavoriteArticles = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(a => a.IsFavorite, cancellationToken)
                    .ConfigureAwait(false);

                // Article age distribution
                var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
                var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                stats.ArticlesOlderThan90Days = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(a => a.PublishedDate < ninetyDaysAgo, cancellationToken)
                    .ConfigureAwait(false);

                stats.ArticlesOlderThan60Days = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(a => a.PublishedDate < sixtyDaysAgo, cancellationToken)
                    .ConfigureAwait(false);

                stats.ArticlesOlderThan30Days = await _dbContext.Articles
                    .AsNoTracking()
                    .CountAsync(a => a.PublishedDate < thirtyDaysAgo, cancellationToken)
                    .ConfigureAwait(false);

                // Oldest and newest article dates
                var oldestArticle = await _dbContext.Articles
                    .AsNoTracking()
                    .OrderBy(a => a.PublishedDate)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (oldestArticle != null && oldestArticle.PublishedDate.HasValue)
                    stats.OldestArticleDate = oldestArticle.PublishedDate.Value;

                var newestArticle = await _dbContext.Articles
                    .AsNoTracking()
                    .OrderByDescending(a => a.PublishedDate)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (newestArticle != null && newestArticle.PublishedDate.HasValue)
                    stats.NewestArticleDate = newestArticle.PublishedDate.Value;

                // Feed statistics
                stats.TotalFeeds = await _dbContext.Feeds
                    .AsNoTracking()
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                stats.ActiveFeeds = await _dbContext.Feeds
                    .AsNoTracking()
                    .CountAsync(f => f.IsActive, cancellationToken)
                    .ConfigureAwait(false);

                // Tag statistics
                stats.TotalTags = await _dbContext.Tags
                    .AsNoTracking()
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Database size
                stats.DatabaseSizeBytes = await GetDatabaseSizeAsync(cancellationToken).ConfigureAwait(false);

                return stats;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetStatisticsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve database statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateTagUsageCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var updateQuery = @"
                    UPDATE Tags
                    SET UsageCount = COALESCE((
                        SELECT COUNT(*)
                        FROM ArticleTags
                        WHERE ArticleTags.TagId = Tags.Id
                    ), 0)";

                var updatedCount = await _dbContext.Database
                    .ExecuteSqlRawAsync(updateQuery, cancellationToken)
                    .ConfigureAwait(false);

                _logger.Information("Updated usage counts for {Count} tags", updatedCount);
                return updatedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateTagUsageCountsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update tag usage counts");
                throw;
            }
        }

        #endregion

        #region Internal Helpers

        /// <inheritdoc />
        public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(_databasePath))
                {
                    _logger.Warning("Database path not configured");
                    return 0;
                }

                if (!File.Exists(_databasePath))
                {
                    _logger.Warning("Database file not found at: {Path}", _databasePath);
                    return 0;
                }

                var info = new FileInfo(_databasePath);
                info.Refresh();
                return info.Length;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetDatabaseSizeAsync cancelled");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get database file size");
                return 0;
            }
        }

        #endregion
    }
}