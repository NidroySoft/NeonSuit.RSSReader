using NeonSuit.RSSReader.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing the many-to-many relationship between <see cref="Article"/> and <see cref="Tag"/> entities.
    /// Handles tag associations, bulk operations, removal, and usage statistics for articles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This repository manages the join table <c>ArticleTag</c>, which stores metadata about when and how a tag was applied to an article
    /// (e.g., manually by user, automatically by rule, confidence score from ML, etc.).
    /// </para>
    ///
    /// <para>
    /// Core responsibilities:
    /// </para>
    /// <list type="bullet">
    ///     <item>Association and disassociation of tags to/from articles (single and bulk)</item>
    ///     <item>Existence checks and targeted retrieval by article or tag</item>
    ///     <item>Bulk cleanup (remove all tags from article or tag from all articles)</item>
    ///     <item>Retrieval of tags with full details or article IDs by tag name</item>
    ///     <item>Statistics and audit-like queries (recently applied, by rule)</item>
    /// </list>
    ///
    /// <para>
    /// Important behavioral expectations:
    /// </para>
    /// <list type="bullet">
    ///     <item>All write operations (associate/remove) should be atomic and preferably transactional.</item>
    ///     <item>Duplicate associations are prevented (unique constraint on ArticleId + TagId).</item>
    ///     <item>Metadata fields (<c>AppliedBy</c>, <c>RuleId</c>, <c>Confidence</c>) are optional but tracked for auditability and future ML/rule improvements.</item>
    ///     <item>Removal operations do not cascade to delete tags themselves — only the join record.</item>
    ///     <item>Queries involving tag names may involve joins to the <c>Tag</c> table.</item>
    /// </list>
    ///
    /// <para>
    /// Usage scenarios:
    /// </para>
    /// <list type="bullet">
    ///     <item>User manually adds/removes tags from article detail view</item>
    ///     <item>Rule engine applies tags automatically during sync</item>
    ///     <item>Feed/category/tag-based filtering in UI</item>
    ///     <item>Tag usage analytics or "recent tags" suggestions</item>
    ///     <item>Cleanup when deleting article or tag</item>
    /// </list>
    ///
    /// <para>
    /// Performance notes:
    /// </para>
    /// <list type="bullet">
    ///     <item>Indexed on ArticleId, TagId, and possibly AppliedAt/RuleId for fast lookups.</item>
    ///     <item>Bulk operations should use batch inserts/deletes where supported.</item>
    ///     <item>Avoid loading full Article/Tag entities unless explicitly needed (use projections).</item>
    /// </list>
    /// </remarks>
    public interface IArticleTagRepository : IRepository<ArticleTag>
    {
        /// <summary>
        /// Retrieves all tag associations for a given article.
        /// </summary>
        /// <param name="articleId">The ID of the article to query.</param>
        /// <returns>List of <see cref="ArticleTag"/> join entities (includes metadata like AppliedBy, RuleId).</returns>
        /// <remarks>
        /// Used to display current tags in article detail view or for validation before adding new ones.
        /// Ordered by application time or tag name (implementation-dependent).
        /// </remarks>
        Task<List<ArticleTag>> GetByArticleIdAsync(int articleId);

        /// <summary>
        /// Retrieves all tag associations for a given tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag to query.</param>
        /// <returns>List of <see cref="ArticleTag"/> entities linked to the tag.</returns>
        /// <remarks>
        /// Useful for tag detail views ("articles with this tag") or bulk removal scenarios.
        /// May be paginated in implementation if large.
        /// </remarks>
        Task<List<ArticleTag>> GetByTagIdAsync(int tagId);

        /// <summary>
        /// Checks if a specific article is already associated with a specific tag.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID.</param>
        /// <returns><c>true</c> if the association exists; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// Optimized scalar query — avoids loading full entity.
        /// Used to prevent duplicate associations.
        /// </remarks>
        Task<bool> ExistsAsync(int articleId, int tagId);

        /// <summary>
        /// Retrieves the exact <see cref="ArticleTag"/> join record for a given article-tag pair.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID.</param>
        /// <returns>The join entity if exists; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// Allows access to metadata (AppliedBy, Confidence, RuleId) without loading lists.
        /// </remarks>
        Task<ArticleTag?> GetByArticleAndTagAsync(int articleId, int tagId);

        /// <summary>
        /// Associates a single tag with an article, including optional metadata.
        /// </summary>
        /// <param name="articleId">ID of the article.</param>
        /// <param name="tagId">ID of the tag to apply.</param>
        /// <param name="appliedBy">Source of the tag ("user", "rule", "ml", etc.). Default: "user".</param>
        /// <param name="ruleId">ID of the rule that applied the tag (null if manual).</param>
        /// <param name="confidence">Confidence score (0.0–1.0) if ML-applied; null otherwise.</param>
        /// <returns><c>true</c> if the association was created; <c>false</c> if already existed.</returns>
        /// <remarks>
        /// Idempotent: does nothing if association already exists.
        /// Sets application timestamp automatically.
        /// Throws if article or tag does not exist.
        /// </remarks>
        Task<bool> AssociateTagWithArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null);

        /// <summary>
        /// Removes a single tag association from an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID to remove.</param>
        /// <returns><c>true</c> if the association was removed; <c>false</c> if it did not exist.</returns>
        /// <remarks>
        /// Safe to call even if no association exists.
        /// Does not delete the tag entity itself.
        /// </remarks>
        Task<bool> RemoveTagFromArticleAsync(int articleId, int tagId);

        /// <summary>
        /// Removes all tag associations from a specific article.
        /// </summary>
        /// <param name="articleId">The article to clear tags from.</param>
        /// <returns>Number of tag associations removed.</returns>
        /// <remarks>
        /// Used when user removes all tags or during article archiving/deletion prep.
        /// Efficient bulk delete.
        /// </remarks>
        Task<int> RemoveAllTagsFromArticleAsync(int articleId);

        /// <summary>
        /// Removes a tag from every article it is associated with.
        /// </summary>
        /// <param name="tagId">The tag to disassociate globally.</param>
        /// <returns>Number of associations removed (i.e., affected articles).</returns>
        /// <remarks>
        /// Called when deleting a tag or bulk-unapplying it.
        /// May be expensive on popular tags — consider background execution.
        /// </remarks>
        Task<int> RemoveTagFromAllArticlesAsync(int tagId);

        /// <summary>
        /// Associates multiple tags with an article in a single operation.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagIds">Collection of tag IDs to apply.</param>
        /// <param name="appliedBy">Source ("user", "rule", etc.).</param>
        /// <param name="ruleId">Rule ID if applied by automation (optional).</param>
        /// <returns>Number of new associations created (duplicates skipped).</returns>
        /// <remarks>
        /// Idempotent bulk operation.
        /// Uses transaction for consistency.
        /// </remarks>
        Task<int> AssociateTagsWithArticleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null);

        /// <summary>
        /// Removes multiple specific tags from an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagIds">Tags to remove.</param>
        /// <returns>Number of associations actually removed.</returns>
        /// <remarks>
        /// Bulk removal — skips non-existent associations.
        /// </remarks>
        Task<int> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds);

        /// <summary>
        /// Retrieves IDs of all articles associated with a given tag name.
        /// </summary>
        /// <param name="tagName">Normalized tag name (case-insensitive usually).</param>
        /// <returns>List of article IDs that have this tag.</returns>
        /// <remarks>
        /// Used for tag-based filtering or navigation ("show all articles tagged 'news'").
        /// Returns IDs only — efficient for large result sets.
        /// </remarks>
        Task<List<int>> GetArticleIdsByTagNameAsync(string tagName);

        /// <summary>
        /// Retrieves full <see cref="Tag"/> entities associated with an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <returns>List of detailed <see cref="Tag"/> objects (not just join records).</returns>
        /// <remarks>
        /// Joins with Tag table to include name, color, description, etc.
        /// Preferred for UI display of tags.
        /// </remarks>
        Task<List<Tag>> GetTagsForArticleWithDetailsAsync(int articleId);

        /// <summary>
        /// Computes basic statistics about tag usage on a specific article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <returns>Dictionary of tag name → count (usually 1 per tag, but extensible).</returns>
        /// <remarks>
        /// Currently simple (count per tag), but can be extended to include confidence averages, etc.
        /// Useful for tag clouds or analytics per article.
        /// </remarks>
        Task<Dictionary<string, int>> GetTagStatisticsForArticleAsync(int articleId);

        /// <summary>
        /// Retrieves the most recently applied tag associations.
        /// </summary>
        /// <param name="limit">Maximum number of recent associations to return (default 50).</param>
        /// <returns>List of <see cref="ArticleTag"/> ordered by application time descending.</returns>
        /// <remarks>
        /// Used for "recent tags" suggestions, activity feed, or undo features.
        /// Ordered by AppliedAt timestamp.
        /// </remarks>
        Task<List<ArticleTag>> GetRecentlyAppliedTagsAsync(int limit = 50);

        /// <summary>
        /// Retrieves all tag associations created by a specific rule.
        /// </summary>
        /// <param name="ruleId">ID of the rule that applied the tags.</param>
        /// <returns>List of <see cref="ArticleTag"/> applied by the rule.</returns>
        /// <remarks>
        /// Useful for rule debugging, auditing, or "undo rule" features.
        /// May return large results if rule is broad.
        /// </remarks>
        Task<List<ArticleTag>> GetTagsAppliedByRuleAsync(int ruleId);
    }
}