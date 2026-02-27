using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="Article"/> entities using Entity Framework Core with SQLite.
/// Optimized for low-resource environments with server-side filtering and minimal memory usage.
/// </summary>
internal class ArticleRepository : BaseRepository<Article>, IArticleRepository
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core database context.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public ArticleRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("ArticleRepository initialized");
#endif
    }

    #endregion

    #region Read Operations

    /// <inheritdoc />
    public new async Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .Include(a => a.Feed)
                .Include(a => a.ArticleTags)
                    .ThenInclude(at => at.Tag)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByIdAsync cancelled for article ID {ArticleId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving article {ArticleId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<Article?> GetByIdReadOnlyAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Include(a => a.ArticleTags)
                    .ThenInclude(at => at.Tag)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByIdReadOnlyAsync cancelled for article ID {ArticleId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving article {ArticleId} (read-only)", id);
            throw;
        }
    }

    /// <inheritdoc />
    public new async Task<List<Article>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllAsync cancelled for articles");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving all articles");
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<List<Article>> GetWhereAsync(Expression<Func<Article, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(predicate)
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetWhereAsync cancelled for articles");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving filtered articles");
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> CountWhereAsync(Expression<Func<Article, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .CountAsync(predicate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("CountWhereAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error counting filtered articles");
            throw;
        }
    }

    #endregion

    #region Specialized Retrieval

    /// <inheritdoc />
    public async Task<List<Article>> GetByFeedAsync(int feedId, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.FeedId == feedId)
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting articles for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetByFeedsAsync(List<int> feedIds, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => feedIds.Contains(a.FeedId))
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByFeedsAsync cancelled for {Count} feeds", feedIds.Count);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting articles for {FeedCount} feeds", feedIds.Count);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetUnreadAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.Status == ArticleStatus.Unread)
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unread articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetStarredAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.IsStarred)
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetStarredAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting starred articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.IsFavorite)
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetFavoritesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting favorite articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetUnreadByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread)
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unread articles for feed {FeedId}", feedId);
            throw;
        }
    }

    #endregion

    #region Duplicate Detection & Lookup

    /// <inheritdoc />
    public async Task<Article?> GetByGuidAsync(string guid, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .FirstOrDefaultAsync(a => a.Guid == guid, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByGuidAsync cancelled for GUID {Guid}", guid);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting article by GUID: {Guid}", guid);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Article?> GetByContentHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .FirstOrDefaultAsync(a => a.ContentHash == hash, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByContentHashAsync cancelled for hash {Hash}", hash);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting article by content hash: {Hash}", hash);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByGuidAsync(string guid, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(a => a.Guid == guid, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByGuidAsync cancelled for GUID {Guid}", guid);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking existence by GUID {Guid}", guid);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByContentHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(a => a.ContentHash == hash, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByContentHashAsync cancelled for hash {Hash}", hash);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking existence by content hash {Hash}", hash);
            throw;
        }
    }

    #endregion

    #region Status & Toggle Operations

    /// <inheritdoc />
    public async Task<int> MarkAsAsync(int articleId, ArticleStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            var article = await _dbSet.FindAsync(new object[] { articleId }, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found for status update", articleId);
                return 0;
            }

            article.Status = status;
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking article {ArticleId} as {Status}", articleId, status);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ToggleStarAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var article = await _dbSet.FindAsync(new object[] { articleId }, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found for star toggle", articleId);
                return 0;
            }

            article.IsStarred = !article.IsStarred;
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ToggleStarAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error toggling star for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ToggleFavoriteAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var article = await _dbSet.FindAsync(new object[] { articleId }, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found for favorite toggle", articleId);
                return 0;
            }

            article.IsFavorite = !article.IsFavorite;
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ToggleFavoriteAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error toggling favorite for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        try
        {
            var updatedCount = await _dbSet
                .Where(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(a => a.Status, ArticleStatus.Read),
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Marked {Count} articles as read for feed {FeedId}", updatedCount, feedId);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAllAsReadByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking all as read for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var updatedCount = await _dbSet
                .Where(a => a.Status == ArticleStatus.Unread)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(a => a.Status, ArticleStatus.Read),
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Marked {Count} articles as read globally", updatedCount);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAllAsReadAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking all articles as read");
            throw;
        }
    }

    #endregion

    #region Counters & Aggregates

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .CountAsync(a => a.Status == ArticleStatus.Unread, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting total unread count");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .CountAsync(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unread count for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(a => a.Status == ArticleStatus.Unread)
                .GroupBy(a => a.FeedId)
                .Select(g => new { FeedId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FeedId, x => x.Count, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountsByFeedAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unread counts by feed");
            throw;
        }
    }

    #endregion

    #region Search & Advanced Filtering

    /// <inheritdoc />
    public async Task<List<Article>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return new List<Article>();
        }

        try
        {
            var lowerSearch = searchText.ToLowerInvariant();

            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a =>
                    EF.Functions.Like(a.Title.ToLower(), $"%{lowerSearch}%") ||
                    (a.Content != null && EF.Functions.Like(a.Content.ToLower(), $"%{lowerSearch}%")) ||
                    (a.Summary != null && EF.Functions.Like(a.Summary.ToLower(), $"%{lowerSearch}%")) ||
                    (a.Author != null && EF.Functions.Like(a.Author.ToLower(), $"%{lowerSearch}%")) ||
                    (a.Categories != null && EF.Functions.Like(a.Categories.ToLower(), $"%{lowerSearch}%")))
                .OrderByDescending(a => a.PublishedDate)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SearchAsync cancelled for: {SearchText}", searchText);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching articles for: {SearchText}", searchText);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetByCategoriesAsync(string categories, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.Categories != null && a.Categories.Contains(categories))
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByCategoriesAsync cancelled for: {Categories}", categories);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting articles by categories: {Categories}", categories);
            throw;
        }
    }

    #endregion

    #region Processing & Notification Workflow

    /// <inheritdoc />
    public async Task<List<Article>> GetUnprocessedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => !a.ProcessedByRules)
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnprocessedArticlesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unprocessed articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetUnnotifiedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => !a.IsNotified)
                .OrderByDescending(a => a.PublishedDate)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnnotifiedArticlesAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting unnotified articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAsProcessedAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var article = await _dbSet.FindAsync(new object[] { articleId }, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found for processing mark", articleId);
                return 0;
            }

            article.ProcessedByRules = true;
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsProcessedAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking article {ArticleId} as processed", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAsNotifiedAsync(int articleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var article = await _dbSet.FindAsync(new object[] { articleId }, cancellationToken).ConfigureAwait(false);
            if (article == null)
            {
                _logger.Warning("Article {ArticleId} not found for notification mark", articleId);
                return 0;
            }

            article.IsNotified = true;
            return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsNotifiedAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error marking article {ArticleId} as notified", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> BulkMarkAsProcessedAsync(List<int> articleIds, CancellationToken cancellationToken = default)
    {
        if (articleIds == null || !articleIds.Any())
        {
            return 0;
        }

        try
        {
            var updatedCount = await _dbSet
                .Where(a => articleIds.Contains(a.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(a => a.ProcessedByRules, true),
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Bulk marked {Count} articles as processed", updatedCount);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("BulkMarkAsProcessedAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error bulk marking articles as processed");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds, CancellationToken cancellationToken = default)
    {
        if (articleIds == null || !articleIds.Any())
        {
            return 0;
        }

        try
        {
            var updatedCount = await _dbSet
                .Where(a => articleIds.Contains(a.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(a => a.IsNotified, true),
                    cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Bulk marked {Count} articles as notified", updatedCount);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("BulkMarkAsNotifiedAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error bulk marking articles as notified");
            throw;
        }
    }

    #endregion

    #region Cleanup Operations

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        try
        {
            var deletedCount = await _dbSet
                .Where(a => a.PublishedDate < date && !a.IsStarred && !a.IsFavorite)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Deleted {Count} articles older than {Date}", deletedCount, date);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteOlderThanAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting articles older than {Date}", date);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        try
        {
            var deletedCount = await _dbSet
                .Where(a => a.FeedId == feedId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.Information("Deleted {Count} articles for feed {FeedId}", deletedCount, feedId);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting articles for feed {FeedId}", feedId);
            throw;
        }
    }

    #endregion

    #region Pagination Support

    /// <inheritdoc />
    public async Task<List<Article>> GetPagedAsync(int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .OrderByDescending(a => a.PublishedDate);

            if (status.HasValue)
            {
                query = (IOrderedQueryable<Article>)query.Where(a => a.Status == status.Value);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPagedAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting paged articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Article>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _dbSet
                .AsNoTracking()
                .Include(a => a.Feed)
                .Where(a => a.FeedId == feedId)
                .OrderByDescending(a => a.PublishedDate);

            if (status.HasValue)
            {
                query = (IOrderedQueryable<Article>)query.Where(a => a.Status == status.Value);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPagedByFeedAsync cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting paged articles for feed {FeedId}", feedId);
            throw;
        }
    }

    #endregion

    #region Bulk Insert/Update

    /// <inheritdoc />
    public async Task<int> InsertAllAsync(List<Article> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null || !entities.Any())
        {
            return 0;
        }

        try
        {
            await _dbSet.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            var result = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Debug("Inserted {Count} articles in bulk", entities.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("InsertAllAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting {Count} articles in bulk", entities.Count);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> UpdateAllAsync(List<Article> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null || !entities.Any())
        {
            return 0;
        }

        try
        {
            _dbSet.UpdateRange(entities);
            var result = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Debug("Updated {Count} articles in bulk", entities.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateAllAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating {Count} articles in bulk", entities.Count);
            throw;
        }
    }

    #endregion

    #region Entity Management

    /// <inheritdoc />
    public async Task DetachEntityAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = _context.ChangeTracker.Entries<Article>()
                .FirstOrDefault(e => e.Entity.Id == id);

            if (entry != null)
            {
                entry.State = EntityState.Detached;
                _logger.Debug("Detached article ID: {ArticleId}", id);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error detaching article with ID: {ArticleId}", id);
            throw;
        }
    }

    #endregion
}