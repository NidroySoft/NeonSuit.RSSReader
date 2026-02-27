using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Article;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Services;

/// <summary>
/// Service implementation for managing RSS articles.
/// Coordinates business logic, state changes, search, cleanup, and statistics while delegating persistence to repositories.
/// </summary>
internal class ArticleService : IArticleService
{
    private readonly IArticleRepository _articleRepository;
    private readonly IFeedRepository _feedRepository;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleService"/> class.
    /// </summary>
    /// <param name="articleRepository">Repository for article persistence operations.</param>
    /// <param name="feedRepository">Repository for feed metadata.</param>
    /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
    /// <param name="logger">Serilog logger instance for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public ArticleService(
        IArticleRepository articleRepository,
        IFeedRepository feedRepository,
        IMapper mapper,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(articleRepository);
        ArgumentNullException.ThrowIfNull(feedRepository);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(logger);

        _articleRepository = articleRepository;
        _feedRepository = feedRepository;
        _mapper = mapper;
        _logger = logger.ForContext<ArticleService>();

#if DEBUG
        _logger.Debug("ArticleService initialized");
#endif
    }

    #endregion

    #region Read Collection Operations

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetAllArticlesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving all articles");
            var articles = await _articleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve all articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetArticlesByFeedAsync(int feedId, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            _logger.Debug("Retrieving articles for feed {FeedId} with limit {Limit}", feedId, limit);
            var articles = await _articleRepository.GetByFeedAsync(feedId, limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} articles for feed {FeedId}", result.Count, feedId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticlesByFeedAsync operation was cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve articles for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetArticlesByCategoryAsync(int categoryId, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(categoryId);

        try
        {
            _logger.Debug("Retrieving articles for category {CategoryId} with limit {Limit}", categoryId, limit);

            var feeds = await _feedRepository.GetByCategoryAsync(categoryId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!feeds.Any())
            {
                _logger.Information("No feeds found for category {CategoryId}", categoryId);
                return new List<ArticleSummaryDto>();
            }

            var feedIds = feeds.Select(f => f.Id).ToList();
            var articles = await _articleRepository.GetByFeedsAsync(feedIds, limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);

            _logger.Information("Retrieved {Count} articles for category {CategoryId}", result.Count, categoryId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticlesByCategoryAsync operation was cancelled for category {CategoryId}", categoryId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve articles for category {CategoryId}", categoryId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetUnreadArticlesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving unread articles with limit {Limit}", limit);
            var articles = await _articleRepository.GetUnreadAsync(limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} unread articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unread articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetStarredArticlesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving starred articles");
            var articles = await _articleRepository.GetStarredAsync(cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} starred articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetStarredArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve starred articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetFavoriteArticlesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving favorite articles");
            var articles = await _articleRepository.GetFavoritesAsync(cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} favorite articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetFavoriteArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve favorite articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetUnreadByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            _logger.Debug("Retrieving unread articles for feed {FeedId}", feedId);
            var articles = await _articleRepository.GetUnreadByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} unread articles for feed {FeedId}", result.Count, feedId);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadByFeedAsync operation was cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unread articles for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> SearchArticlesAsync(string searchText, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchText);

        try
        {
            _logger.Debug("Searching articles for: '{SearchText}'", searchText);
            var articles = await _articleRepository.SearchAsync(searchText, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Found {Count} articles matching '{SearchText}'", result.Count, searchText);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SearchArticlesAsync operation was cancelled for: '{SearchText}'", searchText);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to search articles for: '{SearchText}'", searchText);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetPagedArticlesAsync(int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        try
        {
            _logger.Debug("Retrieving paged articles - Page: {Page}, Size: {PageSize}, Status: {Status}", page, pageSize, status);

            var articles = await _articleRepository.GetPagedAsync(page, pageSize, status, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);

            _logger.Information("Retrieved {Count} paged articles (Page {Page}, Size {PageSize})", result.Count, page, pageSize);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPagedArticlesAsync operation was cancelled - Page: {Page}, Size: {PageSize}", page, pageSize);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve paged articles - Page: {Page}, Size: {PageSize}", page, pageSize);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetPagedByFeedAsync(int feedId, int page, int pageSize, ArticleStatus? status = null, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        try
        {
            _logger.Debug("Retrieving paged articles for feed {FeedId} - Page: {Page}, Size: {PageSize}, Status: {Status}",
                feedId, page, pageSize, status);

            var articles = await _articleRepository.GetPagedByFeedAsync(feedId, page, pageSize, status, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);

            _logger.Information("Retrieved {Count} paged articles for feed {FeedId} (Page {Page}, Size {PageSize})",
                result.Count, feedId, page, pageSize);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPagedByFeedAsync operation was cancelled for feed {FeedId} - Page: {Page}, Size: {PageSize}", feedId, page, pageSize);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve paged articles for feed {FeedId} - Page: {Page}, Size: {PageSize}", feedId, page, pageSize);
            throw;
        }
    }

    #endregion

    #region State Change Operations

    /// <inheritdoc />
    public async Task<bool> MarkAsReadAsync(int articleId, bool isRead, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Marking article {ArticleId} as {Status}", articleId, isRead ? "Read" : "Unread");

            var status = isRead ? ArticleStatus.Read : ArticleStatus.Unread;
            var rowsAffected = await _articleRepository.MarkAsAsync(articleId, status, cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                _logger.Information("Article {ArticleId} marked as {Status}", articleId, isRead ? "Read" : "Unread");
                return true;
            }

            _logger.Warning("Article {ArticleId} not found or already in desired state", articleId);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsReadAsync operation was cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark article {ArticleId} as {Status}", articleId, isRead ? "Read" : "Unread");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ToggleStarredAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Toggling starred status for article {ArticleId}", articleId);

            var rowsAffected = await _articleRepository.ToggleStarAsync(articleId, cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                _logger.Information("Toggled starred status for article {ArticleId}", articleId);
                return true;
            }

            _logger.Warning("Article {ArticleId} not found when toggling starred status", articleId);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ToggleStarredAsync operation was cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to toggle starred status for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ToggleFavoriteAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Toggling favorite status for article {ArticleId}", articleId);

            var rowsAffected = await _articleRepository.ToggleFavoriteAsync(articleId, cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                _logger.Information("Toggled favorite status for article {ArticleId}", articleId);
                return true;
            }

            _logger.Warning("Article {ArticleId} not found when toggling favorite status", articleId);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ToggleFavoriteAsync operation was cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to toggle favorite status for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsNotifiedAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Marking article {ArticleId} as notified", articleId);

            var rowsAffected = await _articleRepository.MarkAsNotifiedAsync(articleId, cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                _logger.Information("Article {ArticleId} marked as notified", articleId);
                return true;
            }

            _logger.Warning("Article {ArticleId} not found when marking as notified", articleId);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsNotifiedAsync operation was cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark article {ArticleId} as notified", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> MarkAsProcessedAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            _logger.Debug("Marking article {ArticleId} as processed by rules", articleId);

            var rowsAffected = await _articleRepository.MarkAsProcessedAsync(articleId, cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0)
            {
                _logger.Information("Article {ArticleId} marked as processed", articleId);
                return true;
            }

            _logger.Warning("Article {ArticleId} not found when marking as processed", articleId);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAsProcessedAsync operation was cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark article {ArticleId} as processed", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            _logger.Debug("Marking all articles as read for feed {FeedId}", feedId);

            var rowsAffected = await _articleRepository.MarkAllAsReadByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);

            _logger.Information("Marked {Count} articles as read for feed {FeedId}", rowsAffected, feedId);
            return rowsAffected;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAllAsReadByFeedAsync operation was cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark all articles as read for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Marking all articles as read (bulk operation)");

            var rowsAffected = await _articleRepository.MarkAllAsReadAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Marked {Count} articles as read", rowsAffected);
            return rowsAffected;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("MarkAllAsReadAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to mark all articles as read");
            throw;
        }
    }

    #endregion

    #region Bulk Operations

    /// <inheritdoc />
    public async Task<int> BulkMarkAsProcessedAsync(List<int> articleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articleIds);

        if (!articleIds.Any())
            return 0;

        try
        {
            _logger.Debug("Bulk marking {Count} articles as processed", articleIds.Count);

            var rowsAffected = await _articleRepository.BulkMarkAsProcessedAsync(articleIds, cancellationToken).ConfigureAwait(false);

            _logger.Information("Bulk marked {Count} articles as processed", rowsAffected);
            return rowsAffected;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("BulkMarkAsProcessedAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to bulk mark articles as processed");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> BulkMarkAsNotifiedAsync(List<int> articleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(articleIds);

        if (!articleIds.Any())
            return 0;

        try
        {
            _logger.Debug("Bulk marking {Count} articles as notified", articleIds.Count);

            var rowsAffected = await _articleRepository.BulkMarkAsNotifiedAsync(articleIds, cancellationToken).ConfigureAwait(false);

            _logger.Information("Bulk marked {Count} articles as notified", rowsAffected);
            return rowsAffected;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("BulkMarkAsNotifiedAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to bulk mark articles as notified");
            throw;
        }
    }

    #endregion

    #region Cleanup & Maintenance Operations

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Deleting articles older than {CutoffDate}", cutoffDate);

            var deletedCount = await _articleRepository.DeleteOlderThanAsync(cutoffDate, cancellationToken).ConfigureAwait(false);

            _logger.Information("Deleted {Count} articles older than {CutoffDate}", deletedCount, cutoffDate);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteOlderThanAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete articles older than {CutoffDate}", cutoffDate);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteOldArticlesAsync(int daysOld = 30, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(daysOld);

        try
        {
            _logger.Debug("Deleting articles older than {DaysOld} days", daysOld);

            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var deletedCount = await _articleRepository.DeleteOlderThanAsync(cutoffDate, cancellationToken).ConfigureAwait(false);

            _logger.Information("Deleted {Count} old articles (older than {DaysOld} days)", deletedCount, daysOld);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteOldArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete old articles (older than {DaysOld} days)", daysOld);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            _logger.Debug("Deleting articles for feed {FeedId}", feedId);

            var deletedCount = await _articleRepository.DeleteByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);

            _logger.Information("Deleted {Count} articles for feed {FeedId}", deletedCount, feedId);
            return deletedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteByFeedAsync operation was cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete articles for feed {FeedId}", feedId);
            throw;
        }
    }

    #endregion

    #region Statistics & Counters

    /// <inheritdoc />
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving total unread article count");
            var count = await _articleRepository.GetUnreadCountAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("Total unread articles: {Count}", count);
            return count;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve total unread article count");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(feedId);

        try
        {
            _logger.Debug("Retrieving unread count for feed {FeedId}", feedId);
            var count = await _articleRepository.GetUnreadCountByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
            _logger.Information("Unread articles for feed {FeedId}: {Count}", feedId, count);
            return count;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountByFeedAsync operation was cancelled for feed {FeedId}", feedId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unread count for feed {FeedId}", feedId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, int>> GetUnreadCountsByFeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving unread counts grouped by feed");
            var counts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);
            _logger.Information("Retrieved unread counts for {Count} feeds", counts.Count);
            return counts;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountsByFeedAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unread counts by feed");
            throw;
        }
    }

    #endregion

    #region Advanced / Filtered Reads

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetUnprocessedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving unprocessed articles with limit {Limit}", limit);
            var articles = await _articleRepository.GetUnprocessedArticlesAsync(limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} unprocessed articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnprocessedArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unprocessed articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetUnnotifiedArticlesAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.Debug("Retrieving unnotified articles with limit {Limit}", limit);
            var articles = await _articleRepository.GetUnnotifiedArticlesAsync(limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} unnotified articles", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnnotifiedArticlesAsync operation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve unnotified articles");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ArticleSummaryDto>> GetArticlesByCategoriesAsync(string categories, int limit = 100, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categories);

        try
        {
            _logger.Debug("Retrieving articles by categories: '{Categories}' with limit {Limit}", categories, limit);
            var articles = await _articleRepository.GetByCategoriesAsync(categories, limit, cancellationToken).ConfigureAwait(false);
            var result = _mapper.Map<List<ArticleSummaryDto>>(articles);
            _logger.Information("Retrieved {Count} articles for categories: '{Categories}'", result.Count, categories);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetArticlesByCategoriesAsync operation was cancelled for: '{Categories}'", categories);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve articles by categories: '{Categories}'", categories);
            throw;
        }
    }

    #endregion

    #region Duplicate Detection

    /// <inheritdoc />
    public async Task<ArticleDetailDto?> GetByGuidAsync(string guid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guid);

        try
        {
            _logger.Debug("Retrieving article by GUID: {Guid}", guid);
            var article = await _articleRepository.GetByGuidAsync(guid, cancellationToken).ConfigureAwait(false);

            if (article == null)
            {
                _logger.Debug("Article with GUID {Guid} not found", guid);
                return null;
            }

            var result = _mapper.Map<ArticleDetailDto>(article);
            _logger.Debug("Found article with GUID {Guid}: {Title}", guid, article.Title);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByGuidAsync operation was cancelled for GUID: {Guid}", guid);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve article by GUID: {Guid}", guid);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ArticleDetailDto?> GetByContentHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        try
        {
            _logger.Debug("Retrieving article by content hash: {Hash}", hash);
            var article = await _articleRepository.GetByContentHashAsync(hash, cancellationToken).ConfigureAwait(false);

            if (article == null)
            {
                _logger.Debug("Article with content hash {Hash} not found", hash);
                return null;
            }

            var result = _mapper.Map<ArticleDetailDto>(article);
            _logger.Debug("Found article with content hash {Hash}: {Title}", hash, article.Title);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByContentHashAsync operation was cancelled for hash: {Hash}", hash);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve article by content hash: {Hash}", hash);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByGuidAsync(string guid, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guid);

        try
        {
            _logger.Debug("Checking existence by GUID: {Guid}", guid);
            var exists = await _articleRepository.ExistsByGuidAsync(guid, cancellationToken).ConfigureAwait(false);
            _logger.Debug("Article with GUID {Guid} exists: {Exists}", guid, exists);
            return exists;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByGuidAsync operation was cancelled for GUID: {Guid}", guid);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check existence by GUID: {Guid}", guid);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByContentHashAsync(string hash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        try
        {
            _logger.Debug("Checking existence by content hash: {Hash}", hash);
            var exists = await _articleRepository.ExistsByContentHashAsync(hash, cancellationToken).ConfigureAwait(false);
            _logger.Debug("Article with content hash {Hash} exists: {Exists}", hash, exists);
            return exists;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByContentHashAsync operation was cancelled for hash: {Hash}", hash);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check existence by content hash: {Hash}", hash);
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
            _logger.Debug("Detaching article entity with ID: {ArticleId}", id);
            await _articleRepository.DetachEntityAsync(id, cancellationToken).ConfigureAwait(false);
            _logger.Debug("Article entity detached successfully");
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DetachEntityAsync operation was cancelled for ID: {ArticleId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to detach article entity with ID: {ArticleId}", id);
            throw;
        }
    }

    #endregion

    #region TODO: Future Improvements

    // TODO (High - v2.0): Interface Segregation Principle (ISP) Violation
    // Current interface has ~40 methods mixing read, write, bulk, statistics, etc.
    // Proposed split:
    // - IArticleReadService: GetAllArticlesAsync, GetArticlesByFeedAsync, GetPagedArticlesAsync, etc.
    // - IArticleStateService: MarkAsReadAsync, ToggleStarredAsync, ToggleFavoriteAsync
    // - IArticleMaintenanceService: DeleteOlderThanAsync, DeleteByFeedAsync
    // - IArticleStatisticsService: GetUnreadCountAsync, GetUnreadCountsByFeedAsync
    // - IArticleProcessingService: MarkAsProcessedAsync, MarkAsNotifiedAsync, Bulk operations
    // Benefit: Smaller, focused interfaces for better testability and reduced coupling
    // Risk level: High - requires updating all consumers
    // Estimated effort: 2-3 days

    // TODO (Medium - v1.x): Add caching for frequently accessed data
    // What to do: Cache unread counts and popular articles with IMemoryCache
    // Why: Reduce database load on frequently called endpoints
    // Implementation: Add cache keys with sliding expiration and invalidation on changes
    // Risk level: Low - cache miss falls back to database
    // Estimated effort: 1 day

    // TODO (Low - v1.x): Add validation for article operations
    // What to do: Validate that feed exists before operations that require it
    // Why: Prevent orphaned articles and improve error messages
    // Implementation: Add CheckFeedExistsAsync helper method
    // Risk level: Low - adds validation without changing behavior
    // Estimated effort: 2 hours

    #endregion
}