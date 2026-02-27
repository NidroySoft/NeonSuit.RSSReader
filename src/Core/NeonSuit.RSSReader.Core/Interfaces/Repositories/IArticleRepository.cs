using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing <see cref="Article"/> domain entities.
/// Provides comprehensive data access operations including CRUD, specialized queries,
/// bulk status updates, duplicate detection, pagination, search, and maintenance tasks.
/// </summary>
/// <remarks>
/// <para>
/// This repository serves as the primary persistence abstraction for articles in the RSS reader application.
/// It encapsulates all database interactions related to articles, ensuring separation from business logic
/// and enabling testability (e.g., via in-memory or mock implementations).
/// </para>
///
/// <para>
/// Core responsibilities and behavioral expectations:
/// </para>
/// <list type="bullet">
/// <item>All write operations should be atomic and transactional where appropriate.</item>
/// <item>Read operations must use indexed columns (FeedId, Status, Guid, ContentHash) for efficiency.</item>
/// <item>Existence and count checks must avoid loading full entities (use scalar queries).</item>
/// <item>Bulk operations are optimized for sync/import/cleanup scenarios.</item>
/// <item>Methods that affect unread counters should trigger recalculation in the implementation.</item>
/// </list>
/// </remarks>
public interface IArticleRepository : IRepository<Article>
{
    #region Basic CRUD Operations

    /// <summary>
    /// Retrieves a single article by its primary key with tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article if found; otherwise null.</returns>
    new Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single article by its primary key with no tracking (read-only).
    /// </summary>
    /// <param name="id">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article if found; otherwise null.</returns>
    /// <remarks>Use this for read-only operations to improve performance.</remarks>
    Task<Article?> GetByIdReadOnlyAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches an article from the change tracker to prevent unintended updates.
    /// </summary>
    /// <param name="id">The ID of the article to detach.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DetachEntityAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all articles (use with caution on large datasets).
    /// </summary>
    new Task<List<Article>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters articles using a server-side predicate expression.
    /// </summary>
    /// <param name="predicate">Lambda expression used to filter articles (e.g., a => a.FeedId == 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of articles that match the predicate.</returns>
    /// <remarks>This method executes filtering at the database level for optimal performance.</remarks>
    new Task<List<Article>> GetWhereAsync(Expression<Func<Article, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new article and returns the generated database ID.
    /// </summary>
    new Task<int> InsertAsync(Article entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk inserts multiple articles in a single operation.
    /// </summary>
    Task<int> InsertAllAsync(List<Article> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing article.
    /// </summary>
    new Task<int> UpdateAsync(Article entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk updates multiple articles.
    /// </summary>
    Task<int> UpdateAllAsync(List<Article> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an article by entity reference.
    /// </summary>
    new Task<int> DeleteAsync(Article entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts articles matching a server-side predicate.
    /// </summary>
    /// <param name="predicate">Lambda expression to filter articles.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of matching articles.</returns>
    new Task<int> CountWhereAsync(Expression<Func<Article, bool>> predicate, CancellationToken cancellationToken = default);

    #endregion

    #region Specialized Retrieval

    /// <summary>
    /// Retrieves the most recent articles from a specific feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed to query.</param>
    /// <param name="limit">Maximum number of articles to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Articles ordered by publication date descending.</returns>
    Task<List<Article>> GetByFeedAsync(int feedId, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles from multiple feeds (union of results).
    /// </summary>
    /// <param name="feedIds">List of feed IDs to include.</param>
    /// <param name="limit">Maximum number of articles to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined list ordered by publication date descending.</returns>
    Task<List<Article>> GetByFeedsAsync(List<int> feedIds, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unread articles across all feeds.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unread articles ordered by publication date descending.</returns>
    Task<List<Article>> GetUnreadAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently starred (bookmarked) articles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Starred articles ordered by publication date descending.</returns>
    Task<List<Article>> GetStarredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all currently favorited articles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Favorited articles ordered by publication date descending.</returns>
    Task<List<Article>> GetFavoritesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unread articles for a specific feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unread articles in the feed, ordered by publication date descending.</returns>
    Task<List<Article>> GetUnreadByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    #endregion

    #region Duplicate Detection & Lookup

    /// <summary>
    /// Finds an existing article by its syndication GUID.
    /// </summary>
    /// <param name="guid">The unique GUID from the feed item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching article, or null if not found.</returns>
    Task<Article?> GetByGuidAsync(string guid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an article by its computed content hash.
    /// </summary>
    /// <param name="hash">The content hash (e.g., SHA256 of title + content).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching article, or null if not found.</returns>
    Task<Article?> GetByContentHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an article with the specified GUID already exists.
    /// </summary>
    /// <param name="guid">The syndication GUID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an article with the GUID exists; otherwise false.</returns>
    Task<bool> ExistsByGuidAsync(string guid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an article with the specified content hash already exists.
    /// </summary>
    /// <param name="hash">The content hash to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an article with the hash exists; otherwise false.</returns>
    Task<bool> ExistsByContentHashAsync(string hash, CancellationToken cancellationToken = default);

    #endregion

    #region Status & Toggle Operations

    /// <summary>
    /// Changes the read/unread status of a single article.
    /// </summary>
    /// <param name="articleId">The ID of the article to update.</param>
    /// <param name="status">The new status to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected (1 on success, 0 if not found).</returns>
    Task<int> MarkAsAsync(int articleId, ArticleStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the starred (bookmarked) state of an article.
    /// </summary>
    /// <param name="articleId">The ID of the article to toggle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected (1 on success, 0 if not found).</returns>
    Task<int> ToggleStarAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the favorite state of an article.
    /// </summary>
    /// <param name="articleId">The ID of the article to toggle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected (1 on success, 0 if not found).</returns>
    Task<int> ToggleFavoriteAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all articles in a specific feed as read.
    /// </summary>
    /// <param name="feedId">The ID of the feed to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles updated to Read status.</returns>
    Task<int> MarkAllAsReadByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all unread articles across all feeds as read.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles updated to Read status.</returns>
    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Counters & Aggregates

    /// <summary>
    /// Gets the total number of unread articles across all feeds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total unread count.</returns>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the unread count for a specific feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed to count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of unread articles in the feed.</returns>
    Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread counts grouped by feed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping FeedId → unread count.</returns>
    Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Search & Advanced Filtering

    /// <summary>
    /// Searches articles by keyword across multiple fields.
    /// </summary>
    /// <param name="searchText">The search term (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching articles ordered by publication date descending.</returns>
    Task<List<Article>> SearchAsync(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles matching one or more categories or tags.
    /// </summary>
    /// <param name="categories">Comma-separated category/tag names.</param>
    /// <param name="limit">Maximum number of articles to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching articles ordered by publication date descending.</returns>
    Task<List<Article>> GetByCategoriesAsync(string categories, int limit = 100, CancellationToken cancellationToken = default);

    #endregion

    #region Processing & Notification Workflow

    /// <summary>
    /// Retrieves articles not yet processed by rules.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unprocessed articles.</returns>
    Task<List<Article>> GetUnprocessedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles not yet notified.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return (default: 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unnotified articles.</returns>
    Task<List<Article>> GetUnnotifiedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single article as processed by rules.
    /// </summary>
    /// <param name="articleId">The ID of the article to mark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected (1 on success).</returns>
    Task<int> MarkAsProcessedAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single article as notified.
    /// </summary>
    /// <param name="articleId">The ID of the article to mark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows affected (1 on success).</returns>
    Task<int> MarkAsNotifiedAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk marks multiple articles as processed.
    /// </summary>
    /// <param name="articleIds">List of article IDs to mark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully marked.</returns>
    Task<int> BulkMarkAsProcessedAsync(List<int> articleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk marks multiple articles as notified.
    /// </summary>
    /// <param name="articleIds">List of article IDs to mark.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully marked.</returns>
    Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds, CancellationToken cancellationToken = default);

    #endregion

    #region Cleanup Operations

    /// <summary>
    /// Deletes articles older than the specified date, excluding starred and favorite items.
    /// </summary>
    /// <param name="date">Cutoff date (articles older than this are eligible).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all articles belonging to a specific feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed to clear.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles deleted.</returns>
    Task<int> DeleteByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    #endregion

    #region Pagination Support

    /// <summary>
    /// Retrieves a paginated list of articles with optional status filter.
    /// </summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated articles ordered by publication date descending.</returns>
    Task<List<Article>> GetPagedAsync(int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of articles from a specific feed.
    /// </summary>
    /// <param name="feedId">The ID of the feed.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated articles from the feed.</returns>
    Task<List<Article>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default);

    #endregion
}