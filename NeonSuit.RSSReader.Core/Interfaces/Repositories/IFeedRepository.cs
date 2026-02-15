using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Professional repository interface for RSS feed data access.
    /// Provides comprehensive feed operations with support for active/inactive filtering.
    /// </summary>
    public interface IFeedRepository
    {
        #region CRUD Operations

        /// <summary>
        /// Retrieves a feed by its unique identifier.
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <returns>The feed if found and meets filter criteria; otherwise, null.</returns>
        Task<Feed?> GetByIdAsync(int id, bool includeInactive = false);

        /// <summary>
        /// Detaches a feed entity from the change tracker to prevent tracking conflicts.
        /// </summary>
        /// <param name="id">The ID of the feed to detach (use 0 to detach all feeds).</param>
        Task DetachEntityAsync(int id);

        /// <summary>
        /// Retrieves all feeds from the database.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of all feeds matching the filter criteria.</returns>
        Task<List<Feed>> GetAllAsync(bool includeInactive = false);

        /// <summary>
        /// Retrieves feeds that match the specified predicate (client-side evaluation).
        /// </summary>
        /// <param name="predicate">The filter predicate.</param>
        /// <returns>A list of matching feeds.</returns>
        Task<List<Feed>> GetWhereAsync(Func<Feed, bool> predicate);

        /// <summary>
        /// Inserts a new feed into the database.
        /// </summary>
        /// <param name="entity">The feed to insert.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> InsertAsync(Feed entity);

        /// <summary>
        /// Updates an existing feed in the database.
        /// </summary>
        /// <param name="entity">The feed with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateAsync(Feed entity);

        /// <summary>
        /// Deletes a feed from the database.
        /// </summary>
        /// <param name="entity">The feed to delete.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteAsync(Feed entity);

        /// <summary>
        /// Counts feeds that match the specified predicate (client-side evaluation).
        /// </summary>
        /// <param name="predicate">The filter predicate.</param>
        /// <returns>The count of matching feeds.</returns>
        Task<int> CountWhereAsync(Func<Feed, bool> predicate);

        #endregion

        #region Feed-specific Operations

        /// <summary>
        /// Retrieves feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of feeds in the category.</returns>
        Task<List<Feed>> GetByCategoryAsync(int categoryId, bool includeInactive = false);

        /// <summary>
        /// Retrieves feeds that are not assigned to any category.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of uncategorized feeds.</returns>
        Task<List<Feed>> GetUncategorizedAsync(bool includeInactive = false);

        /// <summary>
        /// Retrieves all active feeds.
        /// </summary>
        /// <returns>A list of active feeds.</returns>
        Task<List<Feed>> GetActiveAsync();

        /// <summary>
        /// Retrieves all inactive feeds.
        /// </summary>
        /// <returns>A list of inactive feeds.</returns>
        Task<List<Feed>> GetInactiveAsync();

        /// <summary>
        /// Finds a feed by its URL.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <returns>The feed if found and meets filter criteria; otherwise, null.</returns>
        Task<Feed?> GetByUrlAsync(string url, bool includeInactive = false);

        /// <summary>
        /// Retrieves a feed by ID without change tracking (read-only).
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <returns>The feed or null if not found.</returns>
        Task<Feed?> GetByIdNoTrackingAsync(int id);

        /// <summary>
        /// Deletes a feed directly using SQL (bypasses change tracker).
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteFeedDirectAsync(int feedId);

        /// <summary>
        /// Checks if a feed with the specified URL already exists.
        /// </summary>
        /// <param name="url">The feed URL.</param>
        /// <returns>True if the feed exists; otherwise, false.</returns>
        Task<bool> ExistsByUrlAsync(string url);

        /// <summary>
        /// Retrieves feeds that are due for an update based on their frequency.
        /// </summary>
        /// <returns>A list of feeds ready for update.</returns>
        Task<List<Feed>> GetFeedsToUpdateAsync();

        /// <summary>
        /// Updates the LastUpdated timestamp for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateLastUpdatedAsync(int feedId);

        /// <summary>
        /// Updates the NextUpdateSchedule for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="nextUpdate">The next update schedule time.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateNextUpdateScheduleAsync(int feedId, DateTime nextUpdate);

        /// <summary>
        /// Gets the count of feeds grouped by category.
        /// </summary>
        /// <returns>A dictionary mapping category IDs to feed counts.</returns>
        Task<Dictionary<int, int>> GetCountByCategoryAsync();

        #endregion

        #region Health and Status Methods

        /// <summary>
        /// Retrieves feeds with failure count above threshold.
        /// </summary>
        /// <param name="maxFailureCount">The maximum allowed failure count. Default: 3.</param>
        /// <returns>A list of failed feeds.</returns>
        Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3);

        /// <summary>
        /// Retrieves feeds with zero failures.
        /// </summary>
        /// <returns>A list of healthy feeds.</returns>
        Task<List<Feed>> GetHealthyFeedsAsync();

        /// <summary>
        /// Increments failure count and records error message for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="errorMessage">Optional error message to record.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> IncrementFailureCountAsync(int feedId, string? errorMessage = null);

        /// <summary>
        /// Resets failure count to zero for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> ResetFailureCountAsync(int feedId);

        /// <summary>
        /// Updates health status fields in one operation.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="lastUpdated">The last updated timestamp.</param>
        /// <param name="failureCount">The failure count.</param>
        /// <param name="lastError">The last error message.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateHealthStatusAsync(int feedId, DateTime? lastUpdated, int failureCount, string? lastError);

        #endregion

        #region Retention and Cleanup

        /// <summary>
        /// Retrieves feeds with specific retention days configured.
        /// </summary>
        /// <returns>A list of feeds with retention settings.</returns>
        Task<List<Feed>> GetFeedsWithRetentionAsync();

        /// <summary>
        /// Updates article counts for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="totalCount">The total article count.</param>
        /// <param name="unreadCount">The unread article count.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateArticleCountsAsync(int feedId, int totalCount, int unreadCount);

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">The new active status.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> SetActiveStatusAsync(int feedId, bool isActive);

        #endregion

        #region Search and Filtering

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in search results. Default: false.</param>
        /// <returns>A list of matching feeds.</returns>
        Task<List<Feed>> SearchAsync(string searchText, bool includeInactive = false);

        /// <summary>
        /// Retrieves all feeds grouped by their CategoryId.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>Dictionary mapping CategoryId to list of feeds in that category.</returns>
        Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync(bool includeInactive = false);

        /// <summary>
        /// Retrieves all feeds belonging to a specific category, ordered by title.
        /// </summary>
        /// <param name="categoryId">The ID of the category.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>List of feeds in the specified category.</returns>
        Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false);

        #endregion
    }
}