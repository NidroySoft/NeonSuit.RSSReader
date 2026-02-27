using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for ArticleTag join entity.
/// Manages the many-to-many relationship between articles and tags, including metadata about tagging source and timing.
/// </summary>
internal class ArticleTagRepository : BaseRepository<ArticleTag>, IArticleTagRepository
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleTagRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core database context.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public ArticleTagRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("ArticleTagRepository initialized");
#endif
    }

    #endregion

    #region Read Single / Existence Checks

    /// <inheritdoc />
    public async Task<ArticleTag?> GetByArticleAndTagAsync(int articleId, int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(at => at.ArticleId == articleId && at.TagId == tagId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByArticleAndTagAsync cancelled for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve ArticleTag for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(int articleId, int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(at => at.ArticleId == articleId && at.TagId == tagId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsAsync cancelled for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check existence for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
    }

    #endregion

    #region Read Collections - By Article / Tag

    /// <inheritdoc />
    public async Task<List<ArticleTag>> GetByArticleIdAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(at => at.Tag)
                .Where(at => at.ArticleId == articleId)
                .OrderBy(at => at.AppliedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByArticleIdAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve associations for ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleTag>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(at => at.Article)
                .Where(at => at.TagId == tagId)
                .OrderByDescending(at => at.AppliedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByTagIdAsync cancelled for TagId={TagId}", tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve associations for TagId={TagId}", tagId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetTagsForArticleWithDetailsAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(at => at.ArticleId == articleId)
                .Select(at => at.Tag!)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagsForArticleWithDetailsAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve detailed tags for ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    #endregion

    #region Read Collections - Recent / Rule / Tag Name

    /// <inheritdoc />
    public async Task<List<ArticleTag>> GetRecentlyAppliedTagsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(at => at.Tag)
                .Include(at => at.Article)
                .OrderByDescending(at => at.AppliedAt)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetRecentlyAppliedTagsAsync cancelled (limit={Limit})", limit);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve recently applied tags");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleTag>> GetTagsAppliedByRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(at => at.Tag)
                .Include(at => at.Article)
                .Where(at => at.RuleId == ruleId)
                .OrderByDescending(at => at.AppliedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagsAppliedByRuleAsync cancelled for RuleId={RuleId}", ruleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve tags applied by rule {RuleId}", ruleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<int>> GetArticleIdsByTagNameAsync(string tagName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(at => at.Tag != null && at.Tag.Name == tagName)
                .Select(at => at.ArticleId)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticleIdsByTagNameAsync cancelled for tag '{TagName}'", tagName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve article IDs for tag name '{TagName}'", tagName);
            throw;
        }
    }

    #endregion

    #region Statistics

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetTagStatisticsForArticleAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(at => at.ArticleId == articleId && at.Tag != null)
                .GroupBy(at => at.Tag!.Name)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(
                    x => x.Name,
                    x => x.Count,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagStatisticsForArticleAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to compute tag statistics for ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    #endregion

    #region Write - Single Association

    /// <inheritdoc />
    public async Task<bool> AssociateTagWithArticleAsync(
        int articleId,
        int tagId,
        string appliedBy = "user",
        int? ruleId = null,
        double? confidence = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await ExistsAsync(articleId, tagId, cancellationToken).ConfigureAwait(false))
            {
                _logger.Debug("Association already exists: ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
                return false;
            }

            var association = new ArticleTag
            {
                ArticleId = articleId,
                TagId = tagId,
                AppliedBy = appliedBy,
                RuleId = ruleId,
                Confidence = confidence,
                AppliedAt = DateTime.UtcNow
            };

            await _dbSet.AddAsync(association, cancellationToken).ConfigureAwait(false);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Associated tag {TagId} to article {ArticleId} by {AppliedBy}", tagId, articleId, appliedBy);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("AssociateTagWithArticleAsync cancelled for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error associating tag {TagId} to article {ArticleId}", tagId, articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to associate tag {TagId} to article {ArticleId}", tagId, articleId);
            throw;
        }
    }

    #endregion

    #region Write - Bulk Operations

    /// <inheritdoc />
    public async Task<int> AssociateTagsWithArticleAsync(
        int articleId,
        IEnumerable<int> tagIds,
        string appliedBy = "user",
        int? ruleId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tagIdList = tagIds.Distinct().ToList();
            if (!tagIdList.Any()) return 0;

            // Get existing associations to avoid duplicates
            var existing = await _dbSet
                .Where(at => at.ArticleId == articleId && tagIdList.Contains(at.TagId))
                .Select(at => at.TagId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var newTags = tagIdList.Except(existing).ToList();
            if (!newTags.Any()) return 0;

            var newAssociations = newTags.Select(tagId => new ArticleTag
            {
                ArticleId = articleId,
                TagId = tagId,
                AppliedBy = appliedBy,
                RuleId = ruleId,
                AppliedAt = DateTime.UtcNow
            }).ToList();

            await _dbSet.AddRangeAsync(newAssociations, cancellationToken).ConfigureAwait(false);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Added {NewCount} tags to article {ArticleId}", newTags.Count, articleId);
            return newTags.Count;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("AssociateTagsWithArticleAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed bulk association for ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveTagFromArticleAsync(int articleId, int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _dbSet
                .Where(at => at.ArticleId == articleId && at.TagId == tagId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rows > 0)
            {
                _logger.Information("Removed tag {TagId} from article {ArticleId}", tagId, articleId);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RemoveTagFromArticleAsync cancelled for ArticleId={ArticleId}, TagId={TagId}", articleId, tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to remove tag {TagId} from article {ArticleId}", tagId, articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var tagIdList = tagIds.Distinct().ToList();
            if (!tagIdList.Any()) return 0;

            var rows = await _dbSet
                .Where(at => at.ArticleId == articleId && tagIdList.Contains(at.TagId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Removed {Count} tags from article {ArticleId}", rows, articleId);
            return rows;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RemoveTagsFromArticleAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed bulk removal for ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveAllTagsFromArticleAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _dbSet
                .Where(at => at.ArticleId == articleId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Cleared {Count} tags from article {ArticleId}", rows, articleId);
            return rows;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RemoveAllTagsFromArticleAsync cancelled for ArticleId={ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to clear all tags from ArticleId={ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveTagFromAllArticlesAsync(int tagId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _dbSet
                .Where(at => at.TagId == tagId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Removed tag {TagId} from {Count} articles", tagId, rows);
            return rows;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("RemoveTagFromAllArticlesAsync cancelled for TagId={TagId}", tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to remove tag {TagId} from all articles", tagId);
            throw;
        }
    }

    #endregion
}