using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository implementation for ArticleTag join entity.
    /// Manages the many-to-many relationship between articles and tags.
    /// </summary>
    public class ArticleTagRepository : BaseRepository<ArticleTag>, IArticleTagRepository
    {
        private readonly ILogger _logger;
        private readonly RssReaderDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the ArticleTagRepository class.
        /// </summary>
        /// <param name="dbContext">The database context for Entity Framework Core operations.</param>
        public ArticleTagRepository(RssReaderDbContext dbContext, ILogger logger) : base(dbContext)
        {
            _logger = logger.ForContext<ArticleTagRepository>();
            _dbContext = dbContext;
        }

        /// <summary>
        /// Retrieves all ArticleTag associations for a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <returns>A list of ArticleTag associations.</returns>
        public async Task<List<ArticleTag>> GetByArticleIdAsync(int articleId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .Where(at => at.ArticleId == articleId)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve ArticleTag by article ID: {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all ArticleTag associations for a specific tag.
        /// </summary>
        /// <param name="tagId">The ID of the tag.</param>
        /// <returns>A list of ArticleTag associations.</returns>
        public async Task<List<ArticleTag>> GetByTagIdAsync(int tagId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .Where(at => at.TagId == tagId)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve ArticleTag by tag ID: {TagId}", tagId);
                throw;
            }
        }

        /// <summary>
        /// Checks if a specific article-tag association exists.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagId">The ID of the tag.</param>
        /// <returns>True if the association exists, otherwise false.</returns>
        public async Task<bool> ExistsAsync(int articleId, int tagId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .Where(at => at.ArticleId == articleId && at.TagId == tagId)
                    .AsNoTracking()
                    .AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check ArticleTag existence: Article={ArticleId}, Tag={TagId}", articleId, tagId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a specific ArticleTag association by article and tag IDs.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagId">The ID of the tag.</param>
        /// <returns>The ArticleTag association if found, otherwise null.</returns>
        public async Task<ArticleTag?> GetByArticleAndTagAsync(int articleId, int tagId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .Where(at => at.ArticleId == articleId && at.TagId == tagId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve ArticleTag: Article={ArticleId}, Tag={TagId}", articleId, tagId);
                throw;
            }
        }

        /// <summary>
        /// Associates a tag with an article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagId">The ID of the tag.</param>
        /// <param name="appliedBy">The entity that applied the tag (default: "user").</param>
        /// <param name="ruleId">Optional ID of the rule that triggered the association.</param>
        /// <param name="confidence">Optional confidence score for the association.</param>
        /// <returns>True if the association was created, false if it already existed.</returns>
        public async Task<bool> AssociateTagWithArticleAsync(int articleId, int tagId, string appliedBy = "user", int? ruleId = null, double? confidence = null)
        {
            try
            {
                // Check if association already exists
                if (await ExistsAsync(articleId, tagId))
                {
                    _logger.Debug("Tag {TagId} already associated with article {ArticleId}", tagId, articleId);
                    return false;
                }

                var articleTag = new ArticleTag
                {
                    ArticleId = articleId,
                    TagId = tagId,
                    AppliedBy = appliedBy,
                    RuleId = ruleId,
                    Confidence = confidence,
                    AppliedAt = DateTime.UtcNow
                };

                await InsertAsync(articleTag);
                _logger.Debug("Tag {TagId} associated with article {ArticleId} by {AppliedBy}",
                    tagId, articleId, appliedBy);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to associate tag {TagId} with article {ArticleId}", tagId, articleId);
                throw;
            }
        }

        /// <summary>
        /// Removes a tag association from an article.
        /// CORREGIDO: Maneja clave compuesta correctamente sin usar DeleteByIdAsync
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagId">The ID of the tag.</param>
        /// <returns>True if the association was removed, false if it didn't exist.</returns>
        public async Task<bool> RemoveTagFromArticleAsync(int articleId, int tagId)
        {
            try
            {
                // Buscar la entidad trackeada o no trackeada
                var articleTag = await _dbSet
                    .FirstOrDefaultAsync(at => at.ArticleId == articleId && at.TagId == tagId);

                if (articleTag == null)
                {
                    _logger.Debug("Tag {TagId} not associated with article {ArticleId}", tagId, articleId);
                    return false;
                }

                // Eliminar directamente sin usar DeleteAsync del base que requiere ID simple
                _dbSet.Remove(articleTag);
                await _dbContext.SaveChangesAsync();

                _logger.Debug("Tag {TagId} removed from article {ArticleId}", tagId, articleId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove tag {TagId} from article {ArticleId}", tagId, articleId);
                throw;
            }
        }

        /// <summary>
        /// Removes all tags from a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <returns>The number of associations removed.</returns>
        public async Task<int> RemoveAllTagsFromArticleAsync(int articleId)
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ and ExecuteDelete
                var articleTags = await _dbSet
                    .Where(at => at.ArticleId == articleId)
                    .ToListAsync();

                if (articleTags.Any())
                {
                    _dbSet.RemoveRange(articleTags);
                    return await _dbContext.SaveChangesAsync();
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove all tags from article {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Removes a specific tag from all articles.
        /// </summary>
        /// <param name="tagId">The ID of the tag.</param>
        /// <returns>The number of associations removed.</returns>
        public async Task<int> RemoveTagFromAllArticlesAsync(int tagId)
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ and ExecuteDelete
                var articleTags = await _dbSet
                    .Where(at => at.TagId == tagId)
                    .ToListAsync();

                if (articleTags.Any())
                {
                    _dbSet.RemoveRange(articleTags);
                    return await _dbContext.SaveChangesAsync();
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove tag {TagId} from all articles", tagId);
                throw;
            }
        }

        /// <summary>
        /// Associates multiple tags with an article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagIds">Collection of tag IDs to associate.</param>
        /// <param name="appliedBy">The entity that applied the tags (default: "user").</param>
        /// <param name="ruleId">Optional ID of the rule that triggered the associations.</param>
        /// <returns>The number of successful associations.</returns>
        public async Task<int> AssociateTagsWithArticleAsync(int articleId, IEnumerable<int> tagIds, string appliedBy = "user", int? ruleId = null)
        {
            int count = 0;
            foreach (var tagId in tagIds)
            {
                if (await AssociateTagWithArticleAsync(articleId, tagId, appliedBy, ruleId))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Removes multiple tags from an article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <param name="tagIds">Collection of tag IDs to remove.</param>
        /// <returns>The number of successful removals.</returns>
        public async Task<int> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds)
        {
            int count = 0;
            foreach (var tagId in tagIds)
            {
                if (await RemoveTagFromArticleAsync(articleId, tagId))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Retrieves article IDs that have a specific tag name.
        /// </summary>
        /// <param name="tagName">The name of the tag.</param>
        /// <returns>A list of article IDs.</returns>
        public async Task<List<int>> GetArticleIdsByTagNameAsync(string tagName)
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ query
                return await _dbSet
                    .Where(at => at.Tag != null && at.Tag.Name == tagName)
                    .Select(at => at.ArticleId)
                    .Distinct()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve article IDs by tag name: {TagName}", tagName);
                throw;
            }
        }

        /// <summary>
        /// Retrieves detailed tag information for a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <returns>A list of Tag objects associated with the article.</returns>
        public async Task<List<Tag>> GetTagsForArticleWithDetailsAsync(int articleId)
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ and Include
                return await _dbSet
                    .Where(at => at.ArticleId == articleId)
                    .Include(at => at.Tag)
                    .Select(at => at.Tag!)
                    .Where(tag => tag != null)
                    .OrderBy(tag => tag.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags with details for article: {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves tag usage statistics for a specific article.
        /// </summary>
        /// <param name="articleId">The ID of the article.</param>
        /// <returns>A dictionary with tag names as keys and usage counts as values.</returns>
        public async Task<Dictionary<string, int>> GetTagStatisticsForArticleAsync(int articleId)
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ and GroupBy
                var stats = await _dbSet
                    .Where(at => at.ArticleId == articleId)
                    .Include(at => at.Tag)
                    .GroupBy(at => at.Tag!.Name)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .OrderByDescending(s => s.Count)
                    .ToListAsync();

                return stats.ToDictionary(s => s.Name, s => s.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tag statistics for article: {ArticleId}", articleId);
                throw;
            }
        }

        /// <summary>
        /// Retrieves recently applied tag associations.
        /// </summary>
        /// <param name="limit">Maximum number of associations to retrieve (default: 50).</param>
        /// <returns>A list of recently applied ArticleTag associations.</returns>
        public async Task<List<ArticleTag>> GetRecentlyAppliedTagsAsync(int limit = 50)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .OrderByDescending(at => at.AppliedAt)
                    .Take(limit)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recently applied tags");
                throw;
            }
        }

        /// <summary>
        /// Retrieves tag associations applied by a specific rule.
        /// </summary>
        /// <param name="ruleId">The ID of the rule.</param>
        /// <returns>A list of ArticleTag associations applied by the rule.</returns>
        public async Task<List<ArticleTag>> GetTagsAppliedByRuleAsync(int ruleId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                return await _dbSet
                    .Where(at => at.RuleId == ruleId)
                    .OrderByDescending(at => at.AppliedAt)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags applied by rule: {RuleId}", ruleId);
                throw;
            }
        }
    }
}