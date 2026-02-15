using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Professional service interface for RSS feed management.
    /// Provides comprehensive feed operations with support for active/inactive filtering.
    /// </summary>
    public interface IFeedService
    {
        #region Basic CRUD Operations

        /// <summary>
        /// Retrieves all feeds from the system.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of all feeds matching the filter criteria.</returns>
        Task<List<Feed>> GetAllFeedsAsync(bool includeInactive = false);

        /// <summary>
        /// Retrieves a specific feed by its unique identifier.
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <returns>The feed if found and meets filter criteria; otherwise, null.</returns>
        Task<Feed?> GetFeedByIdAsync(int id, bool includeInactive = false);

        /// <summary>
        /// Adds a new feed to the system by parsing it from the provided URL.
        /// </summary>
        /// <param name="url">The feed URL to add.</param>
        /// <param name="categoryId">Optional category identifier to assign the feed to.</param>
        /// <returns>The newly created feed with generated ID and metadata.</returns>
        /// <exception cref="InvalidOperationException">Thrown when feed already exists or cannot be parsed.</exception>
        Task<Feed> AddFeedAsync(string url, int? categoryId = null);

        /// <summary>
        /// Updates an existing feed with modified properties.
        /// </summary>
        /// <param name="feed">The feed entity with updated values.</param>
        /// <returns>True if update succeeded; otherwise, false.</returns>
        Task<bool> UpdateFeedAsync(Feed feed);

        /// <summary>
        /// Deletes a feed and all its associated articles from the system.
        /// </summary>
        /// <param name="feedId">The identifier of the feed to delete.</param>
        /// <returns>True if deletion succeeded; otherwise, false.</returns>
        Task<bool> DeleteFeedAsync(int feedId);

        /// <summary>
        /// Checks if a feed with the specified URL already exists in the system.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>True if a feed with this URL exists; otherwise, false.</returns>
        Task<bool> FeedExistsAsync(string url);

        /// <summary>
        /// Retrieves a feed by its URL.
        /// </summary>
        /// <param name="url">The feed URL to search for.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <returns>The feed if found and meets filter criteria; otherwise, null.</returns>
        Task<Feed?> GetFeedByUrlAsync(string url, bool includeInactive = false);

        /// <summary>
        /// Creates a new feed with the specified properties without parsing.
        /// </summary>
        /// <param name="feed">The feed to create.</param>
        /// <returns>The ID of the newly created feed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when feed is null.</exception>
        /// <exception cref="ArgumentException">Thrown when feed URL is empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when feed with same URL already exists.</exception>
        Task<int> CreateFeedAsync(Feed feed);

        #endregion

        #region Feed Refresh and Synchronization

        /// <summary>
        /// Refreshes a specific feed by downloading and parsing new articles.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>True if refresh succeeded; otherwise, false.</returns>
        Task<bool> RefreshFeedAsync(int feedId);

        /// <summary>
        /// Refreshes all active feeds that are due for update based on their frequency.
        /// </summary>
        /// <returns>The number of feeds successfully refreshed.</returns>
        Task<int> RefreshAllFeedsAsync();

        /// <summary>
        /// Refreshes all active feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>The number of feeds successfully refreshed.</returns>
        Task<int> RefreshFeedsByCategoryAsync(int categoryId);

        #endregion

        #region Feed Management

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">The new active status.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        Task<bool> SetFeedActiveStatusAsync(int feedId, bool isActive);

        /// <summary>
        /// Updates the category assignment of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="categoryId">The new category identifier (null for uncategorized).</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId);

        /// <summary>
        /// Updates the article retention days for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="retentionDays">The new retention days (null for default).</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays);

        #endregion

        #region Health and Monitoring

        /// <summary>
        /// Gets unread article counts grouped by feed.
        /// </summary>
        /// <returns>A dictionary mapping feed IDs to unread counts.</returns>
        Task<Dictionary<int, int>> GetUnreadCountsAsync();

        /// <summary>
        /// Gets total article counts grouped by feed.
        /// </summary>
        /// <returns>A dictionary mapping feed IDs to total article counts.</returns>
        Task<Dictionary<int, int>> GetArticleCountsAsync();

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
        /// Resets failure count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        Task<bool> ResetFeedFailuresAsync(int feedId);

        /// <summary>
        /// Gets feed health statistics grouped by status.
        /// </summary>
        /// <returns>A dictionary mapping health status to count of feeds.</returns>
        Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync();

        #endregion

        #region Search and Filtering

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in search results. Default: false.</param>
        /// <returns>A list of matching feeds.</returns>
        Task<List<Feed>> SearchFeedsAsync(string searchText, bool includeInactive = false);

        /// <summary>
        /// Retrieves feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of feeds in the category.</returns>
        Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false);

        /// <summary>
        /// Retrieves feeds that are not assigned to any category.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <returns>A list of uncategorized feeds.</returns>
        Task<List<Feed>> GetUncategorizedFeedsAsync(bool includeInactive = false);

        #endregion

        #region Feed Properties

        /// <summary>
        /// Gets the total article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The total article count.</returns>
        Task<int> GetTotalArticleCountAsync(int feedId);

        /// <summary>
        /// Gets the unread article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <returns>The unread article count.</returns>
        Task<int> GetUnreadCountByFeedAsync(int feedId);

        /// <summary>
        /// Updates specific properties of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="title">Optional new title.</param>
        /// <param name="description">Optional new description.</param>
        /// <param name="websiteUrl">Optional new website URL.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null);

        #endregion

        #region Maintenance

        /// <summary>
        /// Cleans up old articles based on feed retention settings.
        /// </summary>
        /// <returns>The total number of articles deleted.</returns>
        Task<int> CleanupOldArticlesAsync();

        /// <summary>
        /// Updates article counts for all feeds.
        /// </summary>
        /// <returns>The number of feeds updated.</returns>
        Task<int> UpdateAllFeedCountsAsync();

        #endregion
    }
}