using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Service implementation for managing article-tag relationships.
    /// Provides business logic and coordination between articles and tags.
    /// </summary>
    public class ArticleTagService : IArticleTagService
    {
        private readonly IArticleTagRepository _articleTagRepository;
        private readonly ITagService _tagService;
        private readonly ILogger _logger;
        private readonly IArticleRepository? _articleRepository; // Opcional para validaciones

        public event EventHandler<ArticleTaggedEventArgs>? OnArticleTagged;
        public event EventHandler<ArticleUntaggedEventArgs>? OnArticleUntagged;

        // Constructor principal para producción
        public ArticleTagService(IArticleTagRepository articleTagRepository, ITagService tagService, ILogger logger)
        {
            _articleTagRepository = articleTagRepository;
            _tagService = tagService;
            _logger = logger.ForContext<ArticleTagService>();
        }

        // Constructor extendido que permite validación de artículos
        public ArticleTagService(
            IArticleTagRepository articleTagRepository,
            ITagService tagService,
            IArticleRepository articleRepository,
            ILogger logger) : this(articleTagRepository, tagService, logger)
        {
            _articleRepository = articleRepository;
        }

        public async Task<bool> TagArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null)
        {
            try
            {
                // ✅ VALIDACIÓN: Verificar que el artículo existe
                if (_articleRepository != null)
                {
                    var articleExists = await _articleRepository.GetByIdAsync(articleId);
                    if (articleExists == null)
                    {
                        _logger.Error("Cannot tag non-existent article {ArticleId}", articleId);
                        throw new InvalidOperationException($"Article with ID {articleId} does not exist.");
                    }
                }

                // ✅ VALIDACIÓN: Verificar que el tag existe
                var tag = await _tagService.GetTagAsync(tagId);
                if (tag == null)
                {
                    _logger.Error("Cannot tag with non-existent tag {TagId}", tagId);
                    throw new InvalidOperationException($"Tag with ID {tagId} does not exist.");
                }

                // Associate tag with article
                var result = await _articleTagRepository.AssociateTagWithArticleAsync(
                    articleId, tagId, appliedBy, ruleId, confidence);

                if (result)
                {
                    // Update tag usage statistics
                    await _tagService.UpdateTagUsageAsync(tagId);

                    // Raise event
                    OnArticleTagged?.Invoke(this,
                        new ArticleTaggedEventArgs(articleId, tagId, tag.Name, appliedBy, ruleId));

                    _logger.Information("Article {ArticleId} tagged with '{TagName}' by {AppliedBy}",
                        articleId, tag.Name, appliedBy);
                }

                return result;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to tag article {ArticleId} with tag {TagId}", articleId, tagId);
                throw;
            }
        }

        public async Task<bool> UntagArticleAsync(int articleId, int tagId)
        {
            try
            {
                // ✅ PRIMERO verificar si la asociación existe (más barato y evita excepciones)
                var exists = await _articleTagRepository.ExistsAsync(articleId, tagId);
                if (!exists)
                {
                    _logger.Debug("No association found between article {ArticleId} and tag {TagId}",
                        articleId, tagId);
                    return false;
                }

                // ✅ SOLO si existe, obtener el tag para el nombre (y manejar caso donde tag fue borrado)
                string tagName;
                try
                {
                    var tag = await _tagService.GetTagAsync(tagId);
                    tagName = tag.Name;
                }
                catch (KeyNotFoundException)
                {
                    // Tag fue eliminado pero asociación aún existe (inconsistencia de datos)
                    tagName = $"Tag_{tagId}";
                    _logger.Warning("Tag {TagId} not found during untag (orphaned association)", tagId);
                }

                // Eliminar la asociación
                var result = await _articleTagRepository.RemoveTagFromArticleAsync(articleId, tagId);

                if (result)
                {
                    OnArticleUntagged?.Invoke(this,
                        new ArticleUntaggedEventArgs(articleId, tagId, tagName));

                    _logger.Information("Tag '{TagName}' (ID: {TagId}) removed from article {ArticleId}",
                        tagName, tagId, articleId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to untag article {ArticleId} from tag {TagId}", articleId, tagId);
                throw;
            }
        }

        public async Task<bool> IsArticleTaggedAsync(int articleId, int tagId)
        {
            return await _articleTagRepository.ExistsAsync(articleId, tagId);
        }

        public async Task<int> TagArticleWithMultipleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null)
        {
            int count = 0;

            foreach (var tagId in tagIds)
            {
                if (await TagArticleAsync(articleId, tagId, appliedBy, ruleId))
                    count++;
            }

            _logger.Debug("Article {ArticleId} tagged with {Count} tags by {AppliedBy}",
                articleId, count, appliedBy);

            return count;
        }

        public async Task<int> UntagArticleMultipleAsync(int articleId, IEnumerable<int> tagIds)
        {
            int count = 0;

            foreach (var tagId in tagIds)
            {
                if (await UntagArticleAsync(articleId, tagId))
                    count++;
            }

            _logger.Debug("Removed {Count} tags from article {ArticleId}", count, articleId);

            return count;
        }

        public async Task<int> ReplaceArticleTagsAsync(int articleId, IEnumerable<int> newTagIds, string appliedBy = "user")
        {
            try
            {
                // Get current tags
                var currentTags = await GetTagsForArticleAsync(articleId);
                var currentTagIds = currentTags.Select(t => t.Id).ToList();
                var newTagIdsList = newTagIds.ToList();

                // Determine tags to add and remove
                var tagsToAdd = newTagIdsList.Except(currentTagIds);
                var tagsToRemove = currentTagIds.Except(newTagIdsList);

                // Remove old tags
                int removed = await UntagArticleMultipleAsync(articleId, tagsToRemove);

                // Add new tags
                int added = await TagArticleWithMultipleAsync(articleId, tagsToAdd, appliedBy);

                _logger.Information("Replaced tags for article {ArticleId}: Removed={Removed}, Added={Added}",
                    articleId, removed, added);

                return added;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to replace tags for article {ArticleId}", articleId);
                throw;
            }
        }

        public async Task<List<Tag>> GetTagsForArticleAsync(int articleId)
        {
            return await _articleTagRepository.GetTagsForArticleWithDetailsAsync(articleId);
        }

        public async Task<List<Article>> GetArticlesWithTagAsync(int tagId)
        {
            // Note: This would require access to ArticleRepository
            // Implementation depends on your Article model structure
            _logger.Debug("GetArticlesWithTagAsync requires ArticleRepository implementation");
            return new List<Article>();
        }

        public async Task<List<Article>> GetArticlesWithTagNameAsync(string tagName)
        {
            // Note: This would require access to ArticleRepository
            _logger.Debug("GetArticlesWithTagNameAsync requires ArticleRepository implementation");
            return new List<Article>();
        }

        public async Task<List<ArticleTag>> GetArticleTagAssociationsAsync(int articleId)
        {
            return await _articleTagRepository.GetByArticleIdAsync(articleId);
        }

        public async Task<Dictionary<int, int>> GetTagUsageCountsAsync()
        {
            try
            {
                // This would be more efficient with a direct SQL query
                // For now, we'll use the tag service
                var popularTags = await _tagService.GetPopularTagsAsync(int.MaxValue);
                return popularTags.ToDictionary(t => t.Id, t => t.UsageCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get tag usage counts");
                throw;
            }
        }

        public async Task<List<Tag>> GetMostUsedTagsAsync(int limit = 10)
        {
            return await _tagService.GetPopularTagsAsync(limit);
        }

        public async Task<List<Article>> GetRecentlyTaggedArticlesAsync(int limit = 20)
        {
            // Note: This would require access to ArticleRepository
            _logger.Debug("GetRecentlyTaggedArticlesAsync requires ArticleRepository implementation");
            return new List<Article>();
        }

        public async Task<Dictionary<string, int>> GetTaggingStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var stats = new Dictionary<string, int>
                {
                    ["total_associations"] = 0,
                    ["user_applied"] = 0,
                    ["rule_applied"] = 0,
                    ["system_applied"] = 0
                };

                // This would be more efficient with direct SQL queries
                // For simplicity, we'll count from repository
                var allAssociations = await _articleTagRepository.GetAllAsync();

                if (startDate.HasValue)
                    allAssociations = allAssociations.Where(a => a.AppliedAt >= startDate.Value).ToList();

                if (endDate.HasValue)
                    allAssociations = allAssociations.Where(a => a.AppliedAt <= endDate.Value).ToList();

                stats["total_associations"] = allAssociations.Count;
                stats["user_applied"] = allAssociations.Count(a => a.AppliedBy == "user");
                stats["rule_applied"] = allAssociations.Count(a => a.AppliedBy == "rule");
                stats["system_applied"] = allAssociations.Count(a => a.AppliedBy == "system");

                return stats;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get tagging statistics");
                throw;
            }
        }

        public async Task<int> ApplyRuleTaggingAsync(int ruleId, IEnumerable<int> articleIds, IEnumerable<int> tagIds, double confidence = 0.8)
        {
            int totalApplied = 0;

            foreach (var articleId in articleIds)
            {
                foreach (var tagId in tagIds)
                {
                    if (await TagArticleAsync(articleId, tagId, "rule", ruleId, confidence))
                        totalApplied++;
                }
            }

            _logger.Information("Rule {RuleId} applied {Count} tags to {ArticleCount} articles",
                ruleId, totalApplied, articleIds.Count());

            return totalApplied;
        }

        public async Task<int> RemoveRuleTagsAsync(int ruleId)
        {
            try
            {
                var ruleTags = await _articleTagRepository.GetTagsAppliedByRuleAsync(ruleId);
                int removed = 0;

                foreach (var articleTag in ruleTags)
                {
                    if (await UntagArticleAsync(articleTag.ArticleId, articleTag.TagId))
                        removed++;
                }

                _logger.Information("Removed {Count} tags applied by rule {RuleId}", removed, ruleId);
                return removed;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove tags for rule {RuleId}", ruleId);
                throw;
            }
        }

        public async Task<int> CleanupOrphanedAssociationsAsync()
        {
            // This would clean up ArticleTag records where either
            // the Article or Tag no longer exists
            // Implementation requires checking against Article and Tag repositories
            _logger.Debug("CleanupOrphanedAssociationsAsync requires ArticleRepository implementation");
            return 0;
        }

        public async Task<int> RecalculateTagUsageCountsAsync()
        {
            try
            {
                // This would recalculate all tag usage counts based on actual associations
                // Implementation requires iterating through all tags and counting associations
                _logger.Debug("RecalculateTagUsageCountsAsync requires bulk update implementation");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to recalculate tag usage counts");
                throw;
            }
        }
    }
}