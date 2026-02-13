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
    /// Professional repository for RSS Articles using Entity Framework Core.
    /// Implements comprehensive data access with logging and transaction support.
    /// </summary>
    public class ArticleRepository : BaseRepository<Article>, IArticleRepository
    {
        private readonly ILogger _logger;

        public ArticleRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = logger.ForContext<ArticleRepository>();
        }

        // Implementation of missing methods from IArticleRepository
        public async Task<List<Article>> GetWhereAsync(Func<Article, bool> predicate)
        {
            try
            {
                _logger.Debug("Getting articles with predicate");
                var allArticles = await GetAllAsync();
                var result = allArticles.Where(predicate).ToList();
                _logger.Debug("Retrieved {Count} articles with predicate", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in GetWhereAsync");
                throw;
            }
        }

        public async Task<int> CountWhereAsync(Func<Article, bool> predicate)
        {
            try
            {
                _logger.Debug("Counting articles with predicate");
                var allArticles = await GetAllAsync();
                var count = allArticles.Count(predicate);
                _logger.Debug("Counted {Count} articles with predicate", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in CountWhereAsync");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles associated with a specific feed.
        /// </summary>
        public async Task<List<Article>> GetByFeedAsync(int feedId, int limit = 100)
        {
            try
            {
                _logger.Debug("Getting articles for feed {FeedId} with limit {Limit}", feedId, limit);

                var articles = await GetWhereAsync(a => a.FeedId == feedId);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {Count} articles for feed {FeedId}", articles.Count, feedId);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles belonging to a list of feeds.
        /// </summary>
        public async Task<List<Article>> GetByFeedsAsync(List<int> feedIds, int limit = 100)
        {
            try
            {
                _logger.Debug("Getting articles for {FeedCount} feeds with limit {Limit}", feedIds.Count, limit);

                var articles = await GetWhereAsync(a => feedIds.Contains(a.FeedId));
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {ArticleCount} articles for {FeedCount} feeds", articles.Count, feedIds.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles for {FeedCount} feeds", feedIds.Count);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all unread articles.
        /// </summary>
        public async Task<List<Article>> GetUnreadAsync(int limit = 100)
        {
            try
            {
                _logger.Debug("Getting unread articles with limit {Limit}", limit);

                var articles = await GetWhereAsync(a => a.Status == ArticleStatus.Unread);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {Count} unread articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles marked as starred (bookmarked).
        /// </summary>
        public async Task<List<Article>> GetStarredAsync()
        {
            try
            {
                _logger.Debug("Getting starred articles");

                var articles = await GetWhereAsync(a => a.IsStarred);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .ToList();

                _logger.Debug("Retrieved {Count} starred articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting starred articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles marked as favorites.
        /// </summary>
        public async Task<List<Article>> GetFavoritesAsync()
        {
            try
            {
                _logger.Debug("Getting favorite articles");

                var articles = await GetWhereAsync(a => a.IsFavorite);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .ToList();

                _logger.Debug("Retrieved {Count} favorite articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting favorite articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves unread articles for a specific feed.
        /// </summary>
        public async Task<List<Article>> GetUnreadByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Getting unread articles for feed {FeedId}", feedId);

                var articles = await GetWhereAsync(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .ToList();

                _logger.Debug("Retrieved {Count} unread articles for feed {FeedId}", articles.Count, feedId);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread articles for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Finds an article by its unique GUID.
        /// </summary>
        public async Task<Article?> GetByGuidAsync(string guid)
        {
            try
            {
                _logger.Debug("Getting article by GUID: {Guid}", guid);

                var article = await GetFirstOrDefaultAsync(a => a.Guid == guid);

                if (article == null)
                    _logger.Debug("Article with GUID {Guid} not found", guid);
                else
                    _logger.Debug("Found article with GUID {Guid}: {Title}", guid, article.Title);

                return article;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting article by GUID: {Guid}", guid);
                throw;
            }
        }

        /// <summary>
        /// Finds an article by its content hash.
        /// </summary>
        public async Task<Article?> GetByContentHashAsync(string hash)
        {
            try
            {
                _logger.Debug("Getting article by content hash: {Hash}", hash);

                var article = await GetFirstOrDefaultAsync(a => a.ContentHash == hash);

                if (article == null)
                    _logger.Debug("Article with content hash {Hash} not found", hash);
                else
                    _logger.Debug("Found article with content hash {Hash}: {Title}", hash, article.Title);

                return article;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting article by content hash: {Hash}", hash);
                throw;
            }
        }

        /// <summary>
        /// Checks if an article with the specified GUID already exists.
        /// </summary>
        public async Task<bool> ExistsByGuidAsync(string guid)
        {
            try
            {
                _logger.Debug("Checking if article exists by GUID: {Guid}", guid);
                var article = await GetByGuidAsync(guid);
                var exists = article != null;

                _logger.Debug("Article with GUID {Guid} exists: {Exists}", guid, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if article exists by GUID: {Guid}", guid);
                throw;
            }
        }

        /// <summary>
        /// Checks if an article with the specified content hash already exists.
        /// </summary>
        public async Task<bool> ExistsByContentHashAsync(string hash)
        {
            try
            {
                _logger.Debug("Checking if article exists by content hash: {Hash}", hash);
                var article = await GetByContentHashAsync(hash);
                var exists = article != null;

                _logger.Debug("Article with content hash {Hash} exists: {Exists}", hash, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if article exists by content hash: {Hash}", hash);
                throw;
            }
        }

        /// <summary>
        /// Updates the status (Read/Unread) of a specific article.
        /// </summary>
        public async Task<int> MarkAsAsync(int articleId, ArticleStatus status)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as {Status}", articleId, status);
                var article = await GetByIdAsync(articleId);
                if (article != null)
                {
                    var oldStatus = article.Status;
                    article.Status = status;
                    var result = await UpdateAsync(article);

                    _logger.Information("Article {ArticleId} status changed from {OldStatus} to {NewStatus}",
                        articleId, oldStatus, status);
                    return result;
                }

                _logger.Warning("Article {ArticleId} not found for status update", articleId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as {Status}", articleId, status);
                throw;
            }
        }

        /// <summary>
        /// Toggles the starred status of an article.
        /// </summary>
        public async Task<int> ToggleStarAsync(int articleId)
        {
            try
            {
                _logger.Debug("Toggling star status for article {ArticleId}", articleId);
                var article = await GetByIdAsync(articleId);
                if (article != null)
                {
                    var oldStatus = article.IsStarred;
                    article.IsStarred = !article.IsStarred;
                    var result = await UpdateAsync(article);

                    _logger.Information("Article {ArticleId} star status changed from {OldStatus} to {NewStatus}",
                        articleId, oldStatus, article.IsStarred);
                    return result;
                }

                _logger.Warning("Article {ArticleId} not found for star toggle", articleId);
                return 0;
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
        public async Task<int> ToggleFavoriteAsync(int articleId)
        {
            try
            {
                _logger.Debug("Toggling favorite status for article {ArticleId}", articleId);
                var article = await GetByIdAsync(articleId);
                if (article != null)
                {
                    var oldStatus = article.IsFavorite;
                    article.IsFavorite = !article.IsFavorite;
                    var result = await UpdateAsync(article);

                    _logger.Information("Article {ArticleId} favorite status changed from {OldStatus} to {NewStatus}",
                        articleId, oldStatus, article.IsFavorite);
                    return result;
                }

                _logger.Warning("Article {ArticleId} not found for favorite toggle", articleId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error toggling favorite status for article {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Marks all articles in a feed as read.
        /// </summary>
        public async Task<int> MarkAllAsReadByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Marking all articles as read for feed {FeedId}", feedId);

                var articles = await GetWhereAsync(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread);

                if (!articles.Any())
                {
                    _logger.Debug("No unread articles found for feed {FeedId}", feedId);
                    return 0;
                }

                foreach (var article in articles)
                {
                    article.Status = ArticleStatus.Read;
                }

                var result = await UpdateAllAsync(articles);
                _logger.Information("Marked {Count} articles as read for feed {FeedId}", articles.Count, feedId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking all articles as read for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Marks all articles as read.
        /// </summary>
        public async Task<int> MarkAllAsReadAsync()
        {
            try
            {
                _logger.Debug("Marking all articles as read");

                // ? EF Core 7+: ExecuteUpdateAsync actualiza directamente sin cargar entidades
                // No hay problema de tracking porque no se cargan objetos en memoria
                var updatedCount = await _context.Articles
                    .Where(a => a.Status == ArticleStatus.Unread)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(a => a.Status, ArticleStatus.Read)
                        .SetProperty(a => a.AddedDate, DateTime.UtcNow));

                if (updatedCount > 0)
                {
                    _logger.Information("Marked {Count} articles as read", updatedCount);
                }
                else
                {
                    _logger.Debug("No unread articles found");
                }

                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking all articles as read");
                throw;
            }
        }
        /// <summary>
        /// Gets the total count of unread articles.
        /// </summary>
        public async Task<int> GetUnreadCountAsync()
        {
            try
            {
                _logger.Debug("Getting total unread count");
                var count = await CountWhereAsync(a => a.Status == ArticleStatus.Unread);
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
        /// Gets the unread count for a specific feed.
        /// </summary>
        public async Task<int> GetUnreadCountByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Getting unread count for feed {FeedId}", feedId);
                var count = await CountWhereAsync(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread);
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
        /// Retrieves unread counts grouped by FeedId.
        /// </summary>
        public async Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync()
        {
            try
            {
                _logger.Debug("Getting unread counts grouped by feed");

                var unreadArticles = await GetWhereAsync(a => a.Status == ArticleStatus.Unread);
                var result = unreadArticles
                    .GroupBy(a => a.FeedId)
                    .ToDictionary(g => g.Key, g => g.Count());

                _logger.Debug("Retrieved unread counts for {Count} feeds", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unread counts grouped by feed");
                throw;
            }
        }

        /// <summary>
        /// Searches for articles containing the search text in title, content, or summary.
        /// </summary>
        public async Task<List<Article>> SearchAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<Article>();
                }

                _logger.Debug("Searching articles for: {SearchText}", searchText);

                var allArticles = await GetAllAsync();
                var articles = allArticles
                    .Where(a => a.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                               a.Content.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                               a.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                               a.Author.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                               a.Categories.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.PublishedDate)
                    .ToList();

                _logger.Debug("Found {Count} articles for search: {SearchText}", articles.Count, searchText);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching articles for: {SearchText}", searchText);
                throw;
            }
        }

        /// <summary>
        /// Deletes articles older than the specified date, excluding starred and favorite items.
        /// </summary>
        public async Task<int> DeleteOlderThanAsync(DateTime date)
        {
            try
            {
                _logger.Debug("Deleting articles older than {Date}", date);

                // ? EF Core 7+: ExecuteDeleteAsync elimina directamente sin cargar entidades
                // No hay problema de tracking porque no se cargan objetos en memoria
                var deletedCount = await _context.Articles
                    .Where(a => a.PublishedDate < date && !a.IsStarred && !a.IsFavorite)
                    .ExecuteDeleteAsync();

                _logger.Information("Deleted {Count} articles older than {Date}", deletedCount, date);
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting articles older than {Date}", date);
                throw;
            }
        }

        /// <summary>
        /// Deletes all articles belonging to a specific feed.
        /// </summary>
        public async Task<int> DeleteByFeedAsync(int feedId)
        {
            try
            {
                _logger.Debug("Deleting all articles for feed {FeedId}", feedId);

                // ? SQL CORRECTO CON PARÁMETRO
                var sql = "DELETE FROM Articles WHERE FeedId = @p0";
                var result = await _context.ExecuteSqlCommandAsync(sql,cancellationToken:default  ,feedId);

                _logger.Information("Deleted {Count} articles for feed {FeedId}", result, feedId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting articles for feed {FeedId}", feedId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles that haven't been processed by rules.
        /// </summary>
        public async Task<List<Article>> GetUnprocessedArticlesAsync(int limit = 100)
        {
            try
            {
                _logger.Debug("Getting unprocessed articles with limit {Limit}", limit);

                var articles = await GetWhereAsync(a => !a.ProcessedByRules);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {Count} unprocessed articles", articles.Count);
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
        public async Task<List<Article>> GetUnnotifiedArticlesAsync(int limit = 100)
        {
            try
            {
                _logger.Debug("Getting unnotified articles with limit {Limit}", limit);

                var articles = await GetWhereAsync(a => !a.IsNotified);
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {Count} unnotified articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting unnotified articles");
                throw;
            }
        }

        /// <summary>
        /// Retrieves articles by categories.
        /// </summary>
        public async Task<List<Article>> GetByCategoriesAsync(string categories, int limit = 100)
        {
            try
            {
                _logger.Debug("Getting articles by categories: {Categories} with limit {Limit}", categories, limit);

                var articles = await GetWhereAsync(a => a.Categories.Contains(categories));
                articles = articles
                    .OrderByDescending(a => a.PublishedDate)
                    .Take(limit)
                    .ToList();

                _logger.Debug("Retrieved {Count} articles for categories: {Categories}", articles.Count, categories);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting articles by categories: {Categories}", categories);
                throw;
            }
        }

        /// <summary>
        /// Marks an article as processed by rules.
        /// </summary>
        public async Task<int> MarkAsProcessedAsync(int articleId)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as processed", articleId);
                var article = await GetByIdAsync(articleId);
                if (article != null)
                {
                    article.ProcessedByRules = true;
                    var result = await UpdateAsync(article);

                    _logger.Information("Article {ArticleId} marked as processed", articleId);
                    return result;
                }

                _logger.Warning("Article {ArticleId} not found for processing", articleId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as processed", articleId);
                throw;
            }
        }

        /// <summary>
        /// Marks an article as notified.
        /// </summary>
        public async Task<int> MarkAsNotifiedAsync(int articleId)
        {
            try
            {
                _logger.Debug("Marking article {ArticleId} as notified", articleId);
                var article = await GetByIdAsync(articleId);
                if (article != null)
                {
                    article.IsNotified = true;
                    var result = await UpdateAsync(article);

                    _logger.Information("Article {ArticleId} marked as notified", articleId);
                    return result;
                }

                _logger.Warning("Article {ArticleId} not found for notification", articleId);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking article {ArticleId} as notified", articleId);
                throw;
            }
        }

        /// <summary>
        /// Marks multiple articles as processed by rules.
        /// </summary>
        public async Task<int> BulkMarkAsProcessedAsync(List<int> articleIds)
        {
            try
            {
                _logger.Debug("Bulk marking {Count} articles as processed", articleIds.Count);
                if (!articleIds.Any())
                {
                    _logger.Debug("No article IDs provided for bulk processing");
                    return 0;
                }

                var allArticles = await GetAllAsync();
                var articles = allArticles
                    .Where(a => articleIds.Contains(a.Id))
                    .ToList();

                if (!articles.Any())
                {
                    _logger.Debug("No articles found for bulk processing");
                    return 0;
                }

                foreach (var article in articles)
                {
                    article.ProcessedByRules = true;
                }

                var result = await UpdateAllAsync(articles);
                _logger.Information("Bulk marked {Count} articles as processed", articles.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error bulk marking {Count} articles as processed", articleIds.Count);
                throw;
            }
        }

        /// <summary>
        /// Marks multiple articles as notified.
        /// </summary>
        public async Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds)
        {
            try
            {
                _logger.Debug("Bulk marking {Count} articles as notified", articleIds.Count);
                if (!articleIds.Any())
                {
                    _logger.Debug("No article IDs provided for bulk notification");
                    return 0;
                }

                var allArticles = await GetAllAsync();
                var articles = allArticles
                    .Where(a => articleIds.Contains(a.Id))
                    .ToList();

                if (!articles.Any())
                {
                    _logger.Debug("No articles found for bulk notification");
                    return 0;
                }

                foreach (var article in articles)
                {
                    article.IsNotified = true;
                }

                var result = await UpdateAllAsync(articles);
                _logger.Information("Bulk marked {Count} articles as notified", articles.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error bulk marking {Count} articles as notified", articleIds.Count);
                throw;
            }
        }

        /// <summary>
        /// Retrieves paged articles with optional status filter.
        /// </summary>
        public async Task<List<Article>> GetPagedAsync(int page, int pageSize, ArticleStatus? status = null)
        {
            try
            {
                _logger.Debug("Getting paged articles - Page: {Page}, PageSize: {PageSize}, Status: {Status}",
                    page, pageSize, status?.ToString() ?? "Any");

                var allArticles = await GetAllAsync();
                var query = allArticles.AsQueryable();

                if (status.HasValue)
                {
                    query = query.Where(a => a.Status == status.Value);
                }

                var articles = query
                    .OrderByDescending(a => a.PublishedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                _logger.Debug("Retrieved {Count} paged articles", articles.Count);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting paged articles - Page: {Page}, PageSize: {PageSize}", page, pageSize);
                throw;
            }
        }

        /// <summary>
        /// Retrieves paged articles for a specific feed with optional status filter.
        /// </summary>
        public async Task<List<Article>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null)
        {
            try
            {
                _logger.Debug("Getting paged articles for feed {FeedId} - Page: {Page}, PageSize: {PageSize}, Status: {Status}",
                    feedId, page, pageSize, status?.ToString() ?? "Any");

                var allArticles = await GetAllAsync();
                var query = allArticles
                    .Where(a => a.FeedId == feedId)
                    .AsQueryable();

                if (status.HasValue)
                {
                    query = query.Where(a => a.Status == status.Value);
                }

                var articles = query
                    .OrderByDescending(a => a.PublishedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                _logger.Debug("Retrieved {Count} paged articles for feed {FeedId}", articles.Count, feedId);
                return articles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting paged articles for feed {FeedId} - Page: {Page}, PageSize: {PageSize}",
                    feedId, page, pageSize);
                throw;
            }
        }

        /// <summary>
        /// Inserts multiple articles in a single transaction.
        /// </summary>
        public async Task<int> InsertAllAsync(List<Article> entities)
        {
            try
            {
                if (entities == null || !entities.Any())
                {
                    _logger.Debug("No entities to insert");
                    return 0;
                }

                _logger.Debug("Inserting {Count} articles in bulk", entities.Count);

                var result = await base.InsertAllAsync(entities);

                _logger.Information("Successfully inserted {Count} articles in bulk", entities.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error inserting {Count} articles in bulk", entities?.Count ?? 0);
                throw;
            }
        }

        /// <summary>
        /// Updates multiple articles in a single transaction.
        /// </summary>
        public async Task<int> UpdateAllAsync(List<Article> entities)
        {
            try
            {
                if (entities == null || !entities.Any())
                {
                    _logger.Debug("No entities to update");
                    return 0;
                }

                _logger.Debug("Updating {Count} articles in bulk", entities.Count);

                var result = await base.UpdateAllAsync(entities);

                _logger.Information("Successfully updated {Count} articles in bulk", entities.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating {Count} articles in bulk", entities?.Count ?? 0);
                throw;
            }
        }
    }
}