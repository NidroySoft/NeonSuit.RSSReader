using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for Article entities.
    /// </summary>
    public interface IArticleRepository
    {
        // CRUD Operations
        Task<Article?> GetByIdAsync(int id);
        Task<List<Article>> GetAllAsync();
        Task<List<Article>> GetWhereAsync(Func<Article, bool> predicate);
        Task<int> InsertAsync(Article entity);
        Task<int> InsertAllAsync(List<Article> entities);
        Task<int> UpdateAsync(Article entity);
         Task<int> UpdateAllAsync(List<Article> entities);
        Task<int> DeleteAsync(Article entity);
        Task<int> CountWhereAsync(Func<Article, bool> predicate);

        // Article-specific operations
        Task<List<Article>> GetByFeedAsync(int feedId, int limit = 100);
        Task<List<Article>> GetByFeedsAsync(List<int> feedIds, int limit = 100);
        Task<List<Article>> GetUnreadAsync(int limit = 100);
        Task<List<Article>> GetStarredAsync();
        Task<List<Article>> GetFavoritesAsync();
        Task<List<Article>> GetUnreadByFeedAsync(int feedId);
        Task<Article?> GetByGuidAsync(string guid);
        Task<Article?> GetByContentHashAsync(string hash);
        Task<bool> ExistsByGuidAsync(string guid);
        Task<bool> ExistsByContentHashAsync(string hash);
        Task<int> MarkAsAsync(int articleId, ArticleStatus status);
        Task<int> ToggleStarAsync(int articleId);
        Task<int> ToggleFavoriteAsync(int articleId);
        Task<int> MarkAllAsReadByFeedAsync(int feedId);
        Task<int> MarkAllAsReadAsync();
        Task<int> GetUnreadCountAsync();
        Task<int> GetUnreadCountByFeedAsync(int feedId);
        Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync();
        Task<List<Article>> SearchAsync(string searchText);
        Task<int> DeleteOlderThanAsync(DateTime date);
        Task<int> DeleteByFeedAsync(int feedId);

        // New methods for model properties
        Task<List<Article>> GetUnprocessedArticlesAsync(int limit = 100);
        Task<List<Article>> GetUnnotifiedArticlesAsync(int limit = 100);
        Task<List<Article>> GetByCategoriesAsync(string categories, int limit = 100);
        Task<int> MarkAsProcessedAsync(int articleId);
        Task<int> MarkAsNotifiedAsync(int articleId);
        Task<int> BulkMarkAsProcessedAsync(List<int> articleIds);
        Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds);

        // Pagination
        Task<List<Article>> GetPagedAsync(int page, int pageSize, ArticleStatus? status = null);
        Task<List<Article>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null);
    }
}