using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for managing article-tag relationships.
    /// Provides business logic for tagging operations.
    /// </summary>
    public interface IArticleTagService
    {
        // Basic Operations
        Task<bool> TagArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null);
        Task<bool> UntagArticleAsync(int articleId, int tagId);
        Task<bool> IsArticleTaggedAsync(int articleId, int tagId);

        // Batch Operations
        Task<int> TagArticleWithMultipleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null);
        Task<int> UntagArticleMultipleAsync(int articleId, IEnumerable<int> tagIds);
        Task<int> ReplaceArticleTagsAsync(int articleId, IEnumerable<int> newTagIds, string appliedBy = "user");

        // Retrieval Operations
        Task<List<Tag>> GetTagsForArticleAsync(int articleId);
        Task<List<Article>> GetArticlesWithTagAsync(int tagId);
        Task<List<Article>> GetArticlesWithTagNameAsync(string tagName);
        Task<List<ArticleTag>> GetArticleTagAssociationsAsync(int articleId);

        // Statistics and Analysis
        Task<Dictionary<int, int>> GetTagUsageCountsAsync();
        Task<List<Tag>> GetMostUsedTagsAsync(int limit = 10);
        Task<List<Article>> GetRecentlyTaggedArticlesAsync(int limit = 20);
        Task<Dictionary<string, int>> GetTaggingStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);

        // Rule-based Tagging
        Task<int> ApplyRuleTaggingAsync(int ruleId, IEnumerable<int> articleIds, IEnumerable<int> tagIds, double confidence = 0.8);
        Task<int> RemoveRuleTagsAsync(int ruleId);

        // Maintenance
        Task<int> CleanupOrphanedAssociationsAsync();
        Task<int> RecalculateTagUsageCountsAsync();

        // Events
        event EventHandler<ArticleTaggedEventArgs> OnArticleTagged;
        event EventHandler<ArticleUntaggedEventArgs> OnArticleUntagged;
    }

    public class ArticleTaggedEventArgs : EventArgs
    {
        public int ArticleId { get; }
        public int TagId { get; }
        public string TagName { get; }
        public string AppliedBy { get; }
        public int? RuleId { get; }

        public ArticleTaggedEventArgs(int articleId, int tagId, string tagName, string appliedBy, int? ruleId = null)
        {
            ArticleId = articleId;
            TagId = tagId;
            TagName = tagName;
            AppliedBy = appliedBy;
            RuleId = ruleId;
        }
    }

    public class ArticleUntaggedEventArgs : EventArgs
    {
        public int ArticleId { get; }
        public int TagId { get; }
        public string TagName { get; }

        public ArticleUntaggedEventArgs(int articleId, int tagId, string tagName)
        {
            ArticleId = articleId;
            TagId = tagId;
            TagName = tagName;
        }
    }
}