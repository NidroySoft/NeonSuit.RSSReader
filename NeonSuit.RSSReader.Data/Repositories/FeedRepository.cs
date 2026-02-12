using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Professional repository for RSS Feeds.
    /// Optimized with server-side filtering and async lazy loading with comprehensive logging.
    /// </summary>
    public class FeedRepository : BaseRepository<Feed>, IFeedRepository
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the FeedRepository class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public FeedRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<FeedRepository>();
           
        }

        /// <summary>
        /// Gets feeds that match the specified predicate.
        /// </summary>
        /// <param name="predicate">The filter predicate.</param>
        /// <returns>A list of matching feeds.</returns>
        public async Task<List<Feed>> GetWhereAsync(Func<Feed, bool> predicate)
        {
            try
            {
                _logger.Debug("Getting feeds with predicate");
                // CHANGED: Use EF Core with client-side evaluation
                var allFeeds = await _dbSet.AsNoTracking().ToListAsync();
                var result = allFeeds.Where(predicate).ToList();
                _logger.Debug("Retrieved {Count} feeds with predicate", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GetWhereAsync");
                throw;
            }
        }

        /// <summary>
        /// Counts feeds that match the specified predicate.
        /// </summary>
        /// <param name="predicate">The filter predicate.</param>
        /// <returns>The count of matching feeds.</returns>
        public async Task<int> CountWhereAsync(Func<Feed, bool> predicate)
        {
            try
            {
                _logger.Debug("Counting feeds with predicate");
                // CHANGED: Use EF Core with client-side evaluation
                var allFeeds = await _dbSet.AsNoTracking().ToListAsync();
                var count = allFeeds.Count(predicate);
                _logger.Debug("Counted {Count} feeds with predicate", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in CountWhereAsync");
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds associated with a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>A list of feeds in the specified category.</returns>
        public async Task<List<Feed>> GetByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Getting feeds for category {CategoryId}", categoryId);
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.CategoryId == categoryId)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} feeds for category {CategoryId}", feeds.Count, categoryId);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds that are not assigned to any category.
        /// </summary>
        /// <returns>A list of uncategorized feeds.</returns>
        public async Task<List<Feed>> GetUncategorizedAsync()
        {
            try
            {
                _logger.Debug("Getting uncategorized feeds");
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.CategoryId == null)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} uncategorized feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting uncategorized feeds");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all active feeds.
        /// </summary>
        /// <returns>A list of active feeds.</returns>
        public async Task<List<Feed>> GetActiveAsync()
        {
            try
            {
                _logger.Debug("Getting active feeds");
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} active feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting active feeds");
                throw;
            }
        }

        /// <summary>
        /// Retrieves all inactive feeds.
        /// </summary>
        /// <returns>A list of inactive feeds.</returns>
        public async Task<List<Feed>> GetInactiveAsync()
        {
            try
            {
                _logger.Debug("Getting inactive feeds");
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => !f.IsActive)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} inactive feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting inactive feeds");
                throw;
            }
        }

        /// <summary>
        /// Finds a feed by its URL.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <returns>The feed or null if not found.</returns>
        public async Task<Feed?> GetByUrlAsync(string url)
        {
            try
            {
                _logger.Debug("Getting feed by URL: {Url}", url);
                // CHANGED: Use EF Core DbSet
                var feed = await _dbSet
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Url == url);

                if (feed == null)
                    _logger.Debug("Feed with URL {Url} not found", url);
                else
                    _logger.Debug("Found feed with URL {Url}: {Title}", url, feed.Title);

                return feed;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting feed by URL: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Checks if a feed with the specified URL already exists.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <returns>True if the feed exists, otherwise false.</returns>
        public async Task<bool> ExistsByUrlAsync(string url)
        {
            try
            {
                _logger.Debug("Checking if feed exists by URL: {Url}", url);
                // CHANGED: Use direct AnyAsync for better performance
                var exists = await _dbSet
                    .AsNoTracking()
                    .AnyAsync(f => f.Url == url);

                _logger.Debug("Feed with URL {Url} exists: {Exists}", url, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if feed exists by URL: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds that are due for an update based on their frequency.
        /// </summary>
        /// <returns>A list of feeds ready for update.</returns>
        public async Task<List<Feed>> GetFeedsToUpdateAsync()
        {
            try
            {
                _logger.Debug("Getting feeds ready for update");
                var now = DateTime.UtcNow;

                // CHANGED: Use EF Core with server-side filtering where possible
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
                _logger.Error(ex, "Error getting feeds ready for update");
                throw;
            }
        }

        /// <summary>
        /// Updates the LastUpdated timestamp for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Updates the NextUpdateSchedule for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="nextUpdate">The next update schedule time.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Gets the count of feeds grouped by category.
        /// </summary>
        /// <returns>A dictionary mapping category IDs to feed counts.</returns>
        public async Task<Dictionary<int, int>> GetCountByCategoryAsync()
        {
            try
            {
                _logger.Debug("Getting feed counts by category");
                // CHANGED: Use EF Core LINQ with GroupBy
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
                _logger.Error(ex, "Error getting feed counts by category");
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds with failure count above threshold.
        /// </summary>
        /// <param name="maxFailureCount">The maximum allowed failure count.</param>
        /// <returns>A list of failed feeds.</returns>
        public async Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3)
        {
            try
            {
                _logger.Debug("Getting failed feeds with failure count > {MaxFailureCount}", maxFailureCount);
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.FailureCount > maxFailureCount)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} failed feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting failed feeds with threshold {MaxFailureCount}", maxFailureCount);
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds with zero failures.
        /// </summary>
        /// <returns>A list of healthy feeds.</returns>
        public async Task<List<Feed>> GetHealthyFeedsAsync()
        {
            try
            {
                _logger.Debug("Getting healthy feeds (failure count = 0)");
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.FailureCount == 0)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} healthy feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting healthy feeds");
                throw;
            }
        }

        /// <summary>
        /// Increments failure count and records error message.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="errorMessage">The error message to record.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Resets failure count to zero.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Updates health status fields in one operation.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="lastUpdated">The last updated timestamp.</param>
        /// <param name="failureCount">The failure count.</param>
        /// <param name="lastError">The last error message.</param>
        /// <returns>The number of rows affected.</returns>
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

                    _logger.Information("Updated health status for feed {FeedId}",
                        feedId);
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

        /// <summary>
        /// Retrieves feeds with specific retention days.
        /// </summary>
        /// <returns>A list of feeds with retention settings.</returns>
        public async Task<List<Feed>> GetFeedsWithRetentionAsync()
        {
            try
            {
                _logger.Debug("Getting feeds with retention settings");
                // CHANGED: Use EF Core DbSet
                var feeds = await _dbSet
                    .Where(f => f.ArticleRetentionDays.HasValue && f.ArticleRetentionDays > 0)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} feeds with retention settings", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting feeds with retention settings");
                throw;
            }
        }

        /// <summary>
        /// Updates article counts for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="totalCount">The total article count.</param>
        /// <param name="unreadCount">The unread article count.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">The new active status.</param>
        /// <returns>The number of rows affected.</returns>
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

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <returns>A list of matching feeds.</returns>
        public async Task<List<Feed>> SearchAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Feed>();
                }

                _logger.Debug("Searching feeds for: {SearchText}", searchText);
                // CHANGED: Use EF Core DbSet with Contains for case-insensitive search
                var feeds = await _dbSet
                    .Where(f => EF.Functions.Like(f.Title, $"%{searchText}%") ||
                                (f.Description != null && EF.Functions.Like(f.Description, $"%{searchText}%")) ||
                                EF.Functions.Like(f.Url, $"%{searchText}%"))
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Found {Count} feeds for search: {SearchText}", feeds.Count, searchText);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: {SearchText}", searchText);
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds grouped by category.
        /// </summary>
        /// <returns>A dictionary mapping category IDs to lists of feeds.</returns>
        public async Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync()
        {
            try
            {
                // CHANGED: Use EF Core with server-side filtering and grouping
                var feeds = await _dbSet
                    .Where(f => f.CategoryId.HasValue)
                    .AsNoTracking()
                    .ToListAsync();

                return feeds
                    .GroupBy(f => f.CategoryId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get feeds grouped by category");
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds for a specific category, ordered by title.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>A list of feeds in the specified category.</returns>
        public async Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId)
        {
            try
            {
                // CHANGED: Use EF Core DbSet
                return await _dbSet
                    .Where(f => f.CategoryId == categoryId)
                    .OrderBy(f => f.Title)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get feeds for category: {CategoryId}", categoryId);
                throw;
            }
        }
    }
}