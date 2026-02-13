using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    public interface IFeedService
    {
        // Basic CRUD operations
        Task<List<Feed>> GetAllFeedsAsync();
        Task<Feed?> GetFeedByIdAsync(int id);
        Task<Feed> AddFeedAsync(string url, int? categoryId = null);
        Task<bool> UpdateFeedAsync(Feed feed);
        Task<bool> DeleteFeedAsync(int feedId);
        Task<bool> FeedExistsAsync(string url);
        

        /// <summary>
        /// Retrieves a feed by its URL.
        /// </summary>
        /// <param name="url">The feed URL to search for.</param>
        /// <returns>The Feed object or null if not found.</returns>
        Task<Feed?> GetFeedByUrlAsync(string url);

        /// <summary>
        /// Creates a new feed with the specified properties.
        /// </summary>
        /// <param name="feed">The feed to create.</param>
        /// <returns>The ID of the newly created feed.</returns>
        Task<int> CreateFeedAsync(Feed feed);

        // Feed refresh and synchronization
        Task<bool> RefreshFeedAsync(int feedId);
        Task<int> RefreshAllFeedsAsync();
        Task<int> RefreshFeedsByCategoryAsync(int categoryId);

        // Feed management
        Task<bool> SetFeedActiveStatusAsync(int feedId, bool isActive);
        Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId);
        Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays);

        // Health and monitoring
        Task<Dictionary<int, int>> GetUnreadCountsAsync();
        Task<Dictionary<int, int>> GetArticleCountsAsync();
        Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3);
        Task<List<Feed>> GetHealthyFeedsAsync();
        Task<bool> ResetFeedFailuresAsync(int feedId);
        Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync();

        // Search and filtering
        Task<List<Feed>> SearchFeedsAsync(string searchText);
        Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId);
        Task<List<Feed>> GetUncategorizedFeedsAsync();

        // Feed properties
        Task<int> GetTotalArticleCountAsync(int feedId);
        Task<int> GetUnreadCountByFeedAsync(int feedId);
        Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null);

        // Maintenance
        Task<int> CleanupOldArticlesAsync();
        Task<int> UpdateAllFeedCountsAsync();
    }
}