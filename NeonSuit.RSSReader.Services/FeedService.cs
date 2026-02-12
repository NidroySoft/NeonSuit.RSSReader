using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using NeonSuit.RSSReader.Services.FeedParser;
using Serilog;
using System.Net.Sockets;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional Feed Service.
    /// Handles RSS synchronization and feed management with optimized batch inserts.
    /// </summary>
    public class FeedService : IFeedService
    {
        private readonly IFeedRepository _feedRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IFeedParser _feedParser;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the FeedService class.
        /// </summary>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="articleRepository">The article repository.</param>
        /// <param name="feedParser">The RSS feed parser.</param>
        public FeedService(
            IFeedRepository feedRepository,
            IArticleRepository articleRepository,
            IFeedParser feedParser,
            ILogger logger)
        {
            _feedRepository = feedRepository ?? throw new ArgumentNullException(nameof(feedRepository));
            _articleRepository = articleRepository ?? throw new ArgumentNullException(nameof(articleRepository));
            _feedParser = feedParser ?? throw new ArgumentNullException(nameof(feedParser));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<FeedService>();
        }

        /// <summary>
        /// Retrieves all feeds from the database.
        /// </summary>
        /// <returns>A list of all feeds.</returns>
        public async Task<List<Feed>> GetAllFeedsAsync()
        {
            try
            {
                _logger.Debug("Getting all feeds");
                var feeds = await _feedRepository.GetAllAsync();
                _logger.Information("Retrieved {Count} feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all feeds");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a feed by its identifier.
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <returns>The feed or null if not found.</returns>
        public async Task<Feed?> GetFeedByIdAsync(int id)
        {
            try
            {
                _logger.Debug("Getting feed by ID: {FeedId}", id);
                var feed = await _feedRepository.GetByIdAsync(id);

                if (feed == null)
                    _logger.Debug("Feed {FeedId} not found", id);
                else
                    _logger.Debug("Found feed {FeedId}: {Title}", id, feed.Title);

                return feed;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting feed by ID: {FeedId}", id);
                throw;
            }
        }

        /// <summary>
        /// Adds a new feed to the system with comprehensive network error handling.
        /// </summary>  /// <summary>
        /// Adds a new feed to the system.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <param name="categoryId">Optional category identifier.</param>
        /// <returns>The newly created feed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when feed already exists or cannot be parsed.</exception>
        public async Task<Feed> AddFeedAsync(string url, int? categoryId = null)
        {
            try
            {
                _logger.Debug("Adding new feed: {Url}", url);

                if (await _feedRepository.ExistsByUrlAsync(url))
                {
                    _logger.Warning("Feed already exists: {Url}", url);
                    throw new InvalidOperationException("This feed is already in your list.");
                }

                Feed? feed = null;
                List<Article>? articles = null;

                try
                {
                    (feed, articles) = await _feedParser.ParseFeedAsync(url);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Network error while fetching feed: {Url}. Status: {StatusCode}",
                        url, ex.StatusCode);
                    throw new InvalidOperationException(
                        $"Could not reach the feed server. Please check your internet connection and the URL. " +
                        $"Status: {ex.StatusCode}", ex);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.Error(ex, "Request timeout while fetching feed: {Url}", url);
                    throw new InvalidOperationException(
                        "The feed server took too long to respond. Please try again later.", ex);
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Socket error while fetching feed: {Url}", url);
                    throw new InvalidOperationException(
                        "Network connection issue. Please check your internet connection.", ex);
                }

                if (feed == null)
                {
                    _logger.Error("Failed to parse feed (null returned): {Url}", url);
                    throw new InvalidOperationException("Could not read the feed. Please check the URL.");
                }

                feed.IconUrl ??= string.Empty;
                feed.Title ??= "Untitled Feed";
                feed.CategoryId = categoryId;
                feed.CreatedAt = DateTime.UtcNow;
                feed.IsActive = true;
                feed.LastUpdated = DateTime.UtcNow;
                feed.NextUpdateSchedule = DateTime.UtcNow.AddMinutes((int)FeedUpdateFrequency.EveryHour);

                await _feedRepository.InsertAsync(feed);
                _logger.Information("Feed added successfully: {Title} ({Url})", feed.Title, url);

                if (articles != null && articles.Any())
                {
                    articles.ForEach(a => a.FeedId = feed.Id);
                    var insertedCount = await _articleRepository.InsertAllAsync(articles);
                    await UpdateFeedCountsAsync(feed.Id);
                    _logger.Information("Inserted {Count} initial articles for feed {FeedId}", insertedCount, feed.Id);
                }

                return feed;
            }
            catch (Exception ex) when (ex is not InvalidOperationException and not ArgumentNullException)
            {
                _logger.Error(ex, "Unexpected error adding feed: {Url}", url);
                throw new InvalidOperationException($"Failed to add feed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Refreshes a specific feed by downloading and parsing new articles.
        /// Comprehensive network error handling with failure count management.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>True if refresh succeeded, otherwise false.</returns>
        public async Task<bool> RefreshFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Refreshing feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId);

                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for refresh", feedId);
                    return false;
                }

                List<Article> newArticles;
                try
                {
                    newArticles = await _feedParser.ParseArticlesAsync(feed.Url, feedId);

                    // Success - reset failure count
                    await _feedRepository.ResetFailureCountAsync(feedId);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Network error refreshing feed {FeedId}: {Url}. Status: {StatusCode}",
                        feedId, feed.Url, ex.StatusCode);
                    await _feedRepository.IncrementFailureCountAsync(feedId, $"HTTP {ex.StatusCode}: {ex.Message}");
                    return false;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.Error(ex, "Timeout refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, "Timeout: Server took too long to respond");
                    return false;
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Socket error refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, $"Network error: {ex.SocketErrorCode}");
                    return false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.Error(ex, "Parse error refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, $"Parse error: {ex.Message}");
                    return false;
                }

                var addedCount = 0;
                foreach (var article in newArticles)
                {
                    if (!await _articleRepository.ExistsByGuidAsync(article.Guid))
                    {
                        await _articleRepository.InsertAsync(article);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    await UpdateFeedCountsAsync(feedId);
                    _logger.Information("Added {Count} new articles to feed {FeedId}", addedCount, feedId);
                }
                else
                {
                    _logger.Debug("No new articles found for feed {FeedId}", feedId);
                }

                await _feedRepository.UpdateLastUpdatedAsync(feedId);
                _logger.Information("Feed {FeedId} refreshed successfully", feedId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error refreshing feed {FeedId}", feedId);
                return false;
            }
        }

        /// <summary>
        /// Refreshes all active feeds that are due for update.
        /// </summary>
        /// <returns>The number of feeds successfully refreshed.</returns>
        public async Task<int> RefreshAllFeedsAsync()
        {
            try
            {
                _logger.Debug("Refreshing all feeds");
                var feedsToUpdate = await _feedRepository.GetFeedsToUpdateAsync();
                int updatedCount = 0;

                _logger.Information("Found {Count} feeds ready for update", feedsToUpdate.Count);

                foreach (var feed in feedsToUpdate)
                {
                    if (await RefreshFeedAsync(feed.Id))
                        updatedCount++;
                }

                _logger.Information("Successfully refreshed {UpdatedCount} out of {TotalCount} feeds",
                    updatedCount, feedsToUpdate.Count);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error refreshing all feeds");
                throw;
            }
        }

        /// <summary>
        /// Refreshes feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The number of feeds successfully refreshed.</returns>
        public async Task<int> RefreshFeedsByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Refreshing feeds for category {CategoryId}", categoryId);
                var feeds = await _feedRepository.GetByCategoryAsync(categoryId);
                int updatedCount = 0;

                foreach (var feed in feeds.Where(f => f.IsActive))
                {
                    if (await RefreshFeedAsync(feed.Id))
                        updatedCount++;
                }

                _logger.Information("Refreshed {UpdatedCount} feeds for category {CategoryId}", updatedCount, categoryId);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error refreshing feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing feed.
        /// </summary>
        /// <param name="feed">The feed to update.</param>
        /// <returns>True if the update succeeded, otherwise false.</returns>
        public async Task<bool> UpdateFeedAsync(Feed feed)
        {
            try
            {
                _logger.Debug("Updating feed {FeedId}: {Title}", feed.Id, feed.Title);
                var result = await _feedRepository.UpdateAsync(feed);

                if (result > 0)
                {
                    _logger.Information("Feed {FeedId} updated successfully", feed.Id);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found for update", feed.Id);
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating feed {FeedId}", feed.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a feed and all its associated articles.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>True if the deletion succeeded, otherwise false.</returns>
        public async Task<bool> DeleteFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Deleting feed {FeedId} and its articles", feedId);
                await _articleRepository.DeleteByFeedAsync(feedId);
                var result = await _feedRepository.DeleteAsync(new Feed { Id = feedId }) > 0;

                if (result)
                {
                    _logger.Information("Feed {FeedId} and its articles deleted successfully", feedId);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found for deletion", feedId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Checks if a feed with the specified URL already exists.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <returns>True if the feed exists, otherwise false.</returns>
        public async Task<bool> FeedExistsAsync(string url)
        {
            try
            {
                _logger.Debug("Checking if feed exists: {Url}", url);
                var exists = await _feedRepository.ExistsByUrlAsync(url);
                _logger.Debug("Feed {Url} exists: {Exists}", url, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if feed exists: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Gets unread article counts grouped by feed.
        /// </summary>
        /// <returns>A dictionary mapping feed IDs to unread counts.</returns>
        public async Task<Dictionary<int, int>> GetUnreadCountsAsync()
        {
            try
            {
                _logger.Debug("Getting unread counts by feed");
                var counts = await _articleRepository.GetUnreadCountsByFeedAsync();
                _logger.Debug("Retrieved unread counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread counts by feed");
                throw;
            }
        }

        /// <summary>
        /// Gets total article counts grouped by feed.
        /// </summary>
        /// <returns>A dictionary mapping feed IDs to total article counts.</returns>
        public async Task<Dictionary<int, int>> GetArticleCountsAsync()
        {
            try
            {
                _logger.Debug("Getting article counts by feed");
                var feeds = await _feedRepository.GetAllAsync();
                var counts = feeds.ToDictionary(f => f.Id, f => f.TotalArticleCount);
                _logger.Debug("Retrieved article counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting article counts by feed");
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
                _logger.Debug("Getting failed feeds with threshold {MaxFailureCount}", maxFailureCount);
                var feeds = await _feedRepository.GetFailedFeedsAsync(maxFailureCount);
                _logger.Information("Found {Count} failed feeds", feeds.Count);
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
                _logger.Debug("Getting healthy feeds");
                var feeds = await _feedRepository.GetHealthyFeedsAsync();
                _logger.Information("Found {Count} healthy feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting healthy feeds");
                throw;
            }
        }

        /// <summary>
        /// Resets failure count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> ResetFeedFailuresAsync(int feedId)
        {
            try
            {
                _logger.Debug("Resetting failure count for feed {FeedId}", feedId);
                var result = await _feedRepository.ResetFailureCountAsync(feedId) > 0;

                if (result)
                {
                    _logger.Information("Reset failure count for feed {FeedId}", feedId);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found for failure reset", feedId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error resetting failure count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Gets feed health statistics.
        /// </summary>
        /// <returns>A dictionary mapping health status to count of feeds.</returns>
        public async Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync()
        {
            try
            {
                _logger.Debug("Getting feed health statistics");
                var feeds = await _feedRepository.GetAllAsync();
                var stats = new Dictionary<FeedHealthStatus, int>
                {
                    { FeedHealthStatus.Healthy, 0 },
                    { FeedHealthStatus.Warning, 0 },
                    { FeedHealthStatus.Error, 0 }
                };

                foreach (var feed in feeds)
                {
                    stats[feed.HealthStatus]++;
                }

                _logger.Information("Feed health stats: Healthy={Healthy}, Warning={Warning}, Error={Error}",
                    stats[FeedHealthStatus.Healthy], stats[FeedHealthStatus.Warning], stats[FeedHealthStatus.Error]);
                return stats;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting feed health statistics");
                throw;
            }
        }

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <returns>A list of matching feeds.</returns>
        public async Task<List<Feed>> SearchFeedsAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Feed>();
                }

                _logger.Debug("Searching feeds for: {SearchText}", searchText);
                var feeds = await _feedRepository.SearchAsync(searchText);
                _logger.Information("Found {Count} feeds for search: {SearchText}", feeds.Count, searchText);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: {SearchText}", searchText);
                throw;
            }
        }

        /// <summary>
        /// Retrieves feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>A list of feeds in the category.</returns>
        public async Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Getting feeds for category {CategoryId}", categoryId);
                var feeds = await _feedRepository.GetByCategoryAsync(categoryId);
                _logger.Debug("Found {Count} feeds for category {CategoryId}", feeds.Count, categoryId);
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
        public async Task<List<Feed>> GetUncategorizedFeedsAsync()
        {
            try
            {
                _logger.Debug("Getting uncategorized feeds");
                var feeds = await _feedRepository.GetUncategorizedAsync();
                _logger.Debug("Found {Count} uncategorized feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting uncategorized feeds");
                throw;
            }
        }

       

        /// <summary>
        /// Gets the total article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The total article count.</returns>
        public async Task<int> GetTotalArticleCountAsync(int feedId)
        {
            try
            {
                _logger.Debug("Getting total article count for feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId);
                var count = feed?.TotalArticleCount ?? 0;
                _logger.Debug("Total articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting total article count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Gets the unread article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The unread article count.</returns>
        public async Task<int> GetUnreadCountByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Getting unread count for feed {FeedId}", feedId);
                var count = await _articleRepository.GetUnreadCountByFeedAsync(feedId);
                _logger.Debug("Unread articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Updates specific properties of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="title">Optional new title.</param>
        /// <param name="description">Optional new description.</param>
        /// <param name="websiteUrl">Optional new website URL.</param>
        /// <returns>True if the update succeeded, otherwise false.</returns>
        public async Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null)
        {
            try
            {
                _logger.Debug("Updating properties for feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId);

                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for property update", feedId);
                    return false;
                }

                if (title != null) feed.Title = title;
                if (description != null) feed.Description = description;
                if (websiteUrl != null) feed.WebsiteUrl = websiteUrl;

                var result = await _feedRepository.UpdateAsync(feed) > 0;

                if (result)
                {
                    _logger.Information("Updated properties for feed {FeedId}", feedId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating properties for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">The new active status.</param>
        /// <returns>True if the update succeeded, otherwise false.</returns>
        public async Task<bool> SetFeedActiveStatusAsync(int feedId, bool isActive)
        {
            try
            {
                _logger.Debug("Setting active status for feed {FeedId} to {IsActive}", feedId, isActive);
                var result = await _feedRepository.SetActiveStatusAsync(feedId, isActive) > 0;

                if (result)
                {
                    _logger.Information("Set active status for feed {FeedId} to {IsActive}", feedId, isActive);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found for active status update", feedId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting active status for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Updates the category of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="categoryId">The new category identifier (null for uncategorized).</param>
        /// <returns>True if the update succeeded, otherwise false.</returns>
        public async Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId)
        {
            try
            {
                _logger.Debug("Updating category for feed {FeedId} to {CategoryId}", feedId, categoryId);
                var feed = await _feedRepository.GetByIdAsync(feedId);

                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for category update", feedId);
                    return false;
                }

                feed.CategoryId = categoryId;
                var result = await _feedRepository.UpdateAsync(feed) > 0;

                if (result)
                {
                    _logger.Information("Updated category for feed {FeedId} to {CategoryId}", feedId, categoryId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating category for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Updates the article retention days for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="retentionDays">The new retention days (null for default).</param>
        /// <returns>True if the update succeeded, otherwise false.</returns>
        public async Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays)
        {
            try
            {
                _logger.Debug("Updating retention days for feed {FeedId} to {RetentionDays}", feedId, retentionDays);
                var feed = await _feedRepository.GetByIdAsync(feedId);

                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for retention update", feedId);
                    return false;
                }

                feed.ArticleRetentionDays = retentionDays;
                var result = await _feedRepository.UpdateAsync(feed) > 0;

                if (result)
                {
                    _logger.Information("Updated retention days for feed {FeedId} to {RetentionDays}", feedId, retentionDays);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating retention days for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Cleans up old articles based on feed retention settings.
        /// </summary>
        /// <returns>The total number of articles deleted.</returns>
        public async Task<int> CleanupOldArticlesAsync()
        {
            try
            {
                _logger.Debug("Cleaning up old articles based on retention settings");
                var feeds = await _feedRepository.GetFeedsWithRetentionAsync();
                var totalDeleted = 0;

                _logger.Information("Found {Count} feeds with retention settings", feeds.Count);

                foreach (var feed in feeds)
                {
                    if (feed.ArticleRetentionDays.HasValue)
                    {
                        var cutoffDate = DateTime.UtcNow.AddDays(-feed.ArticleRetentionDays.Value);
                        var deleted = await _articleRepository.DeleteOlderThanAsync(cutoffDate);
                        totalDeleted += deleted;

                        if (deleted > 0)
                        {
                            _logger.Information("Deleted {DeletedCount} old articles for feed {FeedId} (retention: {RetentionDays} days)",
                                deleted, feed.Id, feed.ArticleRetentionDays.Value);
                        }
                    }
                }

                _logger.Information("Total articles cleaned up: {TotalDeleted}", totalDeleted);
                return totalDeleted;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error cleaning up old articles");
                throw;
            }
        }

        /// <summary>
        /// Updates article counts for all feeds.
        /// </summary>
        /// <returns>The number of feeds updated.</returns>
        public async Task<int> UpdateAllFeedCountsAsync()
        {
            try
            {
                _logger.Debug("Updating article counts for all feeds");
                var feeds = await _feedRepository.GetAllAsync();
                var updatedCount = 0;

                foreach (var feed in feeds)
                {
                    await UpdateFeedCountsAsync(feed.Id);
                    updatedCount++;
                }

                _logger.Information("Updated article counts for {Count} feeds", updatedCount);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating article counts for all feeds");
                throw;
            }
        }

        /// <summary>
        /// Updates article counts for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        private async Task UpdateFeedCountsAsync(int feedId)
        {
            try
            {
                var totalCount = await _articleRepository.CountWhereAsync(a => a.FeedId == feedId);
                var unreadCount = await _articleRepository.GetUnreadCountByFeedAsync(feedId);

                await _feedRepository.UpdateArticleCountsAsync(feedId, totalCount, unreadCount);
                _logger.Debug("Updated counts for feed {FeedId}: Total={TotalCount}, Unread={UnreadCount}",
                    feedId, totalCount, unreadCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating counts for feed {FeedId}", feedId);
                throw;
            }
        }

        public async Task<Feed?> GetFeedByUrlAsync(string url)
        {
            try
            {
                // Usar el repositorio que ya tiene este método
                return await _feedRepository.GetByUrlAsync(url);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get feed by URL: {Url}", url);
                throw;
            }
        }

        public async Task<int> CreateFeedAsync(Feed feed)
        {
            try
            {
                // Validaciones básicas
                if (feed == null)
                    throw new ArgumentNullException(nameof(feed));

                if (string.IsNullOrWhiteSpace(feed.Url))
                    throw new ArgumentException("Feed URL cannot be empty");

                // Verificar si ya existe
                var existing = await GetFeedByUrlAsync(feed.Url);
                if (existing != null)
                    throw new InvalidOperationException($"Feed with URL '{feed.Url}' already exists");

                // Establecer valores por defecto si es necesario
                if (feed.CreatedAt == default)
                    feed.CreatedAt = DateTime.UtcNow;

                if (feed.LastUpdated == null)
                    feed.LastUpdated = DateTime.UtcNow;

                // Insertar en la base de datos
                return await _feedRepository.InsertAsync(feed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create feed: {Title}", feed?.Title);
                throw;
            }
        }
    }
}