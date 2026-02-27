using NeonSuit.RSSReader.Core.Models;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing <see cref="Feed"/> entities.
    /// Provides comprehensive feed operations with support for active/inactive filtering, health tracking, and optimized data retrieval.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository handles all feed-related data access including:
    /// <list type="bullet">
    /// <item><description>CRUD operations with active/inactive filtering</description></item>
    /// <item><description>Health monitoring (failure counts, error tracking)</description></item>
    /// <item><description>Synchronization state (last update, next schedule)</description></item>
    /// <item><description>Category-based organization and grouping</description></item>
    /// <item><description>Bulk operations for performance optimization</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Performance optimizations:
    /// <list type="bullet">
    /// <item><description>All read methods use <c>AsNoTracking()</c> for memory efficiency</description></item>
    /// <item><description>Bulk operations use <c>ExecuteUpdateAsync</c>/<c>ExecuteDeleteAsync</c></description></item>
    /// <item><description>Server-side filtering with <c>Expression</c> predicates</description></item>
    /// <item><description>Proper cancellation token support for long operations</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IFeedRepository : IRepository<Feed>
    {
        #region CRUD Operations

        /// <summary>
        /// Retrieves a feed by its unique identifier with optional inactive inclusion.
        /// </summary>
        /// <param name="id">The unique identifier of the feed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The feed if found and meets filter criteria; otherwise, null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if id is less than or equal to 0.</exception>
        new Task<Feed?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a feed by ID without change tracking (read-only).
        /// </summary>
        /// <param name="id">The unique identifier of the feed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The feed entity or null if not found.</returns>
        Task<Feed?> GetByIdNoTrackingAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all feeds, optionally including inactive ones.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all feeds matching the filter criteria.</returns>
        Task<List<Feed>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds matching the specified predicate with server-side evaluation.
        /// </summary>
        /// <param name="predicate">Filter expression for server-side evaluation.</param>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of matching feeds.</returns>
        Task<List<Feed>> GetWhereAsync(Expression<Func<Feed, bool>> predicate, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts feeds matching the specified predicate with server-side evaluation.
        /// </summary>
        /// <param name="predicate">Filter expression for server-side evaluation.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in count. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Count of matching feeds.</returns>
        Task<int> CountWhereAsync(Expression<Func<Feed, bool>> predicate, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Detaches a feed entity from the change tracker.
        /// </summary>
        /// <param name="id">The ID of the feed to detach. Use 0 to detach all tracked feeds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DetachEntityAsync(int id, CancellationToken cancellationToken = default);

        #endregion

        #region Read Single Entity Operations

        /// <summary>
        /// Finds a feed by its URL.
        /// </summary>
        /// <param name="url">The URL of the feed.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The feed if found; otherwise, null.</returns>
        Task<Feed?> GetByUrlAsync(string url, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a feed with the specified URL already exists.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the feed exists; otherwise, false.</returns>
        Task<bool> ExistsByUrlAsync(string url, CancellationToken cancellationToken = default);

        #endregion

        #region Read Collection Operations

        /// <summary>
        /// Retrieves feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of feeds in the category.</returns>
        Task<List<Feed>> GetByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds that are not assigned to any category.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of uncategorized feeds.</returns>
        Task<List<Feed>> GetUncategorizedAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all active feeds.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active feeds.</returns>
        Task<List<Feed>> GetActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all inactive feeds.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of inactive feeds.</returns>
        Task<List<Feed>> GetInactiveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds that are due for an update based on their schedule.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of feeds ready for update.</returns>
        Task<List<Feed>> GetFeedsToUpdateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets feed counts grouped by category.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping category IDs to feed counts.</returns>
        Task<Dictionary<int, int>> GetCountByCategoryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all feeds grouped by their CategoryId.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary mapping CategoryId to lists of feeds.</returns>
        Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds belonging to a specific category, ordered by title.
        /// </summary>
        /// <param name="categoryId">The category ID.</param>
        /// <param name="includeInactive">If true, includes inactive feeds. Default is false.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered list of feeds in the category.</returns>
        Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds with specific retention days configured.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of feeds with custom retention settings.</returns>
        Task<List<Feed>> GetFeedsWithRetentionAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Bulk / Direct Update Operations

        /// <summary>
        /// Deletes a feed directly using efficient bulk delete.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> DeleteFeedDirectAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the LastUpdated timestamp for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> UpdateLastUpdatedAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the NextUpdateSchedule for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="nextUpdate">The new next update schedule.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> UpdateNextUpdateScheduleAsync(int feedId, DateTime nextUpdate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates article counts for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="totalCount">New total article count.</param>
        /// <param name="unreadCount">New unread article count.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> UpdateArticleCountsAsync(int feedId, int totalCount, int unreadCount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">New active status.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> SetActiveStatusAsync(int feedId, bool isActive, CancellationToken cancellationToken = default);

        #endregion

        #region Health and Status Operations

        /// <summary>
        /// Retrieves feeds with failure count above threshold.
        /// </summary>
        /// <param name="maxFailureCount">Maximum allowed failures (default: 3).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of failed feeds.</returns>
        Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds with zero failures.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of healthy feeds.</returns>
        Task<List<Feed>> GetHealthyFeedsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Increments the failure count for a feed and records error message.
        /// </summary>
        /// <param name="feedId">Feed ID.</param>
        /// <param name="errorMessage">Optional error message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> IncrementFailureCountAsync(int feedId, string? errorMessage = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the failure count to zero for a feed.
        /// </summary>
        /// <param name="feedId">Feed ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> ResetFailureCountAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple health status fields in a single operation.
        /// </summary>
        /// <param name="feedId">Feed ID.</param>
        /// <param name="lastUpdated">Last updated timestamp.</param>
        /// <param name="failureCount">New failure count.</param>
        /// <param name="lastError">New error message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected.</returns>
        Task<int> UpdateHealthStatusAsync(int feedId, DateTime? lastUpdated, int failureCount, string? lastError, CancellationToken cancellationToken = default);

        #endregion

        #region Search Operations

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">Text to search for.</param>
        /// <param name="includeInactive">Whether to include inactive feeds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of matching feeds.</returns>
        Task<List<Feed>> SearchAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default);

        #endregion
    }
}