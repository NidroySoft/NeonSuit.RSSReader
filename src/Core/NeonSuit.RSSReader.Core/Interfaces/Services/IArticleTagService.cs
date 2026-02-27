using NeonSuit.RSSReader.Core.DTOs.Article;
using NeonSuit.RSSReader.Core.DTOs.ArticleTags;
using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Events;

namespace NeonSuit.RSSReader.Core.Interfaces.Services;

/// <summary>
/// Service interface for managing article-tag relationships.
/// Provides business logic for tagging operations, batch processing, retrieval,
/// statistics, rule-based tagging, maintenance, and event notifications.
/// </summary>
/// <remarks>
/// <para>
/// This service coordinates tagging workflows between articles and tags,
/// ensuring atomicity, validation, and proper event raising for UI reactivity
/// and rule engine integration.
/// </para>
/// <para>
/// All methods return DTOs instead of entities to maintain separation of concerns
/// and prevent leaking persistence details to the presentation layer.
/// </para>
/// </remarks>
public interface IArticleTagService
{
    #region Events

    /// <summary>
    /// Event raised after a tag is successfully applied to an article.
    /// </summary>
    event EventHandler<ArticleTaggedEventArgs> OnArticleTagged;

    /// <summary>
    /// Event raised after a tag is successfully removed from an article.
    /// </summary>
    event EventHandler<ArticleUntaggedEventArgs> OnArticleUntagged;

    #endregion

    #region Basic Tagging Operations

    /// <summary>
    /// Associates a single tag with an article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="appliedBy">Source of the tagging (user, rule, system).</param>
    /// <param name="ruleId">Optional ID of the rule that triggered the tagging.</param>
    /// <param name="confidence">Optional confidence score (0.0–1.0) for auto-tagging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tag was successfully applied; false if already exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or tagId is less than or equal to 0, or confidence is out of range.</exception>
    /// <exception cref="ArgumentException">Thrown if appliedBy is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the specified article or tag does not exist.</exception>
    Task<bool> TagArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a tag association from an article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tag was successfully removed; false if association did not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or tagId is less than or equal to 0.</exception>
    Task<bool> UntagArticleAsync(int articleId, int tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific tag is currently associated with an article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tag is associated with the article; otherwise false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId or tagId is less than or equal to 0.</exception>
    Task<bool> IsArticleTaggedAsync(int articleId, int tagId, CancellationToken cancellationToken = default);

    #endregion

    #region Batch Tagging Operations

    /// <summary>
    /// Associates multiple tags with a single article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="tagIds">Collection of tag IDs to apply.</param>
    /// <param name="appliedBy">Source of the tagging.</param>
    /// <param name="ruleId">Optional rule ID for batch rule-based tagging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of new associations created.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown if tagIds is null.</exception>
    /// <exception cref="ArgumentException">Thrown if appliedBy is null or whitespace.</exception>
    Task<int> TagArticleWithMultipleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple tags from a single article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="tagIds">Collection of tag IDs to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of associations successfully removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown if tagIds is null.</exception>
    Task<int> UntagArticleMultipleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all existing tags on an article with a new set of tags.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="newTagIds">New set of tag IDs to apply.</param>
    /// <param name="appliedBy">Source of the tagging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tags applied after replacement.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    /// <exception cref="ArgumentNullException">Thrown if newTagIds is null.</exception>
    /// <exception cref="ArgumentException">Thrown if appliedBy is null or whitespace.</exception>
    Task<int> ReplaceArticleTagsAsync(int articleId, IEnumerable<int> newTagIds, string appliedBy = "user", CancellationToken cancellationToken = default);

    #endregion

    #region Retrieval Operations

    /// <summary>
    /// Retrieves all tags currently associated with a specific article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of tag DTOs associated with the article.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<List<TagDto>> GetTagsForArticleAsync(int articleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all articles currently associated with a specific tag.
    /// </summary>
    /// <param name="tagId">The unique identifier of the tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of article summary DTOs tagged with the specified tag.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if tagId is less than or equal to 0.</exception>
    Task<List<ArticleSummaryDto>> GetArticlesWithTagAsync(int tagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all articles currently associated with a tag by its name.
    /// </summary>
    /// <param name="tagName">The name of the tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of article summary DTOs with the specified tag name.</returns>
    /// <exception cref="ArgumentException">Thrown if tagName is null or whitespace.</exception>
    Task<List<ArticleSummaryDto>> GetArticlesWithTagNameAsync(string tagName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the detailed association information for a specific article.
    /// </summary>
    /// <param name="articleId">The unique identifier of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of article-tag info DTOs for the article.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if articleId is less than or equal to 0.</exception>
    Task<List<ArticleTagInfoDto>> GetArticleTagAssociationsAsync(int articleId, CancellationToken cancellationToken = default);

    #endregion

    #region Statistics and Analysis

    /// <summary>
    /// Retrieves current usage counts for all tags.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping tag IDs to their respective usage counts.</returns>
    Task<Dictionary<int, int>> GetTagUsageCountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a limited number of the most frequently used tags.
    /// </summary>
    /// <param name="limit">Maximum number of tags to return. Must be greater than 0.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of the most used tag DTOs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if limit is less than or equal to 0.</exception>
    Task<List<TagDto>> GetMostUsedTagsAsync(int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a limited number of articles that were most recently tagged.
    /// </summary>
    /// <param name="limit">Maximum number of articles to return. Must be greater than 0.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recently tagged article summary DTOs.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if limit is less than or equal to 0.</exception>
    Task<List<ArticleSummaryDto>> GetRecentlyTaggedArticlesAsync(int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tagging statistics aggregated over an optional time range.
    /// </summary>
    /// <param name="startDate">Optional start date for the statistics window.</param>
    /// <param name="endDate">Optional end date for the statistics window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary with tagging statistics.</returns>
    Task<Dictionary<string, int>> GetTaggingStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);

    #endregion

    #region Rule-Based Tagging Operations

    /// <summary>
    /// Applies a set of tags to multiple articles as a result of a rule execution.
    /// </summary>
    /// <param name="ruleId">The unique identifier of the rule that triggered the tagging.</param>
    /// <param name="articleIds">Collection of article IDs to tag.</param>
    /// <param name="tagIds">Collection of tag IDs to apply.</param>
    /// <param name="confidence">Confidence score for auto-tagging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of new tag associations created.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0, or confidence out of range.</exception>
    /// <exception cref="ArgumentNullException">Thrown if articleIds or tagIds is null.</exception>
    Task<int> ApplyRuleTaggingAsync(int ruleId, IEnumerable<int> articleIds, IEnumerable<int> tagIds, double confidence = 0.8, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all tag associations that were explicitly applied by a specific rule.
    /// </summary>
    /// <param name="ruleId">The unique identifier of the rule whose tags should be removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tag associations successfully removed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if ruleId is less than or equal to 0.</exception>
    Task<int> RemoveRuleTagsAsync(int ruleId, CancellationToken cancellationToken = default);

    #endregion

    #region Maintenance Operations

    /// <summary>
    /// Removes orphaned article-tag associations from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of orphaned associations deleted.</returns>
    Task<int> CleanupOrphanedAssociationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalculates and updates the usage count for all tags.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of tags whose usage counts were updated.</returns>
    Task<int> RecalculateTagUsageCountsAsync(CancellationToken cancellationToken = default);

    #endregion
}