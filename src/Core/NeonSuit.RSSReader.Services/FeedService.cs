using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using NeonSuit.RSSReader.Services.RssFeedParser;
using Serilog;
using System.Net.Sockets;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional implementation of the feed service providing comprehensive RSS feed management.
    /// Handles feed CRUD operations, synchronization, health monitoring, and maintenance tasks
    /// with full support for active/inactive feed filtering.
    /// </summary>
    public class FeedService : IFeedService
    {
        private readonly IFeedRepository _feedRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IRssFeedParser _feedParser;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedService"/> class.
        /// </summary>
        /// <param name="feedRepository">The feed repository for data access operations.</param>
        /// <param name="articleRepository">The article repository for article management.</param>
        /// <param name="feedParser">The feed parser for RSS/Atom feed parsing.</param>
        /// <param name="logger">The logger instance for diagnostic tracking.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
        public FeedService(
            IFeedRepository feedRepository,
            IArticleRepository articleRepository,
            IRssFeedParser feedParser,
            ILogger logger)
        {
            _feedRepository = feedRepository ?? throw new ArgumentNullException(nameof(feedRepository));
            _articleRepository = articleRepository ?? throw new ArgumentNullException(nameof(articleRepository));
            _feedParser = feedParser ?? throw new ArgumentNullException(nameof(feedParser));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<FeedService>();
        }

        #region Basic CRUD Operations

        /// <inheritdoc />
        public async Task<List<Feed>> GetAllFeedsAsync(bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving all feeds (IncludeInactive: {IncludeInactive})", includeInactive);
                var feeds = await _feedRepository.GetAllAsync(includeInactive);
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
        public async Task<Feed?> GetFeedByIdAsync(int id, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feed by ID: {FeedId} (IncludeInactive: {IncludeInactive})", id, includeInactive);
                var feed = await _feedRepository.GetByIdAsync(id, includeInactive);

                if (feed == null)
                    _logger.Debug("Feed {FeedId} not found", id);
                else
                    _logger.Debug("Found feed {FeedId}: {Title}", id, feed.Title);

                return feed;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID: {FeedId}", id);
                throw;
            }
        }

        /// <inheritdoc />
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
                feed.Id = 0;

                await _feedRepository.InsertAsync(feed);
                _logger.Information("Feed added successfully: {Title} ({Url})", feed.Title, url);

                if (articles?.Any() == true)
                {
                    articles.ForEach(a =>
                    {
                        a.FeedId = feed.Id;
                        a.Id = 0;
                    });

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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<bool> DeleteFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Deleting feed {FeedId} and its articles", feedId);

                var articlesDeleted = await _articleRepository.DeleteByFeedAsync(feedId);
                _logger.Debug("Deleted {Count} articles for feed {FeedId}", articlesDeleted, feedId);

                var feedDeleted = await _feedRepository.DeleteFeedDirectAsync(feedId);
                _logger.Debug("Feed deletion returned: {Result}", feedDeleted);

                if (feedDeleted > 0)
                {
                    _logger.Information("Feed {FeedId} and {ArticleCount} articles deleted successfully",
                        feedId, articlesDeleted);
                    return true;
                }

                _logger.Warning("Feed {FeedId} not found for deletion", feedId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<Feed?> GetFeedByUrlAsync(string url, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feed by URL: {Url} (IncludeInactive: {IncludeInactive})", url, includeInactive);
                var result = await _feedRepository.GetByUrlAsync(url, includeInactive);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feed by URL: {Url}", url);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CreateFeedAsync(Feed feed)
        {
            if (feed == null)
                throw new ArgumentNullException(nameof(feed));

            if (string.IsNullOrWhiteSpace(feed.Url))
                throw new ArgumentException("Feed URL cannot be empty", nameof(feed));

            try
            {
                var existing = await GetFeedByUrlAsync(feed.Url, true);
                if (existing != null)
                    throw new InvalidOperationException($"Feed with URL '{feed.Url}' already exists");

                var newFeed = new Feed
                {
                    Title = feed.Title,
                    Url = feed.Url,
                    WebsiteUrl = feed.WebsiteUrl,
                    Description = feed.Description,
                    CategoryId = feed.CategoryId,
                    IsActive = feed.IsActive,
                    UpdateFrequency = feed.UpdateFrequency,
                    CreatedAt = feed.CreatedAt == default ? DateTime.UtcNow : feed.CreatedAt,
                    LastUpdated = feed.LastUpdated ?? DateTime.UtcNow,
                    IconUrl = feed.IconUrl,
                    Language = feed.Language,
                    ArticleRetentionDays = feed.ArticleRetentionDays,
                    FailureCount = 0,
                    TotalArticleCount = 0
                };

                var id = await _feedRepository.InsertAsync(newFeed);

                feed.Id = id;

                _logger.Information("Feed created successfully: {Title} (ID: {FeedId})", feed.Title, id);

                return id;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create feed: {Title}", feed.Title);
                throw;
            }
        }

        #endregion

        #region Feed Refresh and Synchronization

        /// <inheritdoc />
        public async Task<bool> RefreshFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Refreshing feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId, true); // Include inactive to refresh even inactive feeds

                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for refresh", feedId);
                    return false;
                }

                List<Article> newArticles;
                try
                {
                    newArticles = await _feedParser.ParseArticlesAsync(feed.Url, feedId);
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

        /// <inheritdoc />
        public async Task<int> RefreshAllFeedsAsync()
        {
            try
            {
                _logger.Debug("Refreshing all feeds");
                var feedsToUpdate = await _feedRepository.GetFeedsToUpdateAsync();
                var updatedCount = 0;

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

        /// <inheritdoc />
        public async Task<int> RefreshFeedsByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Refreshing feeds for category {CategoryId}", categoryId);
                var feeds = await _feedRepository.GetByCategoryAsync(categoryId, true); // Include inactive to refresh all
                var updatedCount = 0;

                foreach (var feed in feeds.Where(f => f.IsActive)) // Only refresh active feeds
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

        #endregion

        #region Feed Management

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId)
        {
            try
            {
                _logger.Debug("Updating category for feed {FeedId} to {CategoryId}", feedId, categoryId);
                var feed = await _feedRepository.GetByIdAsync(feedId, true); // Include inactive to update even inactive feeds

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

        /// <inheritdoc />
        public async Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays)
        {
            try
            {
                _logger.Debug("Updating retention days for feed {FeedId} to {RetentionDays}", feedId, retentionDays);
                var feed = await _feedRepository.GetByIdAsync(feedId, true); // Include inactive to update even inactive feeds

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

        #endregion

        #region Health and Monitoring

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetUnreadCountsAsync()
        {
            try
            {
                _logger.Debug("Retrieving unread counts by feed");
                var counts = await _articleRepository.GetUnreadCountsByFeedAsync();
                _logger.Debug("Retrieved unread counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving unread counts by feed");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetArticleCountsAsync()
        {
            try
            {
                _logger.Debug("Retrieving article counts by feed");
                var feeds = await _feedRepository.GetAllAsync(true); // Include inactive for complete stats
                var counts = feeds.ToDictionary(f => f.Id, f => f.TotalArticleCount);
                _logger.Debug("Retrieved article counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving article counts by feed");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3)
        {
            try
            {
                _logger.Debug("Retrieving failed feeds with threshold {MaxFailureCount}", maxFailureCount);
                var feeds = await _feedRepository.GetFailedFeedsAsync(maxFailureCount);
                _logger.Information("Found {Count} failed feeds", feeds.Count);
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
                _logger.Debug("Retrieving healthy feeds");
                var feeds = await _feedRepository.GetHealthyFeedsAsync();
                _logger.Information("Found {Count} healthy feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving healthy feeds");
                throw;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync()
        {
            try
            {
                _logger.Debug("Retrieving feed health statistics");
                var feeds = await _feedRepository.GetAllAsync(true); // Include inactive for complete stats
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
                _logger.Error(ex, "Error retrieving feed health statistics");
                throw;
            }
        }

        #endregion

        #region Search and Filtering

        /// <inheritdoc />
        public async Task<List<Feed>> SearchFeedsAsync(string searchText, bool includeInactive = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Feed>();
                }

                _logger.Debug("Searching feeds for: {SearchText} (IncludeInactive: {IncludeInactive})", searchText, includeInactive);
                var feeds = await _feedRepository.SearchAsync(searchText, includeInactive);
                _logger.Information("Found {Count} feeds for search: {SearchText}", feeds.Count, searchText);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: {SearchText}", searchText);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving feeds for category {CategoryId} (IncludeInactive: {IncludeInactive})", categoryId, includeInactive);
                var feeds = await _feedRepository.GetByCategoryAsync(categoryId, includeInactive);
                _logger.Debug("Found {Count} feeds for category {CategoryId}", feeds.Count, categoryId);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Feed>> GetUncategorizedFeedsAsync(bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Retrieving uncategorized feeds (IncludeInactive: {IncludeInactive})", includeInactive);
                var feeds = await _feedRepository.GetUncategorizedAsync(includeInactive);
                _logger.Debug("Found {Count} uncategorized feeds", feeds.Count);
                return feeds;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving uncategorized feeds");
                throw;
            }
        }

        #endregion

        #region Feed Properties

        /// <inheritdoc />
        public async Task<int> GetTotalArticleCountAsync(int feedId)
        {
            try
            {
                _logger.Debug("Retrieving total article count for feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId, true); // Include inactive for complete data
                var count = feed?.TotalArticleCount ?? 0;
                _logger.Debug("Total articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving total article count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> GetUnreadCountByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Retrieving unread count for feed {FeedId}", feedId);
                var count = await _articleRepository.GetUnreadCountByFeedAsync(feedId);
                _logger.Debug("Unread articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving unread count for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null)
        {
            try
            {
                _logger.Debug("Updating properties for feed {FeedId}", feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId, true); // Include inactive to update even inactive feeds

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

        #endregion

        #region Maintenance

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<int> UpdateAllFeedCountsAsync()
        {
            try
            {
                _logger.Debug("Updating article counts for all feeds");
                var feeds = await _feedRepository.GetAllAsync(true); // Include inactive to update all
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

        #endregion

        #region Private Helpers

        /// <summary>
        /// Updates the article counts for a specific feed.
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

        #endregion
    }
}