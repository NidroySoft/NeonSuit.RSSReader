using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository implementation for <see cref="Feed"/> entities.
    /// Provides optimized data access with comprehensive logging and support for active/inactive filtering.
    /// </summary>
    internal class FeedRepository : BaseRepository<Feed>, IFeedRepository
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
        public FeedRepository(RSSReaderDbContext context, ILogger logger) : base(context, logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
            _logger.Debug("FeedRepository initialized");
#endif
        }

        #endregion

        #region CRUD Operations

        /// <inheritdoc />
        public override async Task<Feed?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            try
            {
                return await _dbSet
                    .Include(f => f.Category)
                    .Include(f => f.Articles)
                    .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByIdAsync cancelled for feed ID {FeedId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID {FeedId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Feed?> GetByIdNoTrackingAsync(int id, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .FirstOrDefaultAsync(f => f.Id == id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByIdNoTrackingAsync cancelled for feed ID {FeedId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID without tracking {FeedId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                IQueryable<Feed> query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllAsync cancelled (IncludeInactive: {IncludeInactive})", includeInactive);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetWhereAsync(Expression<Func<Feed, bool>> predicate, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            try
            {
                IQueryable<Feed> query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetWhereAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GetWhereAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CountWhereAsync(Expression<Func<Feed, bool>> predicate, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            try
            {
                var query = _dbSet.AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CountWhereAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in CountWhereAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> InsertAsync(Feed entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var result = await base.InsertAsync(entity, cancellationToken).ConfigureAwait(false);
                _logger.Information("Inserted feed '{FeedTitle}' (ID: {FeedId})", entity.Title, entity.Id);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("InsertAsync cancelled for feed '{FeedTitle}'", entity.Title);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.Error(ex, "Database error inserting feed '{FeedTitle}'", entity.Title);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error inserting feed '{FeedTitle}'", entity.Title);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> UpdateAsync(Feed entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var result = await base.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
                _logger.Information("Updated feed '{FeedTitle}' (ID: {FeedId})", entity.Title, entity.Id);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateAsync cancelled for feed ID {FeedId}", entity.Id);
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.Error(ex, "Concurrency conflict updating feed ID {FeedId}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating feed ID {FeedId}", entity.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<int> DeleteAsync(Feed entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var result = await base.DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
                _logger.Information("Deleted feed '{FeedTitle}' (ID: {FeedId})", entity.Title, entity.Id);
                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteAsync cancelled for feed ID {FeedId}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting feed ID {FeedId}", entity.Id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DetachEntityAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                if (id == 0)
                {
                    var trackedEntries = _context.ChangeTracker.Entries<Feed>().ToList();
                    foreach (var entry in trackedEntries)
                        entry.State = EntityState.Detached;

                    _logger.Debug("Detached {Count} feed entities", trackedEntries.Count);
                }
                else
                {
                    var tracked = _context.ChangeTracker.Entries<Feed>()
                        .FirstOrDefault(e => e.Entity.Id == id);

                    if (tracked != null)
                    {
                        tracked.State = EntityState.Detached;
                        _logger.Debug("Detached feed ID {FeedId}", id);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error detaching feed with ID {FeedId}", id);
                throw;
            }
        }

        #endregion

        #region Read Single Entity Operations

        /// <inheritdoc />
        public async Task<Feed?> GetByUrlAsync(string url, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            try
            {
                var query = _dbSet.AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.FirstOrDefaultAsync(f => f.Url == url, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByUrlAsync cancelled for URL {Url}", url);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by URL {Url}", url);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByUrlAsync(string url, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .AnyAsync(f => f.Url == url, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ExistsByUrlAsync cancelled for URL {Url}", url);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking feed existence by URL {Url}", url);
                throw;
            }
        }

        #endregion

        #region Read Collection Operations

        /// <inheritdoc />
        public async Task<List<Feed>> GetByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);

            try
            {
                IQueryable<Feed> query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => f.CategoryId == categoryId);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByCategoryAsync cancelled for category {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetUncategorizedAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => !f.CategoryId.HasValue);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetUncategorizedAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving uncategorized feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => f.IsActive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetActiveAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving active feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetInactiveAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => !f.IsActive)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetInactiveAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving inactive feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsToUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;

                return await _dbSet
                    .AsNoTracking()
                    .Where(f => f.IsActive && (f.NextUpdateSchedule == null || f.NextUpdateSchedule <= now))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFeedsToUpdateAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds ready for update");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetCountByCategoryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(f => f.CategoryId.HasValue)
                    .GroupBy(f => f.CategoryId!.Value)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetCountByCategoryAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed counts by category");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => f.CategoryId.HasValue);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                var feeds = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

                return feeds
                    .GroupBy(f => f.CategoryId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFeedsGroupedByCategoryAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds grouped by category");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);

            try
            {
                IQueryable<Feed> query = _dbSet
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Where(f => f.CategoryId == categoryId)
                    .OrderBy(f => f.Title);

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFeedsByCategoryAsync cancelled for category {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsWithRetentionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(f => f.ArticleRetentionDays.HasValue && f.ArticleRetentionDays > 0)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFeedsWithRetentionAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds with retention settings");
                throw;
            }
        }

        #endregion

        #region Bulk / Direct Update Operations

        /// <inheritdoc />
        public async Task<int> DeleteFeedDirectAsync(int feedId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Information("Directly deleted feed ID {FeedId}", feedId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteFeedDirectAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error directly deleting feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateLastUpdatedAsync(int feedId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.LastUpdated, DateTime.UtcNow), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Updated LastUpdated for feed {FeedId}", feedId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateLastUpdatedAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating LastUpdated for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateNextUpdateScheduleAsync(int feedId, DateTime nextUpdate, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.NextUpdateSchedule, nextUpdate), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Updated NextUpdateSchedule for feed {FeedId} to {NextUpdate}", feedId, nextUpdate);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateNextUpdateScheduleAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating NextUpdateSchedule for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateArticleCountsAsync(int feedId, int totalCount, int unreadCount, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);
            ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
            ArgumentOutOfRangeException.ThrowIfNegative(unreadCount);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.TotalArticleCount, totalCount)
                        .SetProperty(f => f.UnreadCount, unreadCount), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Updated article counts for feed {FeedId}: Total={TotalCount}, Unread={UnreadCount}",
                        feedId, totalCount, unreadCount);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateArticleCountsAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating article counts for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> SetActiveStatusAsync(int feedId, bool isActive, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.IsActive, isActive), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Information("Set active status for feed {FeedId} to {IsActive}", feedId, isActive);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SetActiveStatusAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting active status for feed {FeedId}", feedId);
                throw;
            }
        }

        #endregion

        #region Health and Status Operations

        /// <inheritdoc />
        public async Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxFailureCount);

            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(f => f.FailureCount > maxFailureCount)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetFailedFeedsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving failed feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetHealthyFeedsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(f => f.FailureCount == 0)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetHealthyFeedsAsync cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving healthy feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> IncrementFailureCountAsync(int feedId, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.FailureCount, f => f.FailureCount + 1)
                        .SetProperty(f => f.LastError, errorMessage)
                        .SetProperty(f => f.LastUpdated, DateTime.UtcNow), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Incremented failure count for feed {FeedId}", feedId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("IncrementFailureCountAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error incrementing failure count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> ResetFailureCountAsync(int feedId, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.FailureCount, 0)
                        .SetProperty(f => f.LastError, (string?)null), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Reset failure count for feed {FeedId}", feedId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ResetFailureCountAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error resetting failure count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateHealthStatusAsync(int feedId, DateTime? lastUpdated, int failureCount, string? lastError, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);
            ArgumentOutOfRangeException.ThrowIfNegative(failureCount);

            try
            {
                var result = await _dbSet
                    .Where(f => f.Id == feedId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(f => f.LastUpdated, lastUpdated)
                        .SetProperty(f => f.FailureCount, failureCount)
                        .SetProperty(f => f.LastError, lastError), cancellationToken)
                    .ConfigureAwait(false);

                if (result > 0)
                    _logger.Debug("Updated health status for feed {FeedId}", feedId);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateHealthStatusAsync cancelled for feed {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating health status for feed {FeedId}", feedId);
                throw;
            }
        }

        #endregion

        #region Search Operations

        /// <inheritdoc />
        public async Task<List<Feed>> SearchAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

            try
            {
                var query = _dbSet
                    .AsNoTracking()
                    .Where(f => EF.Functions.Like(f.Title, $"%{searchText}%") ||
                                (f.Description != null && EF.Functions.Like(f.Description, $"%{searchText}%")) ||
                                EF.Functions.Like(f.Url, $"%{searchText}%"));

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SearchAsync cancelled for: '{SearchText}'", searchText);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: '{SearchText}'", searchText);
                throw;
            }
        }

        #endregion

        // TODO (High - v2.0): Interface Segregation Principle (ISP) Violation
        // The IFeedRepository interface is currently very large and encompasses many distinct concerns.
        // Proposed refactoring:
        // - IFeedReadRepository: For all read-only queries
        // - IFeedWriteRepository: For basic CRUD
        // - IFeedHealthRepository: For health-related updates
        // - IFeedMaintenanceRepository: For specific bulk updates
        // - IFeedAnalyticsRepository: For aggregation/reporting
    }
}