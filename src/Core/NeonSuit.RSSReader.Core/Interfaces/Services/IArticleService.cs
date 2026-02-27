using NeonSuit.RSSReader.Core.DTOs.Article;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;

namespace NeonSuit.RSSReader.Core.Interfaces.Services;

/// <summary>
/// Service interface for managing article-related business logic and operations.
/// Acts as an abstraction layer between UI/presentation and data repositories,
/// coordinating article retrieval, state changes (read/star/favorite), search,
/// cleanup, and statistics.
/// </summary>
/// <remarks>
/// <para>
/// This service centralizes all article-related use cases in the RSS reader application,
/// ensuring consistent business rules, validation, and transaction boundaries.
/// It delegates persistence to <see cref="IArticleRepository"/> and related repositories.
/// </para>
/// <para>
/// All methods return DTOs instead of entities to maintain separation of concerns
/// and prevent leaking persistence details to the presentation layer.
/// </para>
/// <para>
/// Implementations must ensure:
/// <list type="bullet">
/// <item>All read operations are efficient and memory-safe (pagination, AsNoTracking, server-side filtering).</item>
/// <item>State changes (read/star/favorite/notified/processed) are atomic and logged.</item>
/// <item>Search supports full-text or keyword matching with relevance ordering.</item>
/// <item>Cleanup operations (delete old) are safe and respect user preferences (e.g., starred/favorite protection).</item>
/// <item>Statistics (unread counts) are cached or computed efficiently.</item>
/// <item>Cancellation support for long-running operations via <see cref="CancellationToken"/>.</item>
/// </list>
/// </para>
/// </remarks>
public interface IArticleService
{
    #region Read Collection Operations

    /// <summary>
    /// Retrieves all articles from the data store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of article summaries.</returns>
    /// <remarks>
    /// This method is intended for administrative or export scenarios.
    /// Prefer <see cref="GetPagedArticlesAsync"/> for display purposes.
    /// </remarks>
    Task<List<ArticleSummaryDto>> GetAllArticlesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles associated with a specific feed.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of article summaries for the specified feed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    Task<List<ArticleSummaryDto>> GetArticlesByFeedAsync(int feedId, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles associated with a specific category.
    /// </summary>
    /// <param name="categoryId">The unique identifier of the category.</param>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of article summaries for the specified category.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if categoryId is less than or equal to 0.</exception>
    Task<List<ArticleSummaryDto>> GetArticlesByCategoryAsync(int categoryId, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all unread articles across all feeds.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unread article summaries.</returns>
    Task<List<ArticleSummaryDto>> GetUnreadArticlesAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all starred (bookmarked) articles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of starred article summaries.</returns>
    Task<List<ArticleSummaryDto>> GetStarredArticlesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all favorite articles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of favorite article summaries.</returns>
    Task<List<ArticleSummaryDto>> GetFavoriteArticlesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves unread articles for a specific feed.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unread article summaries for the specified feed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    Task<List<ArticleSummaryDto>> GetUnreadByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches articles by keyword or phrase, matching against title and/or content.
    /// </summary>
    /// <param name="searchText">The search term (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching article summaries, ordered by relevance.</returns>
    /// <exception cref="ArgumentException">Thrown if searchText is null or whitespace.</exception>
    Task<List<ArticleSummaryDto>> SearchArticlesAsync(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of articles with an optional filter by status.
    /// </summary>
    /// <param name="page">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of articles to return per page.</param>
    /// <param name="status">Optional filter by article status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of article summaries for the specified page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if page or pageSize is less than 1.</exception>
    Task<List<ArticleSummaryDto>> GetPagedArticlesAsync(int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of articles for a specific feed.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="page">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The maximum number of articles to return per page.</param>
    /// <param name="status">Optional filter by article status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of article summaries for the specified feed and page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId, page, or pageSize is invalid.</exception>
    Task<List<ArticleSummaryDto>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default);

    #endregion

    #region State Change Operations

    /// <summary>
    /// Marks an article as read or unread.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="isRead">True to mark as read, false to mark as unread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the article's read status was successfully changed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<bool> MarkAsReadAsync(int articleId, bool isRead, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the starred (bookmarked) state of an article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the starred state was successfully toggled; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<bool> ToggleStarredAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the favorite state of an article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the favorite state was successfully toggled; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<bool> ToggleFavoriteAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an article as notified, indicating that a notification for this article has been sent.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the article was successfully marked as notified; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<bool> MarkAsNotifiedAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an article as processed by the rules engine.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the article was successfully marked as processed; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<bool> MarkAsProcessedAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all articles as read for a specific feed in a bulk operation.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles marked as read.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    Task<int> MarkAllAsReadByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all articles as read in a bulk operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles marked as read.</returns>
    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk marks multiple articles as processed by the rules engine.
    /// </summary>
    /// <param name="articleIds">The list of article IDs to mark as processed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully marked as processed.</returns>
    /// <exception cref="ArgumentNullException">Thrown if articleIds is null.</exception>
    Task<int> BulkMarkAsProcessedAsync(List<int> articleIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk marks multiple articles as notified.
    /// </summary>
    /// <param name="articleIds">The list of article IDs to mark as notified.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully marked as notified.</returns>
    /// <exception cref="ArgumentNullException">Thrown if articleIds is null.</exception>
    Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds, CancellationToken cancellationToken = default);

    #endregion

    #region Cleanup & Maintenance Operations

    /// <summary>
    /// Deletes articles older than the specified cutoff date.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date; articles older than this will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully deleted.</returns>
    /// <remarks>
    /// This operation protects starred and favorite articles from deletion.
    /// </remarks>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes articles older than the specified number of days.
    /// </summary>
    /// <param name="daysOld">The minimum age in days for an article to be eligible for deletion (default: 30).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully deleted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if daysOld is negative.</exception>
    Task<int> DeleteOldArticlesAsync(int daysOld = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all articles for a specific feed.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of articles successfully deleted.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    Task<int> DeleteByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    #endregion

    #region Statistics & Counters

    /// <summary>
    /// Gets the total number of unread articles across all feeds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of unread articles.</returns>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of unread articles for a specific feed.
    /// </summary>
    /// <param name="feedId">The unique identifier of the feed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of unread articles in the specified feed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if feedId is less than or equal to 0.</exception>
    Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unread counts for all feeds, grouped by feed ID.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping feed IDs to unread counts.</returns>
    Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Advanced / Filtered Reads

    /// <summary>
    /// Retrieves articles that have not yet been processed by the rules engine.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unprocessed article summaries.</returns>
    Task<List<ArticleSummaryDto>> GetUnprocessedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles that have not yet been notified to the user.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of unnotified article summaries.</returns>
    Task<List<ArticleSummaryDto>> GetUnnotifiedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves articles belonging to one or more specified categories.
    /// </summary>
    /// <param name="categories">A comma-separated string of category IDs or names.</param>
    /// <param name="limit">Maximum number of articles to return. Default: 100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching article summaries.</returns>
    /// <exception cref="ArgumentException">Thrown if categories is null or whitespace.</exception>
    Task<List<ArticleSummaryDto>> GetArticlesByCategoriesAsync(string categories, int limit = 100, CancellationToken cancellationToken = default);

    #endregion

    #region Duplicate Detection

    /// <summary>
    /// Retrieves an article by its unique GUID.
    /// </summary>
    /// <param name="guid">The GUID of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article detail if found; otherwise, null.</returns>
    /// <exception cref="ArgumentException">Thrown if guid is null or whitespace.</exception>
    Task<ArticleDetailDto?> GetByGuidAsync(string guid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an article by its content hash.
    /// </summary>
    /// <param name="hash">The content hash of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The article detail if found; otherwise, null.</returns>
    /// <exception cref="ArgumentException">Thrown if hash is null or whitespace.</exception>
    Task<ArticleDetailDto?> GetByContentHashAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an article with the specified GUID exists.
    /// </summary>
    /// <param name="guid">The GUID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an article with this GUID exists; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown if guid is null or whitespace.</exception>
    Task<bool> ExistsByGuidAsync(string guid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an article with the specified content hash exists.
    /// </summary>
    /// <param name="hash">The content hash to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an article with this content hash exists; otherwise, false.</returns>
    /// <exception cref="ArgumentException">Thrown if hash is null or whitespace.</exception>
    Task<bool> ExistsByContentHashAsync(string hash, CancellationToken cancellationToken = default);

    #endregion

    #region Entity Management

    /// <summary>
    /// Detaches an article entity from the change tracker.
    /// </summary>
    /// <param name="id">The ID of the article to detach.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DetachEntityAsync(int id, CancellationToken cancellationToken = default);

    #endregion
}