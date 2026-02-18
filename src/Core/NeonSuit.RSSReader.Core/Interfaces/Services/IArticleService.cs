using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    public interface IArticleService
    {
        Task<List<Article>> GetAllArticlesAsync();
        Task<List<Article>> GetArticlesByFeedAsync(int feedId);
        Task<List<Article>> GetArticlesByCategoryAsync(int categoryId);
        Task<List<Article>> GetUnreadArticlesAsync();
        Task<List<Article>> GetStarredArticlesAsync();
        Task<List<Article>> GetFavoriteArticlesAsync();
        Task<bool> MarkAsReadAsync(int articleId, bool isRead);
        Task<bool> ToggleStarredAsync(int articleId);
        Task<bool> ToggleFavoriteAsync(int articleId);
        Task<List<Article>> SearchArticlesAsync(string searchText);
        Task<int> DeleteOldArticlesAsync(int daysOld = 30);
        Task<int> GetUnreadCountAsync();
        Task<int> GetUnreadCountByFeedAsync(int feedId);
        Task<bool> MarkAllAsReadAsync();
        Task<bool> MarkAsNotifiedAsync(int articleId);
        Task<bool> MarkAsProcessedAsync(int articleId);
        Task<List<Article>> GetUnprocessedArticlesAsync();
        Task<List<Article>> GetUnnotifiedArticlesAsync();
        Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync();
        Task<List<Article>> GetArticlesByCategoriesAsync(string categories);
        Task<List<Article>> GetPagedArticlesAsync(int page, int pageSize, bool? unreadOnly = null);
    }
}