using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for ArticleTag join entity operations.
    /// Manages the many-to-many relationship between articles and tags.
    /// </summary>
    public interface IArticleTagRepository : IRepository<ArticleTag>
    {
        /// <summary>
        /// Retrieves all ArticleTag associations for a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        Task<List<ArticleTag>> GetByArticleIdAsync(int articleId);

        /// <summary>
        /// Retrieves all ArticleTag associations for a specific tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag.</param>
        Task<List<ArticleTag>> GetByTagIdAsync(int tagId);

        /// <summary>
        /// Checks if a specific article-tag association exists.
        /// </summary>
        Task<bool> ExistsAsync(int articleId, int tagId);

        /// <summary>
        /// Retrieves ArticleTag association by article and tag IDs.
        /// </summary>
        Task<ArticleTag?> GetByArticleAndTagAsync(int articleId, int tagId);

        /// <summary>
        /// Associates a tag with an article.
        /// </summary>
        Task<bool> AssociateTagWithArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null);

        /// <summary>
        /// Removes a tag association from an article.
        /// </summary>
        Task<bool> RemoveTagFromArticleAsync(int articleId, int tagId);

        /// <summary>
        /// Removes all tags from an article.
        /// </summary>
        Task<int> RemoveAllTagsFromArticleAsync(int articleId);

        /// <summary>
        /// Removes a tag from all articles.
        /// </summary>
        Task<int> RemoveTagFromAllArticlesAsync(int tagId);

        /// <summary>
        /// Associates multiple tags with an article.
        /// </summary>
        Task<int> AssociateTagsWithArticleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null);

        /// <summary>
        /// Removes multiple tags from an article.
        /// </summary>
        Task<int> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds);

        /// <summary>
        /// Retrieves articles by tag name.
        /// </summary>
        Task<List<int>> GetArticleIdsByTagNameAsync(string tagName);

        /// <summary>
        /// Retrieves tags by article with full tag information.
        /// </summary>
        Task<List<Tag>> GetTagsForArticleWithDetailsAsync(int articleId);

        /// <summary>
        /// Retrieves statistics about tag usage for an article.
        /// </summary>
        Task<Dictionary<string, int>> GetTagStatisticsForArticleAsync(int articleId);

        /// <summary>
        /// Retrieves the most recently applied tags.
        /// </summary>
        Task<List<ArticleTag>> GetRecentlyAppliedTagsAsync(int limit = 50);

        /// <summary>
        /// Retrieves tags applied by a specific rule.
        /// </summary>
        Task<List<ArticleTag>> GetTagsAppliedByRuleAsync(int ruleId);
    }
}