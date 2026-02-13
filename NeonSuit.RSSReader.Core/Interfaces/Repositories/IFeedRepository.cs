using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    public interface IFeedRepository
    {
        // CRUD Operations
        Task<Feed?> GetByIdAsync(int id);
        Task DetachEntityAsync(int id);
        Task<List<Feed>> GetAllAsync();
        Task<List<Feed>> GetWhereAsync(Func<Feed, bool> predicate);
        Task<int> InsertAsync(Feed entity);
        Task<int> UpdateAsync(Feed entity);
        Task<int> DeleteAsync(Feed entity);
        Task<int> CountWhereAsync(Func<Feed, bool> predicate);

        // Feed-specific operations
        Task<List<Feed>> GetByCategoryAsync(int categoryId);
        Task<List<Feed>> GetUncategorizedAsync();
        Task<List<Feed>> GetActiveAsync();
        Task<List<Feed>> GetInactiveAsync();
        Task<Feed?> GetByUrlAsync(string url);
        Task<Feed?> GetByIdNoTrackingAsync(int id);
        Task<int> DeleteFeedDirectAsync(int feedId);
        Task<bool> ExistsByUrlAsync(string url);
        Task<List<Feed>> GetFeedsToUpdateAsync();
        Task<int> UpdateLastUpdatedAsync(int feedId);
        Task<int> UpdateNextUpdateScheduleAsync(int feedId, DateTime nextUpdate);
        Task<Dictionary<int, int>> GetCountByCategoryAsync();

        // Health and status methods
        Task<List<Feed>> GetFailedFeedsAsync(int maxFailureCount = 3);
        Task<List<Feed>> GetHealthyFeedsAsync();
        Task<int> IncrementFailureCountAsync(int feedId, string? errorMessage = null);
        Task<int> ResetFailureCountAsync(int feedId);
        Task<int> UpdateHealthStatusAsync(int feedId, DateTime? lastUpdated, int failureCount, string? lastError);

        // Retention and cleanup
        Task<List<Feed>> GetFeedsWithRetentionAsync();
        Task<int> UpdateArticleCountsAsync(int feedId, int totalCount, int unreadCount);
        Task<int> SetActiveStatusAsync(int feedId, bool isActive);

        // Search and filtering
        Task<List<Feed>> SearchAsync(string searchText);
        /// <summary>
        /// Retrieves all feeds grouped by their CategoryId.
        /// </summary>
        /// <returns>Dictionary mapping CategoryId to list of feeds in that category.</returns>
        Task<Dictionary<int, List<Feed>>> GetFeedsGroupedByCategoryAsync();

        /// <summary>
        /// Retrieves all feeds belonging to a specific category.
        /// </summary>
        /// <param name="categoryId">The ID of the category.</param>
        /// <returns>List of feeds in the specified category.</returns>
        Task<List<Feed>> GetFeedsByCategoryAsync(int categoryId);
    }
}
