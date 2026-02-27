using NeonSuit.RSSReader.Core.Models;

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
    ///     <item>All write operations should be atomic and preferably transactional.</item>
    ///     <item>Duplicate associations are prevented (unique constraint on ArticleId + TagId).</item>
    ///     <item>Metadata fields (<c>AppliedBy</c>, <c>RuleId</c>, <c>Confidence</c>) are optional but tracked for auditability and future ML/rule improvements.</item>
    ///     <item>Removal operations do not cascade to delete tags themselves — only the join record.</item>
    ///     <item>Queries involving tag names may involve joins to the <c>Tag</c> table.</item>
    /// </list>
    /// </remarks>
    public interface IArticleTagRepository : IRepository<ArticleTag>
    {
        #region Read Single / Existence Checks

        /// <summary>
        /// Retrieves the exact <see cref="ArticleTag"/> join record for a given article-tag pair.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The join entity if exists; otherwise <c>null</c>.</returns>
        Task<ArticleTag?> GetByArticleAndTagAsync(int articleId, int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a specific article is already associated with a specific tag.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the association exists; otherwise <c>false</c>.</returns>
        Task<bool> ExistsAsync(int articleId, int tagId, CancellationToken cancellationToken = default);

        #endregion

        #region Read Collections - By Article / Tag

        /// <summary>
        /// Retrieves all tag associations for a given article.
        /// </summary>
        /// <param name="articleId">The ID of the article to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of <see cref="ArticleTag"/> join entities.</returns>
        Task<List<ArticleTag>> GetByArticleIdAsync(int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all tag associations for a given tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag to query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of <see cref="ArticleTag"/> entities linked to the tag.</returns>
        Task<List<ArticleTag>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves full <see cref="Tag"/> entities associated with an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of detailed <see cref="Tag"/> objects.</returns>
        Task<List<Tag>> GetTagsForArticleWithDetailsAsync(int articleId, CancellationToken cancellationToken = default);

        #endregion

        #region Read Collections - Recent / Rule / Tag Name

        /// <summary>
        /// Retrieves the most recently applied tag associations.
        /// </summary>
        /// <param name="limit">Maximum number of recent associations to return (default 50).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of <see cref="ArticleTag"/> ordered by application time descending.</returns>
        Task<List<ArticleTag>> GetRecentlyAppliedTagsAsync(int limit = 50, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all tag associations created by a specific rule.
        /// </summary>
        /// <param name="ruleId">ID of the rule that applied the tags.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of <see cref="ArticleTag"/> applied by the rule.</returns>
        Task<List<ArticleTag>> GetTagsAppliedByRuleAsync(int ruleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves IDs of all articles associated with a given tag name.
        /// </summary>
        /// <param name="tagName">Normalized tag name (case-insensitive).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of article IDs that have this tag.</returns>
        Task<List<int>> GetArticleIdsByTagNameAsync(string tagName, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Computes basic statistics about tag usage on a specific article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary of tag name → count.</returns>
        Task<Dictionary<string, int>> GetTagStatisticsForArticleAsync(int articleId, CancellationToken cancellationToken = default);

        #endregion

        #region Write - Single Association

        /// <summary>
        /// Associates a single tag with an article, including optional metadata.
        /// </summary>
        /// <param name="articleId">ID of the article.</param>
        /// <param name="tagId">ID of the tag to apply.</param>
        /// <param name="appliedBy">Source of the tag ("user", "rule", "ml", etc.). Default: "user".</param>
        /// <param name="ruleId">ID of the rule that applied the tag (null if manual).</param>
        /// <param name="confidence">Confidence score (0.0–1.0) if ML-applied; null otherwise.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the association was created; <c>false</c> if already existed.</returns>
        Task<bool> AssociateTagWithArticleAsync(
            int articleId,
            int tagId,
            string appliedBy = "user",
            int? ruleId = null,
            double? confidence = null,
            CancellationToken cancellationToken = default);

        #endregion

        #region Write - Bulk Operations

        /// <summary>
        /// Associates multiple tags with an article in a single operation.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagIds">Collection of tag IDs to apply.</param>
        /// <param name="appliedBy">Source ("user", "rule", etc.).</param>
        /// <param name="ruleId">Rule ID if applied by automation (optional).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of new associations created.</returns>
        Task<int> AssociateTagsWithArticleAsync(
            int articleId,
            IEnumerable<int> tagIds,
            string appliedBy = "user",
            int? ruleId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a single tag association from an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagId">Tag ID to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the association was removed; <c>false</c> if it did not exist.</returns>
        Task<bool> RemoveTagFromArticleAsync(int articleId, int tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes multiple specific tags from an article.
        /// </summary>
        /// <param name="articleId">Article ID.</param>
        /// <param name="tagIds">Tags to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of associations actually removed.</returns>
        Task<int> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes all tag associations from a specific article.
        /// </summary>
        /// <param name="articleId">The article to clear tags from.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of tag associations removed.</returns>
        Task<int> RemoveAllTagsFromArticleAsync(int articleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a tag from every article it is associated with.
        /// </summary>
        /// <param name="tagId">The tag to disassociate globally.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of associations removed.</returns>
        Task<int> RemoveTagFromAllArticlesAsync(int tagId, CancellationToken cancellationToken = default);

        #endregion
    }
}