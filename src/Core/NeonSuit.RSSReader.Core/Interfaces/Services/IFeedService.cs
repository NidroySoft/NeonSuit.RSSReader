using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for RSS feed management operations.
    /// Provides comprehensive feed management with support for active/inactive filtering,
    /// health monitoring, and synchronization capabilities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all feed-related business logic including:
    /// <list type="bullet">
    /// <item>Feed CRUD operations with active/inactive filtering</item>
    /// <item>Feed refresh and synchronization with configurable intervals</item>
    /// <item>Health monitoring with failure tracking and auto-disable</item>
    /// <item>Category assignment and management</item>
    /// <item>Article count tracking and retention policy enforcement</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// Implementations must ensure:
    /// <list type="bullet">
    /// <item>All read operations are memory-efficient using server-side filtering</item>
    /// <item>Refresh operations respect feed-specific intervals to avoid overloading sources</item>
    /// <item>Feed health monitoring automatically disables failing feeds after threshold</item>
    /// <item>All operations support cancellation via <see cref="CancellationToken"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface IFeedService
    {
        #region Basic CRUD Operations

        /// <summary>
        /// Retrieves all feeds from the system.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of all feeds matching the filter criteria.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedSummaryDto>> GetAllFeedsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific feed by its unique identifier.
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The feed DTO if found and meets filter criteria; otherwise, null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="id"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<FeedDto?> GetFeedByIdAsync(int id, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a new feed to the system by parsing it from the provided URL.
        /// </summary>
        /// <param name="createDto">The DTO containing feed creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The newly created feed DTO with generated ID and metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if URL is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when feed already exists or cannot be parsed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<FeedDto> AddFeedAsync(CreateFeedDto createDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing feed with modified properties.
        /// </summary>
        /// <param name="feedId">The ID of the feed to update.</param>
        /// <param name="updateDto">The DTO containing updated feed data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated feed DTO if successful; otherwise, null.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<FeedDto?> UpdateFeedAsync(int feedId, UpdateFeedDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a feed and all its associated articles from the system.
        /// </summary>
        /// <param name="feedId">The identifier of the feed to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if deletion succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> DeleteFeedAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a feed with the specified URL already exists in the system.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if a feed with this URL exists; otherwise, false.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="url"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> FeedExistsAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a feed by its URL.
        /// </summary>
        /// <param name="url">The feed URL to search for.</param>
        /// <param name="includeInactive">If true, allows retrieval of inactive feeds. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The feed DTO if found and meets filter criteria; otherwise, null.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="url"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<FeedDto?> GetFeedByUrlAsync(string url, bool includeInactive = false, CancellationToken cancellationToken = default);

        #endregion

        #region Feed Refresh and Synchronization

        /// <summary>
        /// Refreshes a specific feed by downloading and parsing new articles.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if refresh succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> RefreshFeedAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes all active feeds that are due for update based on their frequency.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of feeds successfully refreshed.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> RefreshAllFeedsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes all active feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of feeds successfully refreshed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> RefreshFeedsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

        #endregion

        #region Feed Management

        /// <summary>
        /// Sets the active status of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isActive">The new active status.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> SetFeedActiveStatusAsync(int feedId, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the category assignment of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="categoryId">The new category identifier (null for uncategorized).</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the article retention days for a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="retentionDays">The new retention days (null for default).</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="retentionDays"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays, CancellationToken cancellationToken = default);

        #endregion

        #region Health and Monitoring

        /// <summary>
        /// Gets unread article counts grouped by feed.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping feed IDs to unread counts.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, int>> GetUnreadCountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets total article counts grouped by feed.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping feed IDs to total article counts.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, int>> GetArticleCountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds with failure count above threshold.
        /// </summary>
        /// <param name="maxFailureCount">The maximum allowed failure count. Default: 3.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of failed feed health DTOs.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="maxFailureCount"/> is less than 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedHealthDto>> GetFailedFeedsAsync(int maxFailureCount = 3, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds with zero failures.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of healthy feed DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedSummaryDto>> GetHealthyFeedsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets failure count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the operation succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> ResetFeedFailuresAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets feed health statistics grouped by status.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping health status to count of feeds.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Search and Filtering

        /// <summary>
        /// Searches for feeds by title, description, or URL.
        /// </summary>
        /// <param name="searchText">The search text.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in search results. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of matching feed summary DTOs.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="searchText"/> is null or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedSummaryDto>> SearchFeedsAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The category identifier.</param>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of feed summary DTOs in the category.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedSummaryDto>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves feeds that are not assigned to any category.
        /// </summary>
        /// <param name="includeInactive">If true, includes inactive feeds in the result. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of uncategorized feed summary DTOs.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<FeedSummaryDto>> GetUncategorizedFeedsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

        #endregion

        #region Feed Properties

        /// <summary>
        /// Gets the total article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The total article count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> GetTotalArticleCountAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the unread article count for a specific feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The unread article count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates specific properties of a feed.
        /// </summary>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="title">Optional new title.</param>
        /// <param name="description">Optional new description.</param>
        /// <param name="websiteUrl">Optional new website URL.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if the update succeeded; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="feedId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null, CancellationToken cancellationToken = default);

        #endregion

        #region Maintenance

        /// <summary>
        /// Cleans up old articles based on feed retention settings.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The total number of articles deleted.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> CleanupOldArticlesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates article counts for all feeds.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The number of feeds updated.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<int> UpdateAllFeedCountsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}