using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Article;
using NeonSuit.RSSReader.Core.DTOs.ArticleTags;
using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Events;
using Serilog;

namespace NeonSuit.RSSReader.Services;

/// <summary>
/// Service implementation for managing article-tag relationships.
/// Provides business logic and coordination between articles and tags,
/// ensuring data integrity and efficient operations for low-resource environments.
/// </summary>
internal class ArticleTagService : IArticleTagService
{
    private readonly IArticleTagRepository _articleTagRepository;
    private readonly IArticleRepository _articleRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleTagService"/> class.
    /// </summary>
    /// <param name="articleTagRepository">Repository for article-tag associations.</param>
    /// <param name="articleRepository">Repository for article operations.</param>
    /// <param name="tagRepository">Repository for tag operations.</param>
    /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
    /// <param name="logger">Logger instance for diagnostic tracking.</param>
    /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
    public ArticleTagService(
        IArticleTagRepository articleTagRepository,
        IArticleRepository articleRepository,
        ITagRepository tagRepository,
        IMapper mapper,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(articleTagRepository);
        ArgumentNullException.ThrowIfNull(articleRepository);
        ArgumentNullException.ThrowIfNull(tagRepository);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(logger);

        _articleTagRepository = articleTagRepository;
        _articleRepository = articleRepository;
        _tagRepository = tagRepository;
        _mapper = mapper;
        _logger = logger.ForContext<ArticleTagService>();

#if DEBUG
        _logger.Debug("ArticleTagService initialized");
#endif
    }

    #endregion

    #region Events

    /// <inheritdoc />
    public event EventHandler<ArticleTaggedEventArgs>? OnArticleTagged;

    /// <inheritdoc />
    public event EventHandler<ArticleUntaggedEventArgs>? OnArticleUntagged;

    #endregion

    #region Basic Tagging Operations

    /// <inheritdoc />
    public async Task<bool> TagArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliedBy);

        if (confidence.HasValue && (confidence < 0 || confidence > 1))
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1");

        try
        {
            _logger.Debug("Tagging article {ArticleId} with tag {TagId} by {AppliedBy}", articleId, tagId, appliedBy);

            // Validate article exists
            var article = await _articleRepository.GetByIdReadOnlyAsync(articleId, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found", articleId);
                throw new InvalidOperationException($"Article with ID {articleId} does not exist");
            }

            // Validate tag exists and get current count
            var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
            if (tag == null)
            {
                _logger.Warning("Tag {TagId} not found", tagId);
                throw new InvalidOperationException($"Tag with ID {tagId} does not exist");
            }

            var result = await _articleTagRepository.AssociateTagWithArticleAsync(
                articleId, tagId, appliedBy, ruleId, confidence, cancellationToken).ConfigureAwait(false);

            if (result)
            {
                // ✅ Increment usage count using UpdateUsageMetadataAsync
                var newCount = tag.UsageCount + 1;
                await _tagRepository.UpdateUsageMetadataAsync(
                    tagId,
                    newCount,
                    DateTime.UtcNow,
                    cancellationToken).ConfigureAwait(false);

                // Raise event
                OnArticleTagged?.Invoke(this,
                    new ArticleTaggedEventArgs(articleId, tagId, tag.Name, appliedBy, ruleId, confidence));

                _logger.Information("Article {ArticleId} tagged with '{TagName}'", articleId, tag.Name);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("TagArticleAsync cancelled for article {ArticleId}, tag {TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error tagging article {ArticleId} with tag {TagId}", articleId, tagId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UntagArticleAsync(int articleId, int tagId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);

        try
        {
            _logger.Debug("Untagging article {ArticleId} from tag {TagId}", articleId, tagId);

            var exists = await _articleTagRepository.ExistsAsync(articleId, tagId, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                _logger.Information("Article {ArticleId} is not tagged with tag {TagId}", articleId, tagId);
                return false;
            }

            // Get tag for name and current count
            string tagName = $"Tag_{tagId}";
            int currentCount = 0;

            var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
            if (tag != null)
            {
                tagName = tag.Name;
                currentCount = tag.UsageCount;
            }

            var result = await _articleTagRepository.RemoveTagFromArticleAsync(articleId, tagId, cancellationToken).ConfigureAwait(false);

            if (result)
            {
                // ✅ Decrement usage count using UpdateUsageMetadataAsync (only if tag still exists)
                if (tag != null)
                {
                    var newCount = Math.Max(0, currentCount - 1);
                    await _tagRepository.UpdateUsageMetadataAsync(
                        tagId,
                        newCount,
                        DateTime.UtcNow,
                        cancellationToken).ConfigureAwait(false);
                }

                OnArticleUntagged?.Invoke(this,
                    new ArticleUntaggedEventArgs(articleId, tagId, tagName));

                _logger.Information("Article {ArticleId} untagged from '{TagName}'", articleId, tagName);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UntagArticleAsync cancelled for article {ArticleId}, tag {TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error untagging article {ArticleId} from tag {TagId}", articleId, tagId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsArticleTaggedAsync(int articleId, int tagId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);

        try
        {
            return await _articleTagRepository.ExistsAsync(articleId, tagId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("IsArticleTaggedAsync cancelled for article {ArticleId}, tag {TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking if article {ArticleId} is tagged with tag {TagId}", articleId, tagId);
            throw;
        }
    }

    #endregion

    #region Batch Tagging Operations

    /// <inheritdoc />
    public async Task<int> TagArticleWithMultipleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentNullException.ThrowIfNull(tagIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliedBy);

        try
        {
            _logger.Debug("Tagging article {ArticleId} with multiple tags", articleId);

            var result = await _articleTagRepository.AssociateTagsWithArticleAsync(
                articleId, tagIds, appliedBy, ruleId, cancellationToken).ConfigureAwait(false);

            _logger.Information("Tagged article {ArticleId} with {Count} new tags", articleId, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("TagArticleWithMultipleAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error tagging article {ArticleId} with multiple tags", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> UntagArticleMultipleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentNullException.ThrowIfNull(tagIds);

        try
        {
            _logger.Debug("Untagging article {ArticleId} from multiple tags", articleId);

            var result = await _articleTagRepository.RemoveTagsFromArticleAsync(
                articleId, tagIds, cancellationToken).ConfigureAwait(false);

            _logger.Information("Untagged article {ArticleId} from {Count} tags", articleId, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UntagArticleMultipleAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error untagging article {ArticleId} from multiple tags", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ReplaceArticleTagsAsync(int articleId, IEnumerable<int> newTagIds, string appliedBy = "user", CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);
        ArgumentNullException.ThrowIfNull(newTagIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(appliedBy);

        try
        {
            _logger.Debug("Replacing tags for article {ArticleId}", articleId);

            var currentTags = await GetArticleTagAssociationsAsync(articleId, cancellationToken).ConfigureAwait(false);
            var currentTagIds = currentTags.Select(t => t.TagId).ToList();
            var newTagIdsList = newTagIds.ToList();

            var tagsToRemove = currentTagIds.Except(newTagIdsList).ToList();
            var tagsToAdd = newTagIdsList.Except(currentTagIds).ToList();

            int removed = 0;
            if (tagsToRemove.Any())
            {
                removed = await _articleTagRepository.RemoveTagsFromArticleAsync(articleId, tagsToRemove, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Removed {Count} tags from article {ArticleId}", removed, articleId);
            }

            int added = 0;
            if (tagsToAdd.Any())
            {
                added = await _articleTagRepository.AssociateTagsWithArticleAsync(articleId, tagsToAdd, appliedBy, null, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Added {Count} tags to article {ArticleId}", added, articleId);
            }

            _logger.Information("Replaced tags for article {ArticleId}: Removed={Removed}, Added={Added}", articleId, removed, added);
            return added;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ReplaceArticleTagsAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error replacing tags for article {ArticleId}", articleId);
            throw;
        }
    }

    #endregion

    #region Retrieval Operations

    /// <inheritdoc />
    public async Task<List<TagDto>> GetTagsForArticleAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Retrieving tags for article {ArticleId}", articleId);

            var tags = await _articleTagRepository.GetTagsForArticleWithDetailsAsync(articleId, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<TagDto>>(tags);

            _logger.Information("Retrieved {Count} tags for article {ArticleId}", result.Count, articleId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagsForArticleAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tags for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetArticlesWithTagAsync(int tagId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);

        try
        {
            _logger.Debug("Retrieving articles with tag {TagId}", tagId);

            var associations = await _articleTagRepository.GetByTagIdAsync(tagId, cancellationToken).ConfigureAwait(false);

            var articleIds = associations.Select(a => a.ArticleId).Distinct().ToList();

            if (!articleIds.Any())
            {
                _logger.Information("No articles found with tag {TagId}", tagId);
                return new List<ArticleSummaryDto>();
            }

            var articles = new List<Article>();
            foreach (var articleId in articleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var article = await _articleRepository.GetByIdReadOnlyAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article != null)
                    articles.Add(article);
            }

            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} articles with tag {TagId}", result.Count, tagId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticlesWithTagAsync cancelled for tag {TagId}", tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving articles with tag {TagId}", tagId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetArticlesWithTagNameAsync(string tagName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        try
        {
            _logger.Debug("Retrieving articles with tag name '{TagName}'", tagName);

            var tag = await _tagRepository.GetByNameAsync(tagName, cancellationToken).ConfigureAwait(false);
            if (tag == null)
            {
                _logger.Information("Tag '{TagName}' not found", tagName);
                return new List<ArticleSummaryDto>();
            }

            return await GetArticlesWithTagAsync(tag.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticlesWithTagNameAsync cancelled for '{TagName}'", tagName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving articles with tag name '{TagName}'", tagName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleTagInfoDto>> GetArticleTagAssociationsAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Retrieving tag associations for article {ArticleId}", articleId);

            var associations = await _articleTagRepository.GetByArticleIdAsync(articleId, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleTagInfoDto>>(associations);

            _logger.Information("Retrieved {Count} tag associations for article {ArticleId}", result.Count, articleId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticleTagAssociationsAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tag associations for article {ArticleId}", articleId);
            throw;
        }
    }

    #endregion

    #region Statistics and Analysis

    /// <inheritdoc />
    public async Task<Dictionary<int, int>> GetTagUsageCountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving tag usage counts");

            var allTags = await _tagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var result = allTags.ToDictionary(t => t.Id, t => t.UsageCount);

            _logger.Information("Retrieved usage counts for {Count} tags", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagUsageCountsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tag usage counts");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<TagDto>> GetMostUsedTagsAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            _logger.Debug("Retrieving top {Limit} most used tags", limit);

            var tags = await _tagRepository.GetPopularTagsAsync(limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<TagDto>>(tags);

            _logger.Information("Retrieved {Count} most used tags", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetMostUsedTagsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving most used tags");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetRecentlyTaggedArticlesAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        try
        {
            _logger.Debug("Retrieving {Limit} recently tagged articles", limit);

            var recentAssociations = await _articleTagRepository.GetRecentlyAppliedTagsAsync(limit, cancellationToken).ConfigureAwait(false);
            var articleIds = recentAssociations.Select(a => a.ArticleId).Distinct().ToList();

            var articles = new List<Article>();
            foreach (var articleId in articleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var article = await _articleRepository.GetByIdReadOnlyAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article != null)
                    articles.Add(article);
            }

            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} recently tagged articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRecentlyTaggedArticlesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving recently tagged articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetTaggingStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Generating tagging statistics for period {StartDate} to {EndDate}", startDate, endDate);

            var allAssociations = await _articleTagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            var filtered = allAssociations.AsEnumerable();
            if (startDate.HasValue)
                filtered = filtered.Where(a => a.AppliedAt >= startDate.Value);
            if (endDate.HasValue)
                filtered = filtered.Where(a => a.AppliedAt <= endDate.Value);

            var filteredList = filtered.ToList();

            var stats = new Dictionary<string, int>
            {
                ["total_associations"] = filteredList.Count,
                ["user_applied"] = filteredList.Count(a => a.AppliedBy == "user"),
                ["rule_applied"] = filteredList.Count(a => a.AppliedBy == "rule"),
                ["system_applied"] = filteredList.Count(a => a.AppliedBy == "system")
            };

            _logger.Information("Generated tagging statistics: Total={Total}, User={User}, Rule={Rule}, System={System}",
                stats["total_associations"], stats["user_applied"], stats["rule_applied"], stats["system_applied"]);

            return stats;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTaggingStatisticsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error generating tagging statistics");
            throw;
        }
    }

    #endregion

    #region Rule-Based Tagging Operations

    /// <inheritdoc />
    public async Task<int> ApplyRuleTaggingAsync(int ruleId, IEnumerable<int> articleIds, IEnumerable<int> tagIds, double confidence = 0.8, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);
        ArgumentNullException.ThrowIfNull(articleIds);
        ArgumentNullException.ThrowIfNull(tagIds);

        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1");

        try
        {
            _logger.Debug("Applying rule {RuleId} tags to multiple articles", ruleId);

            var articleIdsList = articleIds.ToList();
            var tagIdsList = tagIds.ToList();
            int totalApplied = 0;

            foreach (var articleId in articleIdsList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var added = await _articleTagRepository.AssociateTagsWithArticleAsync(
                    articleId, tagIdsList, "rule", ruleId, cancellationToken).ConfigureAwait(false);
                totalApplied += added;
            }

            _logger.Information("Rule {RuleId} applied {Count} tags to {ArticleCount} articles",
                ruleId, totalApplied, articleIdsList.Count);

            return totalApplied;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ApplyRuleTaggingAsync cancelled for rule {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error applying rule {RuleId} tagging", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveRuleTagsAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ruleId);

        try
        {
            _logger.Debug("Removing tags applied by rule {RuleId}", ruleId);

            var ruleTags = await _articleTagRepository.GetTagsAppliedByRuleAsync(ruleId, cancellationToken).ConfigureAwait(false);
            var removed = 0;

            foreach (var articleTag in ruleTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await UntagArticleAsync(articleTag.ArticleId, articleTag.TagId, cancellationToken).ConfigureAwait(false))
                    removed++;
            }

            _logger.Information("Removed {Count} tags applied by rule {RuleId}", removed, ruleId);
            return removed;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RemoveRuleTagsAsync cancelled for rule {RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error removing tags for rule {RuleId}", ruleId);
            throw;
        }
    }

    #endregion

    #region Maintenance Operations

    /// <inheritdoc />
    public async Task<int> CleanupOrphanedAssociationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Cleaning up orphaned article-tag associations");

            // Get all associations
            var allAssociations = await _articleTagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var orphanedIds = new List<int>();

            foreach (var association in allAssociations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var article = await _articleRepository.GetByIdReadOnlyAsync(association.ArticleId, cancellationToken).ConfigureAwait(false);
                var tag = await _tagRepository.GetByIdAsync(association.TagId, cancellationToken).ConfigureAwait(false);

                if (article == null || tag == null)
                    orphanedIds.Add(association.Id);
            }

            var deletedCount = 0;
            foreach (var id in orphanedIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                deletedCount += await _articleTagRepository.DeleteByIdAsync(id, cancellationToken).ConfigureAwait(false);
            }

            _logger.Information("Cleaned up {Count} orphaned article-tag associations", deletedCount);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("CleanupOrphanedAssociationsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error cleaning up orphaned associations");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RecalculateTagUsageCountsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Recalculating tag usage counts");

            var allTags = await _tagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var updatedCount = 0;

            foreach (var tag in allTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var associations = await _articleTagRepository.GetByTagIdAsync(tag.Id, cancellationToken).ConfigureAwait(false);
                var actualCount = associations.Count;

                if (tag.UsageCount != actualCount)
                {
                    tag.UsageCount = actualCount;
                    await _tagRepository.UpdateAsync(tag, cancellationToken).ConfigureAwait(false);
                    updatedCount++;
                }
            }

            _logger.Information("Recalculated and updated usage counts for {Count} tags", updatedCount);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RecalculateTagUsageCountsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error recalculating tag usage counts");
            throw;
        }
    }

    #endregion

    #region TODO: Future Improvements

    // TODO (High - v2.0): Add bulk operations to repository for better performance
    // - Add BulkAssociateTagsForRuleAsync to repository
    // - Add BulkRemoveTagsByRuleAsync to repository
    // - Add GetArticlesByIdsAsync to article repository
    // Why: Reduce database round-trips for large operations
    // Risk level: Medium
    // Estimated effort: 2 days

    // TODO (Medium - v1.x): Add caching for frequently accessed data
    // - Cache tag lists per article with invalidation on changes
    // - Cache tag usage counts with sliding expiration
    // Why: Improve performance for frequently called endpoints
    // Risk level: Low
    // Estimated effort: 1 day

    // TODO (Low - v1.x): Add validation for maximum tags per article
    // Why: Prevent performance issues with articles that have too many tags
    // Implementation: Add configurable limit and validation
    // Risk level: Low
    // Estimated effort: 2 hours

    #endregion
}