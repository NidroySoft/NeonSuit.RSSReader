using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional service for managing RSS articles.
    /// Optimized to minimize memory footprint and database roundtrips.
    /// </summary>
    public class ArticleService : IArticleService
    {
        private readonly IArticleRepository _articleRepository;
        private readonly IFeedRepository _feedRepository;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ArticleService class.
        /// </summary>
        /// <param name="articleRepository">The article repository.</param>
        /// <param name="feedRepository">The feed repository.</param>
        public ArticleService(
            IArticleRepository articleRepository,
            IFeedRepository feedRepository,
            ILogger logger)
        {
            _articleRepository = articleRepository;
            _feedRepository = feedRepository;
            _logger = logger.ForContext<ArticleService>();
        }

        /// <summary>
        /// Retrieves all articles from the database.
        /// </summary>
        /// <returns>A list of all articles.</returns>
        public async Task<List<Article>> GetAllArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting all articles");
                var articles = await _articleRepository.GetAllAsync();
                _logger.Information("Retrieved {Count} articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles for a specific feed and attaches the parent feed object.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>A list of articles for the specified feed.</returns>
        public async Task<List<Article>> GetArticlesByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Getting articles for feed {FeedId}", feedId);
                var articles = await _articleRepository.GetByFeedAsync(feedId);
                var feed = await _feedRepository.GetByIdAsync(feedId);

                if (feed != null)
                {
                    articles.ForEach(a => a.Feed = feed);
                    _logger.Debug("Attached feed {FeedTitle} to {Count} articles", feed.Title, articles.Count);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found when attaching to articles", feedId);
                }

                _logger.Information("Retrieved {Count} articles for feed {FeedId}", articles.Count, feedId);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles for an entire category.
        /// Optimized: Filters by multiple feed IDs at the database level.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>A list of articles in the specified category.</returns>
        public async Task<List<Article>> GetArticlesByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Getting articles for category {CategoryId}", categoryId);
                var feeds = await _feedRepository.GetByCategoryAsync(categoryId);

                if (!feeds.Any())
                {
                    _logger.Debug("No feeds found for category {CategoryId}", categoryId);
                    return new List<Article>();
                }

                var feedIds = feeds.Select(f => f.Id).ToList();
                var articles = await _articleRepository.GetByFeedsAsync(feedIds);

                var feedMap = feeds.ToDictionary(f => f.Id);
                articles.ForEach(a =>
                {
                    if (feedMap.ContainsKey(a.FeedId))
                        a.Feed = feedMap[a.FeedId];
                });

                _logger.Information("Retrieved {Count} articles for category {CategoryId} from {FeedCount} feeds",
                    articles.Count, categoryId, feeds.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles for category {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all currently unread articles.
        /// </summary>
        /// <returns>A list of unread articles.</returns>
        public async Task<List<Article>> GetUnreadArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting unread articles");
                var articles = await _articleRepository.GetUnreadAsync();
                _logger.Information("Retrieved {Count} unread articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles marked as starred by the user.
        /// </summary>
        /// <returns>A list of starred articles.</returns>
        public async Task<List<Article>> GetStarredArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting starred articles");
                var articles = await _articleRepository.GetStarredAsync();
                _logger.Information("Retrieved {Count} starred articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting starred articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles marked as favorites by the user.
        /// </summary>
        /// <returns>A list of favorite articles.</returns>
        public async Task<List<Article>> GetFavoriteArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting favorite articles");
                var articles = await _articleRepository.GetFavoritesAsync();
                _logger.Information("Retrieved {Count} favorite articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting favorite articles");
                throw;
            }
        }

        /// <summary>
        /// Marks an article as read or unread.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <param name="isRead">True to mark as read, false to mark as unread.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> MarkAsReadAsync(int articleId, bool isRead)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as {Status}", articleId, isRead ? "Read" : "Unread");
                var status = isRead ? ArticleStatus.Read : ArticleStatus.Unread;
                var result = await _articleRepository.MarkAsAsync(articleId, status);

                if (result > 0)
                {
                    _logger.Information("Article {ArticleId} marked as {Status}", articleId, isRead ? "Read" : "Unread");
                }
                else
                {
                    _logger.Warning("Failed to mark article {ArticleId} as {Status}", articleId, isRead ? "Read" : "Unread");
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as {Status}", articleId, isRead ? "Read" : "Unread");
                throw;
            }
        }

        /// <summary>
        /// Toggles the starred status of an article.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> ToggleStarredAsync(int articleId)
        {
            try
            {
                _logger.Debug("Toggling star status for article {ArticleId}", articleId);
                var result = await _articleRepository.ToggleStarAsync(articleId);

                if (result > 0)
                {
                    _logger.Information("Toggled star status for article {ArticleId}", articleId);
                }
                else
                {
                    _logger.Warning("Failed to toggle star status for article {ArticleId}", articleId);
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error toggling star status for article {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Toggles the favorite status of an article.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> ToggleFavoriteAsync(int articleId)
        {
            try
            {
                _logger.Debug("Toggling favorite status for article {ArticleId}", articleId);
                var result = await _articleRepository.ToggleFavoriteAsync(articleId);

                if (result > 0)
                {
                    _logger.Information("Toggled favorite status for article {ArticleId}", articleId);
                }
                else
                {
                    _logger.Warning("Failed to toggle favorite status for article {ArticleId}", articleId);
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error toggling favorite status for article {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Performs a full-text search across all stored articles.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <returns>A list of matching articles.</returns>
        public async Task<List<Article>> SearchArticlesAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Article>();
                }

                _logger.Debug("Searching articles for: {SearchText}", searchText);
                var articles = await _articleRepository.SearchAsync(searchText);

                var feedIds = articles.Select(a => a.FeedId).Distinct().ToList();
                var feeds = new Dictionary<int, Feed>();

                foreach (var feedId in feedIds)
                {
                    var feed = await _feedRepository.GetByIdAsync(feedId);
                    if (feed != null) feeds[feedId] = feed;
                }

                articles.ForEach(a =>
                {
                    if (feeds.ContainsKey(a.FeedId))
                        a.Feed = feeds[a.FeedId];
                });

                _logger.Information("Found {Count} articles for search: {SearchText}", articles.Count, searchText);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching articles for: {SearchText}", searchText);
                throw;
            }
        }

        /// <summary>
        /// Removes articles older than a specific date to free up storage.
        /// </summary>
        /// <param name="daysOld">The age in days (default: 30).</param>
        /// <returns>The number of articles deleted.</returns>
        public async Task<int> DeleteOldArticlesAsync(int daysOld = 30)
        {
            try
            {
                _logger.Debug("Deleting articles older than {DaysOld} days", daysOld);
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var deletedCount = await _articleRepository.DeleteOlderThanAsync(cutoffDate);

                _logger.Information("Deleted {Count} articles older than {DaysOld} days", deletedCount, daysOld);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting articles older than {DaysOld} days", daysOld);
                throw;
            }
        }

        /// <summary>
        /// Gets the total count of unread articles.
        /// </summary>
        /// <returns>The total unread article count.</returns>
        public async Task<int> GetUnreadCountAsync()
        {
            try
            {
                _logger.Debug("Getting total unread count");
                var count = await _articleRepository.GetUnreadCountAsync();
                _logger.Debug("Total unread articles: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting total unread count");
                throw;
            }
        }

        /// <summary>
        /// Gets the unread article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The unread article count for the feed.</returns>
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
        /// Marks all articles as read.
        /// </summary>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> MarkAllAsReadAsync()
        {
            try
            {
                _logger.Debug("Marking all articles as read");
                var result = await _articleRepository.MarkAllAsReadAsync();

                if (result > 0)
                {
                    _logger.Information("Marked all articles as read, affected {Count} articles", result);
                }
                else
                {
                    _logger.Debug("No articles to mark as read");
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking all articles as read");
                throw;
            }
        }

        /// <summary>
        /// Marks an article as notified.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> MarkAsNotifiedAsync(int articleId)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as notified", articleId);
                var result = await _articleRepository.MarkAsNotifiedAsync(articleId);

                if (result > 0)
                {
                    _logger.Information("Article {ArticleId} marked as notified", articleId);
                }
                else
                {
                    _logger.Warning("Failed to mark article {ArticleId} as notified", articleId);
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as notified", articleId);
                throw;
            }
        }

        /// <summary>
        /// Marks an article as processed by rules.
        /// </summary>
        /// <param name="articleId">The article identifier.</param>
        /// <returns>True if the operation succeeded, otherwise false.</returns>
        public async Task<bool> MarkAsProcessedAsync(int articleId)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as processed", articleId);
                var result = await _articleRepository.MarkAsProcessedAsync(articleId);

                if (result > 0)
                {
                    _logger.Information("Article {ArticleId} marked as processed", articleId);
                }
                else
                {
                    _logger.Warning("Failed to mark article {ArticleId} as processed", articleId);
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as processed", articleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles that haven't been processed by rules.
        /// </summary>
        /// <returns>A list of unprocessed articles.</returns>
        public async Task<List<Article>> GetUnprocessedArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting unprocessed articles");
                var articles = await _articleRepository.GetUnprocessedArticlesAsync();
                _logger.Information("Retrieved {Count} unprocessed articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unprocessed articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles that haven't been notified to the user.
        /// </summary>
        /// <returns>A list of unnotified articles.</returns>
        public async Task<List<Article>> GetUnnotifiedArticlesAsync()
        {
            try
            {
                _logger.Debug("Getting unnotified articles");
                var articles = await _articleRepository.GetUnnotifiedArticlesAsync();
                _logger.Information("Retrieved {Count} unnotified articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unnotified articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves unread counts grouped by FeedId.
        /// </summary>
        /// <returns>A dictionary mapping feed IDs to unread counts.</returns>
        public async Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync()
        {
            try
            {
                _logger.Debug("Getting unread counts by feed");
                var counts = await _articleRepository.GetUnreadCountsByFeedAsync();
                _logger.Information("Retrieved unread counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread counts by feed");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles by categories.
        /// </summary>
        /// <param name="categories">The categories to filter by.</param>
        /// <returns>A list of articles in the specified categories.</returns>
        public async Task<List<Article>> GetArticlesByCategoriesAsync(string categories)
        {
            try
            {
                _logger.Debug("Getting articles by categories: {Categories}", categories);
                var articles = await _articleRepository.GetByCategoriesAsync(categories);
                _logger.Information("Retrieved {Count} articles for categories: {Categories}", articles.Count, categories);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles by categories: {Categories}", categories);
                throw;
            }
        }

        /// <summary>
        /// Retrieves paged articles with optional unread filter.
        /// </summary>
        /// <param name="page">The page number (1-based).</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <param name="unreadOnly">Optional filter for unread articles only.</param>
        /// <returns>A list of paged articles.</returns>
        public async Task<List<Article>> GetPagedArticlesAsync(int page, int pageSize, bool? unreadOnly = null)
        {
            try
            {
                _logger.Debug("Getting paged articles - Page: {Page}, PageSize: {PageSize}, UnreadOnly: {UnreadOnly}",
                    page, pageSize, unreadOnly);

                ArticleStatus? status = null;
                if (unreadOnly.HasValue && unreadOnly.Value)
                {
                    status = ArticleStatus.Unread;
                }

                var articles = await _articleRepository.GetPagedAsync(page, pageSize, status);
                _logger.Information("Retrieved {Count} paged articles (Page {Page}, Size {PageSize})",
                    articles.Count, page, pageSize);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting paged articles - Page: {Page}, PageSize: {PageSize}", page, pageSize);
                throw;
            }
        }
    }
}