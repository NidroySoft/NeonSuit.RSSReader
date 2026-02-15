using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Professional repository implementation for RSS Feed data access.
    /// Provides optimized queries with comprehensive logging and support for active/inactive filtering.
    /// </summary>
    public class FeedRepository : BaseRepository<Feed>, IFeedRepository
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedRepository"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        /// <param name="logger">The logger instance for diagnostic tracking.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public FeedRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<FeedRepository>();
        }

        #region CRUD Operations

        /// <inheritdoc />
        public async Task<Feed?> GetByIdAsync(int id, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feed by ID: {FeedId} (IncludeInactive: {IncludeInactive})", id, includeInactive);

                var query = _dbSet.AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.FirstOrDefaultAsync(f => f.Id == id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID: {FeedId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public new async Task<Feed?> GetByIdNoTrackingAsync(int id)
        {
            try
            {
                _logger.Debug("Retrieving feed by ID without tracking: {FeedId}", id);
                return await _context.Feeds
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID without tracking: {FeedId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetAllAsync(bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving all feeds (IncludeInactive: {IncludeInactive})", includeInactive);

                var query = _dbSet.AsNoTracking();

                if (includeInactive)
                {
                    query = query.IgnoreQueryFilters();
                }

                var feeds = await query.ToListAsync();
                _logger.Information("Retrieved {Count} feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetWhereAsync(Func<Feed, bool> predicate)
        {
            try
            {
                _logger.Debug("Retrieving feeds with custom predicate");
                var allFeeds = await _dbSet.AsNoTracking().ToListAsync();
                var result = allFeeds.Where(predicate).ToList();
                _logger.Debug("Retrieved {Count} feeds matching predicate", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GetWhereAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CountWhereAsync(Func<Feed, bool> predicate)
        {
            try
            {
                _logger.Debug("Counting feeds with custom predicate");
                var allFeeds = await _dbSet.AsNoTracking().ToListAsync();
                var count = allFeeds.Count(predicate);
                _logger.Debug("Counted {Count} feeds matching predicate", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in CountWhereAsync");
                throw;
            }
        }

        /// <inheritdoc />
        public new async Task DetachEntityAsync(int id)
        {
            try
            {
                _logger.Debug("Detaching feed with ID: {FeedId} from change tracker", id);

                var tracked = _context.ChangeTracker.Entries<Feed>()
                    .FirstOrDefault(e => e.Entity.Id == id);

                if (tracked != null)
                {
                    tracked.State = EntityState.Detached;
                    _logger.Debug("Successfully detached feed ID: {FeedId}", id);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error detaching feed with ID: {FeedId}", id);
                throw;
            }
        }

        #endregion

        #region Feed-specific Operations

        /// <inheritdoc />
        public async Task<List<Feed>> GetByCategoryAsync(int categoryId, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feeds for category {CategoryId} (IncludeInactive: {IncludeInactive})",
                    categoryId, includeInactive);

                var query = _dbSet
                    .Where(f => f.CategoryId == categoryId)
                    .AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                var feeds = await query.ToListAsync();
                _logger.Debug("Retrieved {Count} feeds for category {CategoryId}", feeds.Count, categoryId);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetUncategorizedAsync(bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving uncategorized feeds (IncludeInactive: {IncludeInactive})", includeInactive);

                var query = _dbSet
                    .Where(f => f.CategoryId == null)
                    .AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                var feeds = await query.ToListAsync();
                _logger.Debug("Retrieved {Count} uncategorized feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving uncategorized feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetActiveAsync()
        {
            try
            {
                _logger.Debug("Retrieving active feeds");
                var feeds = await _dbSet
                    .Where(f => f.IsActive)
                    .AsNoTracking()
                    .ToListAsync();
                _logger.Debug("Retrieved {Count} active feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving active feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetInactiveAsync()
        {
            try
            {
                _logger.Debug("Retrieving inactive feeds");
                var feeds = await _dbSet
                    .Where(f => !f.IsActive)
                    .AsNoTracking()
                    .ToListAsync();
                _logger.Debug("Retrieved {Count} inactive feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving inactive feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Feed?> GetByUrlAsync(string url, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feed by URL: {Url} (IncludeInactive: {IncludeInactive})", url, includeInactive);

                var query = _dbSet.AsNoTracking();

                if (includeInactive)
                {
                    query = query.IgnoreQueryFilters();
                }

                var feed = await query.FirstOrDefaultAsync(f => f.Url == url);

                if (feed == null)
                {
                    var allUrls = await _dbSet.Select(f => f.Url).ToListAsync();
                    _logger.Debug("Feed with URL {Url} not found", url);
                }
                else
                { 
                    _logger.Debug("Found feed with URL {Url}: {Title}", url, feed.Title);
                }
                  

                return feed;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by URL: {Url}", url);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsByUrlAsync(string url)
        {
            try
            {
                _logger.Debug("Checking existence of feed by URL: {Url}", url);
                var exists = await _dbSet
                    .AsNoTracking()
                    .AnyAsync(f => f.Url == url);
                _logger.Debug("Feed with URL {Url} exists: {Exists}", url, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking feed existence by URL: {Url}", url);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsToUpdateAsync()
        {
            try
            {
                _logger.Debug("Retrieving feeds ready for update");
                var now = DateTime.UtcNow;

                var activeFeeds = await _dbSet
                    .Where(f => f.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                var feedsToUpdate = activeFeeds.Where(f =>
                {
                    if (!f.LastUpdated.HasValue)
                        return true;

                    var minutesSinceUpdate = (now - f.LastUpdated.Value).TotalMinutes;
                    return minutesSinceUpdate >= (int)f.UpdateFrequency;
                }).ToList();

                _logger.Debug("Found {Count} feeds ready for update", feedsToUpdate.Count);
                return feedsToUpdate;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds ready for update");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateLastUpdatedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Updating LastUpdated for feed {FeedId}", feedId);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousUpdate = feed.LastUpdated;
                    feed.LastUpdated = DateTime.UtcNow;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Updated LastUpdated for feed {FeedId} from {Previous} to {Current}",
                        feedId, previousUpdate, feed.LastUpdated);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for LastUpdated update", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating LastUpdated for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateNextUpdateScheduleAsync(int feedId, DateTime nextUpdate)
        {
            try
            {
                _logger.Debug("Updating NextUpdateSchedule for feed {FeedId} to {NextUpdate}", feedId, nextUpdate);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousSchedule = feed.NextUpdateSchedule;
                    feed.NextUpdateSchedule = nextUpdate;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Updated NextUpdateSchedule for feed {FeedId} from {Previous} to {Current}",
                        feedId, previousSchedule, nextUpdate);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for NextUpdateSchedule update", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating NextUpdateSchedule for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteFeedDirectAsync(int feedId)
        {
            try
            {
                _logger.Debug("Directly deleting feed with ID: {FeedId}", feedId);

                var sql = "DELETE FROM Feeds WHERE Id = @p0";
                var result = await _context.ExecuteSqlCommandAsync(sql, cancellationToken: default, feedId);

                _logger.Debug("DeleteFeedDirectAsync affected {Count} rows for ID {FeedId}", result, feedId);

                if (result == 0)
                {
                    _logger.Warning("No feed found with ID {FeedId} to delete", feedId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error directly deleting feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetCountByCategoryAsync()
        {
            try
            {
                _logger.Debug("Retrieving feed counts by category");

                var result = await _dbSet
                    .Where(f => f.CategoryId.HasValue)
                    .GroupBy(f => f.CategoryId!.Value)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .AsNoTracking()
                    .ToListAsync();

                var dictionary = result.ToDictionary(x => x.CategoryId, x => x.Count);
                _logger.Debug("Retrieved feed counts for {Count} categories", dictionary.Count);
                return dictionary;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed counts by category");
                throw;
            }
        }

        #endregion

        #region Health and Status Methods

        /// <inheritdoc />
        public async Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3)
        {
            try
            {
                _logger.Debug("Retrieving failed feeds with failure count > {MaxFailureCount}", maxFailureCount);

                var feeds = await _dbSet
                    .Where(f => f.FailureCount > maxFailureCount)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} failed feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving failed feeds with threshold {MaxFailureCount}", maxFailureCount);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetHealthyFeedsAsync()
        {
            try
            {
                _logger.Debug("Retrieving healthy feeds (failure count = 0)");

                var feeds = await _dbSet
                    .Where(f => f.FailureCount == 0)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} healthy feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving healthy feeds");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> IncrementFailureCountAsync(int feedId, string? errorMessage = null)
        {
            try
            {
                _logger.Debug("Incrementing failure count for feed {FeedId}", feedId);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousCount = feed.FailureCount;
                    feed.FailureCount++;
                    feed.LastError = errorMessage;
                    feed.LastUpdated = DateTime.UtcNow;
                    var result = await UpdateAsync(feed);

                    _logger.Warning("Incremented failure count for feed {FeedId} from {Previous} to {Current}. Error: {Error}",
                        feedId, previousCount, feed.FailureCount, errorMessage ?? "No error message");
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for failure count increment", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error incrementing failure count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> ResetFailureCountAsync(int feedId)
        {
            try
            {
                _logger.Debug("Resetting failure count for feed {FeedId}", feedId);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousCount = feed.FailureCount;
                    feed.FailureCount = 0;
                    feed.LastError = null;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Reset failure count for feed {FeedId} from {Previous} to 0", feedId, previousCount);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for failure count reset", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error resetting failure count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateHealthStatusAsync(int feedId, DateTime? lastUpdated, int failureCount, string? lastError)
        {
            try
            {
                _logger.Debug("Updating health status for feed {FeedId}", feedId);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    feed.LastUpdated = lastUpdated;
                    feed.FailureCount = failureCount;
                    feed.LastError = lastError;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Updated health status for feed {FeedId}", feedId);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for health status update", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating health status for feed {FeedId}", feedId);
                throw;
            }
        }

        #endregion

        #region Retention and Cleanup

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsWithRetentionAsync()
        {
            try
            {
                _logger.Debug("Retrieving feeds with retention settings");

                var feeds = await _dbSet
                    .Where(f => f.ArticleRetentionDays.HasValue && f.ArticleRetentionDays > 0)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} feeds with retention settings", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds with retention settings");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateArticleCountsAsync(int feedId, int totalCount, int unreadCount)
        {
            try
            {
                _logger.Debug("Updating article counts for feed {FeedId}: Total={TotalCount}, Unread={UnreadCount}",
                    feedId, totalCount, unreadCount);

                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousTotal = feed.TotalArticleCount;
                    feed.TotalArticleCount = totalCount;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Updated article counts for feed {FeedId}: Total {Previous}->{Current}",
                        feedId, previousTotal, totalCount);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for article counts update", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating article counts for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> SetActiveStatusAsync(int feedId, bool isActive)
        {
            try
            {
                _logger.Debug("Setting active status for feed {FeedId} to {IsActive}", feedId, isActive);
                var feed = await GetByIdAsync(feedId);
                if (feed != null)
                {
                    var previousStatus = feed.IsActive;
                    feed.IsActive = isActive;
                    var result = await UpdateAsync(feed);

                    _logger.Information("Changed active status for feed {FeedId}: {Previous} -> {Current}",
                        feedId, previousStatus, isActive);
                    return result;
                }

                _logger.Warning("Feed {FeedId} not found for active status update", feedId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting active status for feed {FeedId}", feedId);
                throw;
            }
        }

        #endregion

        #region Search and Filtering

        /// <inheritdoc />
        public async Task<List<Feed>> SearchAsync(string searchText, bool includeInactive = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Feed>();
                }

                _logger.Debug("Searching feeds for: {SearchText} (IncludeInactive: {IncludeInactive})",
                    searchText, includeInactive);

                var query = _dbSet
                    .Where(f => EF.Functions.Like(f.Title, $"%{searchText}%") ||
                                (f.Description != null && EF.Functions.Like(f.Description, $"%{searchText}%")) ||
                                EF.Functions.Like(f.Url, $"%{searchText}%"))
                    .AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                var feeds = await query.ToListAsync();
                _logger.Debug("Found {Count} feeds for search: {SearchText}", feeds.Count, searchText);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: {SearchText}", searchText);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync(bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feeds grouped by category (IncludeInactive: {IncludeInactive})", includeInactive);

                var query = _dbSet
                    .Where(f => f.CategoryId.HasValue)
                    .AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                var feeds = await query.ToListAsync();

                return feeds
                    .GroupBy(f => f.CategoryId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feeds grouped by category");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feeds for category {CategoryId} (IncludeInactive: {IncludeInactive})",
                    categoryId, includeInactive);

                var query = _dbSet
                    .Where(f => f.CategoryId == categoryId)
                    .OrderBy(f => f.Title)
                    .AsNoTracking();

                if (!includeInactive)
                    query = query.Where(f => f.IsActive);

                return await query.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feeds for category: {CategoryId}", categoryId);
                throw;
            }
        }

        #endregion
    }
}