using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
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
    ///     <item>All write operations (insert, update, delete, bulk status changes) should be atomic and transactional where appropriate.</item>
    ///     <item>Read operations should prefer efficient queries (indexed columns like FeedId, Status, Guid, ContentHash).</item>
    ///     <item>Existence and count checks must avoid loading full entities (use scalar queries).</item>
    ///     <item>Bulk operations are optimized for sync/import/cleanup scenarios and should minimize round-trips.</item>
    ///     <item>Methods that affect unread counters should trigger recalculation or event raising in the implementation.</item>
    ///     <item>Search and pagination support basic filtering; advanced full-text search may require extensions.</item>
    /// </list>
    ///
    /// <para>
    /// Refactoring note (pending):
    /// </para>
    /// <para>
    /// The interface currently contains ~40 methods, mixing concerns (CRUD, queries, commands, aggregates, maintenance).
    /// Consider splitting in the future into more focused interfaces:
    /// </para>
    /// <list type="bullet">
    ///     <item><c>IArticleQueryRepository</c> → read-only operations, search, pagination, counters</item>
    ///     <item><c>IArticleCommandRepository</c> → insert/update/delete, status changes, bulk marks</item>
    ///     <item><c>IArticleStatisticsRepository</c> → unread counts and aggregates</item>
    ///     <item><c>IArticleMaintenanceRepository</c> → cleanup/delete operations</item>
    /// </list>
    /// <para>
    /// For now, the single interface is acceptable but monitor growth.
    /// </para>
    /// </remarks>
    public interface IArticleRepository
    {
        #region Basic CRUD Operations

        /// <summary>
        /// Retrieves a single article by its primary key (database ID).
        /// </summary>
        /// <param name="id">The unique identifier of the article.</param>
        /// <returns>The article if found; otherwise null.</returns>
        /// <remarks>
        /// Used for detail views or when only the ID is known.
        /// Returns tracked entity by default (EF Core change tracking).
        /// </remarks>
        Task<Article?> GetByIdAsync(int id);

        /// <summary>
        /// Detaches an article from the change tracker to prevent unintended updates.
        /// </summary>
        /// <param name="id">The ID of the article to detach.</param>
        /// <remarks>
        /// Useful after read-only queries to avoid accidental persistence of modified read-only entities.
        /// Has no effect if the entity was not previously attached/tracked.
        /// </remarks>
        Task DetachEntityAsync(int id);

        /// <summary>
        /// Retrieves all articles in the database (use with caution on large datasets).
        /// </summary>
        /// <returns>List of all articles (potentially large).</returns>
        /// <remarks>
        /// Primarily for administrative/debug purposes or small datasets.
        /// Avoid in production code without pagination or filtering.
        /// </remarks>
        Task<List<Article>> GetAllAsync();

        /// <summary>
        /// Filters articles using a client-side predicate (executed in memory).
        /// </summary>
        /// <param name="predicate">Lambda expression to filter articles.</param>
        /// <returns>List of matching articles.</returns>
        /// <remarks>
        /// Not recommended for large datasets (loads all data first).
        /// Prefer expression-based queries or dedicated methods for performance.
        /// </remarks>
        Task<List<Article>> GetWhereAsync(Func<Article, bool> predicate);

        /// <summary>
        /// Inserts a new article and returns the generated database ID.
        /// </summary>
        /// <param name="entity">The article to insert (must have FeedId set).</param>
        /// <returns>The new ID assigned by the database.</returns>
        /// <remarks>
        /// Performs validation and sets creation timestamps if needed.
        /// Throws if duplicate GUID/content hash exists (depending on implementation).
        /// </remarks>
        Task<int> InsertAsync(Article entity);

        /// <summary>
        /// Bulk inserts multiple articles in a single operation.
        /// </summary>
        /// <param name="entities">List of articles to insert (must have FeedId set).</param>
        /// <returns>Number of articles successfully inserted.</returns>
        /// <remarks>
        /// Optimized for feed sync/import scenarios.
        /// Should use transaction and batching internally.
        /// May skip duplicates based on GUID/hash (implementation-dependent).
        /// </remarks>
        Task<int> InsertAllAsync(List<Article> entities);

        /// <summary>
        /// Updates an existing article with new values.
        /// </summary>
        /// <param name="entity">The modified article (must have valid ID).</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Only updates changed properties if change tracking is enabled.
        /// Throws if entity not found or concurrency conflict occurs.
        /// </remarks>
        Task<int> UpdateAsync(Article entity);

        /// <summary>
        /// Bulk updates multiple existing articles.
        /// </summary>
        /// <param name="entities">List of modified articles.</param>
        /// <returns>Number of articles successfully updated.</returns>
        /// <remarks>
        /// Use for batch status changes or metadata corrections.
        /// Transactional and efficient where possible.
        /// </remarks>
        Task<int> UpdateAllAsync(List<Article> entities);

        /// <summary>
        /// Deletes a single article by entity reference.
        /// </summary>
        /// <param name="entity">The article to delete.</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Cascades to related data if configured (e.g., tags, notifications).
        /// Soft-delete preferred in some implementations (status → Archived).
        /// </remarks>
        Task<int> DeleteAsync(Article entity);

        /// <summary>
        /// Counts articles matching a client-side predicate.
        /// </summary>
        /// <param name="predicate">Lambda to filter articles.</param>
        /// <returns>Count of matching articles.</returns>
        /// <remarks>
        /// Loads all data first — inefficient for large sets.
        /// Prefer dedicated count methods.
        /// </remarks>
        Task<int> CountWhereAsync(Func<Article, bool> predicate);

        #endregion

        #region Specialized Retrieval

        /// <summary>
        /// Retrieves the most recent articles from a specific feed.
        /// </summary>
        /// <param name="feedId">ID of the feed.</param>
        /// <param name="limit">Maximum number of articles to return (default 100).</param>
        /// <returns>List of articles ordered by publication date descending.</returns>
        /// <remarks>
        /// Used for feed detail views and infinite scrolling.
        /// Ordered newest first.
        /// </remarks>
        Task<List<Article>> GetByFeedAsync(int feedId, int limit = 100);

        /// <summary>
        /// Retrieves articles from multiple feeds (union).
        /// </summary>
        /// <param name="feedIds">List of feed IDs to include.</param>
        /// <param name="limit">Maximum number to return.</param>
        /// <returns>Combined list ordered by date.</returns>
        /// <remarks>
        /// Useful for category views or multi-feed dashboards.
        /// </remarks>
        Task<List<Article>> GetByFeedsAsync(List<int> feedIds, int limit = 100);

        /// <summary>
        /// Gets unread articles across all feeds.
        /// </summary>
        /// <param name="limit">Maximum number to return.</param>
        /// <returns>Unread articles, newest first.</returns>
        Task<List<Article>> GetUnreadAsync(int limit = 100);

        /// <summary>
        /// Retrieves all currently starred articles.
        /// </summary>
        /// <returns>List of starred articles.</returns>
        /// <remarks>
        /// Used for "Favorites" or "Starred" view.
        /// May include archived if business rule allows.
        /// </remarks>
        Task<List<Article>> GetStarredAsync();

        /// <summary>
        /// Retrieves all currently favorited articles (alias for starred in some implementations).
        /// </summary>
        /// <returns>List of favorited articles.</returns>
        /// <remarks>
        /// If Favorite and Starred are the same, prefer GetStarredAsync.
        /// </remarks>
        Task<List<Article>> GetFavoritesAsync();

        /// <summary>
        /// Gets unread articles for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed to query.</param>
        /// <returns>Unread articles in the feed, newest first.</returns>
        Task<List<Article>> GetUnreadByFeedAsync(int feedId);

        #endregion

        #region Duplicate Detection & Lookup

        /// <summary>
        /// Finds an existing article by its syndication GUID.
        /// </summary>
        /// <param name="guid">The unique GUID from the feed item.</param>
        /// <returns>The matching article or null.</returns>
        /// <remarks>
        /// Primary method for duplicate detection during sync.
        /// GUID should be normalized (trimmed, lowercased if needed).
        /// </remarks>
        Task<Article?> GetByGuidAsync(string guid);

        /// <summary>
        /// Finds an article by computed content hash (fallback duplicate check).
        /// </summary>
        /// <param name="hash">SHA256 or similar hash of title + content.</param>
        /// <returns>The matching article or null.</returns>
        /// <remarks>
        /// Used when GUID is missing or unreliable.
        /// Hash algorithm must be consistent across syncs.
        /// </remarks>
        Task<Article?> GetByContentHashAsync(string hash);

        /// <summary>
        /// Checks if an article with the given GUID already exists.
        /// </summary>
        Task<bool> ExistsByGuidAsync(string guid);

        /// <summary>
        /// Checks if an article with the given content hash already exists.
        /// </summary>
        Task<bool> ExistsByContentHashAsync(string hash);

        #endregion

        #region Status & Toggle Operations

        /// <summary>
        /// Changes the status of a single article (e.g., Unread → Read).
        /// </summary>
        /// <param name="articleId">ID of the article to update.</param>
        /// <param name="status">New status to set.</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Triggers counter updates and potential events.
        /// Safe to call multiple times (idempotent for same status).
        /// </remarks>
        Task<int> MarkAsAsync(int articleId, ArticleStatus status);

        /// <summary>
        /// Toggles the starred/favorited state of an article.
        /// </summary>
        /// <param name="articleId">ID of the article.</param>
        /// <returns>1 if toggled successfully.</returns>
        /// <remarks>
        /// Switches between Starred ↔ previous status (usually Read/Unread).
        /// Updates UI badges and starred view eligibility.
        /// </remarks>
        Task<int> ToggleStarAsync(int articleId);

        /// <summary>
        /// Toggles the favorite state (if separate from starred).
        /// </summary>
        /// <param name="articleId">ID of the article.</param>
        /// <returns>1 if toggled successfully.</returns>
        /// <remarks>
        /// Alias or alternative to ToggleStarAsync depending on model.
        /// </remarks>
        Task<int> ToggleFavoriteAsync(int articleId);

        /// <summary>
        /// Marks all articles in a feed as read in one operation.
        /// </summary>
        /// <param name="feedId">ID of the feed to mark as read.</param>
        /// <returns>Number of articles updated to Read.</returns>
        /// <remarks>
        /// Efficient bulk UPDATE.
        /// Updates global and per-feed unread counters.
        /// Does not affect Starred/Archived unless configured.
        /// </remarks>
        Task<int> MarkAllAsReadByFeedAsync(int feedId);

        /// <summary>
        /// Marks all unread articles across all feeds as read.
        /// </summary>
        /// <returns>Number of articles updated.</returns>
        /// <remarks>
        /// Global "Mark all as read" action.
        /// Updates all counters to zero.
        /// Expensive on very large datasets — use with care.
        /// </remarks>
        Task<int> MarkAllAsReadAsync();

        #endregion

        #region Counters & Aggregates

        /// <summary>
        /// Gets the total number of unread articles across all feeds.
        /// </summary>
        /// <returns>Current unread count (for global badge).</returns>
        /// <remarks>
        /// Should be fast (indexed query or cached).
        /// Used for app badge, dashboard, etc.
        /// </remarks>
        Task<int> GetUnreadCountAsync();

        /// <summary>
        /// Gets the unread count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed to count.</param>
        /// <returns>Number of unread articles in the feed.</returns>
        /// <remarks>
        /// Used for per-feed badges in feed list.
        /// </remarks>
        Task<int> GetUnreadCountByFeedAsync(int feedId);

        /// <summary>
        /// Gets unread counts for all feeds in a single query.
        /// </summary>
        /// <returns>Dictionary of FeedId → unread count.</returns>
        /// <remarks>
        /// Efficient for refreshing entire feed list badges.
        /// Only includes feeds with unread > 0 (or all, implementation-dependent).
        /// </remarks>
        Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync();

        #endregion

        #region Search & Advanced Filtering

        /// <summary>
        /// Searches articles by keyword across title, content, author, etc.
        /// </summary>
        /// <param name="searchText">Search term or phrase.</param>
        /// <returns>List of matching articles (relevance ordered if possible).</returns>
        /// <remarks>
        /// May use LIKE '%term%' or full-text search.
        /// Performance varies — limit results internally.
        /// </remarks>
        Task<List<Article>> SearchAsync(string searchText);

        /// <summary>
        /// Retrieves articles matching one or more categories/tags.
        /// </summary>
        /// <param name="categories">Comma-separated or space-separated categories.</param>
        /// <param name="limit">Maximum results to return.</param>
        /// <returns>Matching articles.</returns>
        /// <remarks>
        /// Supports tag-based filtering.
        /// Parsing of <paramref name="categories"/> is implementation-specific.
        /// </remarks>
        Task<List<Article>> GetByCategoriesAsync(string categories, int limit = 100);

        #endregion

        #region Processing & Notification Workflow

        /// <summary>
        /// Gets articles that have not yet been processed (e.g., full-text extraction pending).
        /// </summary>
        /// <param name="limit">Maximum to return.</param>
        /// <returns>Unprocessed articles (oldest first usually).</returns>
        /// <remarks>
        /// Used by background workers for deferred processing.
        /// </remarks>
        Task<List<Article>> GetUnprocessedArticlesAsync(int limit = 100);

        /// <summary>
        /// Gets articles that have not yet triggered a notification.
        /// </summary>
        /// <param name="limit">Maximum to return.</param>
        /// <returns>Unnotified articles eligible for alerting.</returns>
        /// <remarks>
        /// Used by notification service.
        /// </remarks>
        Task<List<Article>> GetUnnotifiedArticlesAsync(int limit = 100);

        /// <summary>
        /// Marks a single article as processed.
        /// </summary>
        Task<int> MarkAsProcessedAsync(int articleId);

        /// <summary>
        /// Marks a single article as having sent its notification.
        /// </summary>
        Task<int> MarkAsNotifiedAsync(int articleId);

        /// <summary>
        /// Bulk marks multiple articles as processed.
        /// </summary>
        Task<int> BulkMarkAsProcessedAsync(List<int> articleIds);

        /// <summary>
        /// Bulk marks multiple articles as notified.
        /// </summary>
        Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds);

        #endregion

        #region Cleanup Operations

        /// <summary>
        /// Deletes articles older than the specified cutoff date.
        /// </summary>
        /// <param name="date">Articles published before this date are deleted.</param>
        /// <returns>Number of articles deleted.</returns>
        /// <remarks>
        /// Used for retention policy enforcement.
        /// May exclude Starred/Archived depending on rules.
        /// Transactional and logged.
        /// </remarks>
        Task<int> DeleteOlderThanAsync(DateTime date);

        /// <summary>
        /// Deletes all articles belonging to a specific feed.
        /// </summary>
        /// <param name="feedId">The feed to clear.</param>
        /// <returns>Number of articles deleted.</returns>
        /// <remarks>
        /// Called during unsubscribe or feed removal.
        /// Cascades to related data.
        /// Updates counters.
        /// </remarks>
        Task<int> DeleteByFeedAsync(int feedId);

        #endregion

        #region Pagination Support

        /// <summary>
        /// Retrieves a page of articles with optional status filter.
        /// </summary>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page.</param>
        /// <param name="status">Optional status filter.</param>
        /// <returns>Paged list of articles (ordered by date desc).</returns>
        /// <remarks>
        /// Used for infinite scroll or list views.
        /// Total count may be separate call.
        /// </remarks>
        Task<List<Article>> GetPagedAsync(int page, int pageSize, ArticleStatus? status = null);

        /// <summary>
        /// Retrieves a page of articles from a specific feed with status filter.
        /// </summary>
        /// <param name="feedId">The feed to paginate.</param>
        /// <param name="page">Page number.</param>
        /// <param name="pageSize">Items per page.</param>
        /// <param name="status">Optional status filter.</param>
        /// <returns>Paged articles from the feed.</returns>
        /// <remarks>
        /// Feed-specific paginated view.
        /// </remarks>
        Task<List<Article>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null);

        #endregion
    }
}