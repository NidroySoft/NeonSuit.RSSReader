using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.RssFeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IFeedService"/> providing comprehensive RSS feed management.
    /// Handles feed CRUD operations, synchronization, health monitoring, and maintenance tasks
    /// with full support for active/inactive feed filtering.
    /// </summary>
    internal class FeedService : IFeedService
    {
        private readonly IFeedRepository _feedRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IRssFeedParser _feedParser;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedService"/> class.
        /// </summary>
        /// <param name="feedRepository">The feed repository for data access operations.</param>
        /// <param name="articleRepository">The article repository for article management.</param>
        /// <param name="feedParser">The feed parser for RSS/Atom feed parsing.</param>
        /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
        /// <param name="logger">The logger instance for diagnostic tracking.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
        public FeedService(
            IFeedRepository feedRepository,
            IArticleRepository articleRepository,
            IRssFeedParser feedParser,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(feedRepository);
            ArgumentNullException.ThrowIfNull(articleRepository);
            ArgumentNullException.ThrowIfNull(feedParser);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _feedRepository = feedRepository;
            _articleRepository = articleRepository;
            _feedParser = feedParser;
            _mapper = mapper;
            _logger = logger.ForContext<FeedService>();

#if DEBUG
            _logger.Debug("FeedService initialized");
#endif
        }

        #endregion

        #region Basic CRUD Operations

        /// <inheritdoc />
        public async Task<List<FeedSummaryDto>> GetAllFeedsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all feeds (IncludeInactive: {IncludeInactive})", includeInactive);

                var feeds = await _feedRepository.GetAllAsync(includeInactive, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);

                var feedDtos = _mapper.Map<List<FeedSummaryDto>>(feeds);
                foreach (var dto in feedDtos)
                {
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Information("Retrieved {Count} feeds", feedDtos.Count);
                return feedDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetAllFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving all feeds");
                throw new InvalidOperationException("Failed to retrieve feeds", ex);
            }
        }

        /// <inheritdoc />
        public async Task<FeedDto?> GetFeedByIdAsync(int id, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (id <= 0)
                {
                    _logger.Warning("Invalid feed ID provided: {FeedId}", id);
                    throw new ArgumentOutOfRangeException(nameof(id), "Feed ID must be greater than 0");
                }

                _logger.Debug("Retrieving feed by ID: {FeedId} (IncludeInactive: {IncludeInactive})", id, includeInactive);

                var feed = await _feedRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

                if (feed == null || (!includeInactive && !feed.IsActive))
                {
                    _logger.Debug("Feed {FeedId} not found or inactive", id);
                    return null;
                }

                var feedDto = _mapper.Map<FeedDto>(feed);
                feedDto.UnreadCount = await _articleRepository.GetUnreadCountByFeedAsync(id, cancellationToken).ConfigureAwait(false);
                feedDto.EffectiveRetentionDays = feed.ArticleRetentionDays ?? 30;

                _logger.Debug("Found feed {FeedId}: {Title}", id, feed.Title);
                return feedDto;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetFeedByIdAsync operation was cancelled for ID: {FeedId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed by ID: {FeedId}", id);
                throw new InvalidOperationException($"Failed to retrieve feed with ID {id}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<FeedDto> AddFeedAsync(CreateFeedDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                if (string.IsNullOrWhiteSpace(createDto.Url))
                {
                    _logger.Warning("Attempted to add feed with empty URL");
                    throw new ArgumentException("Feed URL cannot be empty", nameof(createDto));
                }

                var normalizedUrl = NormalizeUrl(createDto.Url);
                _logger.Debug("Adding new feed: {Url} (normalized: {NormalizedUrl})", createDto.Url, normalizedUrl);

                var exists = await _feedRepository.ExistsByUrlAsync(normalizedUrl, cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    _logger.Warning("Feed already exists: {Url}", normalizedUrl);
                    throw new InvalidOperationException("This feed is already in your list.");
                }

                Feed? feed = null;
                List<Article>? articles = null;

                try
                {
                    (feed, articles) = await _feedParser.ParseFeedAsync(normalizedUrl, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Network error while fetching feed: {Url}. Status: {StatusCode}", normalizedUrl, ex.StatusCode);
                    throw new InvalidOperationException(
                        $"Could not reach the feed server. Please check your internet connection and the URL. " +
                        $"Status: {ex.StatusCode}", ex);
                }
                catch (TaskCanceledException ex)
                {
                    _logger.Error(ex, "Request timeout while fetching feed: {Url}", normalizedUrl);
                    throw new InvalidOperationException(
                        "The feed server took too long to respond. Please try again later.", ex);
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Socket error while fetching feed: {Url}", normalizedUrl);
                    throw new InvalidOperationException(
                        "Network connection issue. Please check your internet connection.", ex);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.Debug(ex, "AddFeedAsync operation was cancelled for URL: {Url}", normalizedUrl);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error parsing feed: {Url}", normalizedUrl);
                    throw new InvalidOperationException($"Failed to parse feed: {ex.Message}", ex);
                }

                if (feed == null)
                {
                    _logger.Error("Failed to parse feed (null returned): {Url}", normalizedUrl);
                    throw new InvalidOperationException("Could not read the feed. Please check the URL.");
                }

                // Apply DTO values to parsed feed
                feed.Title = !string.IsNullOrWhiteSpace(createDto.Title) ? createDto.Title : (feed.Title ?? "Untitled Feed");
                feed.Description = !string.IsNullOrWhiteSpace(createDto.Description) ? createDto.Description : feed.Description;
                feed.WebsiteUrl = !string.IsNullOrWhiteSpace(createDto.WebsiteUrl) ? createDto.WebsiteUrl : feed.WebsiteUrl;
                feed.CategoryId = createDto.CategoryId;
                feed.UpdateFrequency = createDto.UpdateFrequency;
                feed.ArticleRetentionDays = createDto.ArticleRetentionDays;
                feed.IsPodcastFeed = createDto.IsPodcastFeed;
                feed.IconUrl = !string.IsNullOrWhiteSpace(createDto.IconUrl) ? createDto.IconUrl : feed.IconUrl;
                feed.IsActive = true;
                feed.CreatedAt = DateTime.UtcNow;
                feed.LastUpdated = DateTime.UtcNow;
                feed.NextUpdateSchedule = CalculateNextUpdate(feed.UpdateFrequency);
                feed.Id = 0;

                await _feedRepository.InsertAsync(feed, cancellationToken).ConfigureAwait(false);
                _logger.Information("Feed added successfully: {Title} ({Url})", feed.Title, normalizedUrl);

                if (articles?.Any() == true)
                {
                    articles.ForEach(a =>
                    {
                        a.FeedId = feed.Id;
                        a.Id = 0;
                    });

                    var insertedCount = await _articleRepository.InsertAllAsync(articles, cancellationToken).ConfigureAwait(false);
                    await UpdateFeedCountsAsync(feed.Id, cancellationToken).ConfigureAwait(false);
                    _logger.Information("Inserted {Count} initial articles for feed {FeedId}", insertedCount, feed.Id);
                }

                var feedDto = _mapper.Map<FeedDto>(feed);
                feedDto.UnreadCount = 0;
                feedDto.TotalArticleCount = articles?.Count ?? 0;
                feedDto.EffectiveRetentionDays = feed.ArticleRetentionDays ?? 30;

                return feedDto;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "AddFeedAsync operation was cancelled for URL: {Url}", createDto.Url);
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error adding feed: {Url}", createDto.Url);
                throw new InvalidOperationException($"Failed to add feed: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<FeedDto?> UpdateFeedAsync(int feedId, UpdateFeedDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for update: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Updating feed {FeedId}", feedId);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for update", feedId);
                    return null;
                }

                // Track original values for logging
                var originalTitle = feed.Title;

                // Apply updates
                if (updateDto.Title != null)
                    feed.Title = updateDto.Title;

                if (updateDto.Description != null)
                    feed.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description;

                if (updateDto.WebsiteUrl != null)
                    feed.WebsiteUrl = updateDto.WebsiteUrl ?? "Url missing";

                if (updateDto.IconUrl != null)
                    feed.IconUrl = string.IsNullOrWhiteSpace(updateDto.IconUrl) ? null : updateDto.IconUrl;

                if (updateDto.CategoryId.HasValue)
                    feed.CategoryId = updateDto.CategoryId.Value > 0 ? updateDto.CategoryId : null;

                if (updateDto.UpdateFrequency.HasValue)
                {
                    feed.UpdateFrequency = updateDto.UpdateFrequency.Value;
                    feed.NextUpdateSchedule = CalculateNextUpdate(feed.UpdateFrequency);
                }

                if (updateDto.IsActive.HasValue)
                    feed.IsActive = updateDto.IsActive.Value;

                if (updateDto.ArticleRetentionDays.HasValue)
                    feed.ArticleRetentionDays = updateDto.ArticleRetentionDays.Value > 0 ? updateDto.ArticleRetentionDays : null;

                if (updateDto.IsPodcastFeed.HasValue)
                    feed.IsPodcastFeed = updateDto.IsPodcastFeed.Value;

                if (updateDto.ResetFailureCount == true)
                {
                    feed.FailureCount = 0;
                    feed.LastError = null;
                }

                var result = await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false);

                if (result > 0)
                {
                    _logger.Information("Feed {FeedId} updated successfully (Title: {OriginalTitle} -> {NewTitle})",
                        feedId, originalTitle, feed.Title);

                    var feedDto = _mapper.Map<FeedDto>(feed);
                    feedDto.UnreadCount = await _articleRepository.GetUnreadCountByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
                    feedDto.EffectiveRetentionDays = feed.ArticleRetentionDays ?? 30;

                    return feedDto;
                }

                _logger.Warning("No changes made to feed {FeedId}", feedId);
                return null;    
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateFeedAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to update feed with ID {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteFeedAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for deletion: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Deleting feed {FeedId} and its articles", feedId);

                var articlesDeleted = await _articleRepository.DeleteByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Deleted {Count} articles for feed {FeedId}", articlesDeleted, feedId);

                var feedDeleted = await _feedRepository.DeleteFeedDirectAsync(feedId, cancellationToken).ConfigureAwait(false);

                if (feedDeleted > 0)
                {
                    _logger.Information("Feed {FeedId} and {ArticleCount} articles deleted successfully",
                        feedId, articlesDeleted);
                    return true;
                }

                _logger.Warning("Feed {FeedId} not found for deletion", feedId);
                return false;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "DeleteFeedAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to delete feed with ID {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> FeedExistsAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    _logger.Warning("Attempted to check existence with empty URL");
                    throw new ArgumentException("URL cannot be empty", nameof(url));
                }

                var normalizedUrl = NormalizeUrl(url);
                _logger.Debug("Checking if feed exists: {Url}", normalizedUrl);
                var exists = await _feedRepository.ExistsByUrlAsync(normalizedUrl, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Feed {Url} exists: {Exists}", normalizedUrl, exists);
                return exists;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "FeedExistsAsync operation was cancelled for URL: {Url}", url);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if feed exists: {Url}", url);
                throw new InvalidOperationException($"Failed to check feed existence for URL: {url}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<FeedDto?> GetFeedByUrlAsync(string url, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    _logger.Warning("Attempted to get feed with empty URL");
                    throw new ArgumentException("URL cannot be empty", nameof(url));
                }

                var normalizedUrl = NormalizeUrl(url);
                _logger.Debug("Retrieving feed by URL: {Url} (IncludeInactive: {IncludeInactive})", normalizedUrl, includeInactive);

                var feed = await _feedRepository.GetByUrlAsync(normalizedUrl,false, cancellationToken).ConfigureAwait(false);

                if (feed == null || (!includeInactive && !feed.IsActive))
                {
                    return null;
                }

                var feedDto = _mapper.Map<FeedDto>(feed);
                feedDto.UnreadCount = await _articleRepository.GetUnreadCountByFeedAsync(feed.Id, cancellationToken).ConfigureAwait(false);
                feedDto.EffectiveRetentionDays = feed.ArticleRetentionDays ?? 30;

                return feedDto;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetFeedByUrlAsync operation was cancelled for URL: {Url}", url);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feed by URL: {Url}", url);
                throw new InvalidOperationException($"Failed to retrieve feed by URL: {url}", ex);
            }
        }

        #endregion

        #region Feed Refresh and Synchronization

        /// <inheritdoc />
        public async Task<bool> RefreshFeedAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for refresh: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Refreshing feed {FeedId}", feedId);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for refresh", feedId);
                    return false;
                }

                List<Article> newArticles;
                try
                {
                    newArticles = await _feedParser.ParseArticlesAsync(feed.Url, feedId, cancellationToken).ConfigureAwait(false);
                    await _feedRepository.ResetFailureCountAsync(feedId, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Error(ex, "Network error refreshing feed {FeedId}: {Url}. Status: {StatusCode}",
                        feedId, feed.Url, ex.StatusCode);

                    await _feedRepository.IncrementFailureCountAsync(feedId, $"HTTP {ex.StatusCode}: {ex.Message}", cancellationToken).ConfigureAwait(false);

                    if (feed.FailureCount + 1 >= 3)
                    {
                        feed.IsActive = false;
                        await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false);
                        _logger.Warning("Feed {FeedId} auto-disabled after {FailureCount} failures", feedId, feed.FailureCount + 1);
                    }

                    return false;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.Error(ex, "Timeout refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, "Timeout: Server took too long to respond", cancellationToken).ConfigureAwait(false);
                    return false;
                }
                catch (SocketException ex)
                {
                    _logger.Error(ex, "Socket error refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, $"Network error: {ex.SocketErrorCode}", cancellationToken).ConfigureAwait(false);
                    return false;
                }
                catch (OperationCanceledException ex)
                {
                    _logger.Debug(ex, "RefreshFeedAsync operation was cancelled for feed ID: {FeedId}", feedId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Parse error refreshing feed {FeedId}: {Url}", feedId, feed.Url);
                    await _feedRepository.IncrementFailureCountAsync(feedId, $"Parse error: {ex.Message}", cancellationToken).ConfigureAwait(false);
                    return false;
                }

                var addedCount = 0;
                foreach (var article in newArticles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var exists = await _articleRepository.ExistsByGuidAsync(article.Guid, cancellationToken).ConfigureAwait(false);
                    if (!exists)
                    {
                        await _articleRepository.InsertAsync(article, cancellationToken).ConfigureAwait(false);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    await UpdateFeedCountsAsync(feedId, cancellationToken).ConfigureAwait(false);
                    _logger.Information("Added {Count} new articles to feed {FeedId}", addedCount, feedId);
                }
                else
                {
                    _logger.Debug("No new articles found for feed {FeedId}", feedId);
                }

                feed.LastUpdated = DateTime.UtcNow;
                feed.NextUpdateSchedule = CalculateNextUpdate(feed.UpdateFrequency);
                await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false);

                _logger.Information("Feed {FeedId} refreshed successfully", feedId);
                return true;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "RefreshFeedAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error refreshing feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to refresh feed with ID {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> RefreshAllFeedsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Refreshing all feeds");

                var feedsToUpdate = await _feedRepository.GetFeedsToUpdateAsync(cancellationToken).ConfigureAwait(false);
                var updatedCount = 0;

                _logger.Information("Found {Count} feeds ready for update", feedsToUpdate.Count);

                foreach (var feed in feedsToUpdate)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await RefreshFeedAsync(feed.Id, cancellationToken).ConfigureAwait(false))
                        updatedCount++;
                }

                _logger.Information("Successfully refreshed {UpdatedCount} out of {TotalCount} feeds",
                    updatedCount, feedsToUpdate.Count);
                return updatedCount;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "RefreshAllFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error refreshing all feeds");
                throw new InvalidOperationException("Failed to refresh all feeds", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> RefreshFeedsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided for refresh: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Debug("Refreshing feeds for category {CategoryId}", categoryId);

                var feeds = await _feedRepository.GetByCategoryAsync(categoryId, true, cancellationToken).ConfigureAwait(false);
                var updatedCount = 0;

                foreach (var feed in feeds.Where(f => f.IsActive))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (await RefreshFeedAsync(feed.Id, cancellationToken).ConfigureAwait(false))
                        updatedCount++;
                }

                _logger.Information("Refreshed {UpdatedCount} feeds for category {CategoryId}", updatedCount, categoryId);
                return updatedCount;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "RefreshFeedsByCategoryAsync operation was cancelled for category ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error refreshing feeds for category {CategoryId}", categoryId);
                throw new InvalidOperationException($"Failed to refresh feeds for category {categoryId}", ex);
            }
        }

        #endregion

        #region Feed Management

        /// <inheritdoc />
        public async Task<bool> SetFeedActiveStatusAsync(int feedId, bool isActive, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for status update: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Setting active status for feed {FeedId} to {IsActive}", feedId, isActive);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for active status update", feedId);
                    return false;
                }

                feed.IsActive = isActive;
                var result = await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false) > 0;

                if (result)
                {
                    _logger.Information("Set active status for feed {FeedId} to {IsActive}", feedId, isActive);
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "SetFeedActiveStatusAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error setting active status for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to set active status for feed {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateFeedCategoryAsync(int feedId, int? categoryId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for category update: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Updating category for feed {FeedId} to {CategoryId}", feedId, categoryId);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for category update", feedId);
                    return false;
                }

                feed.CategoryId = categoryId;
                var result = await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false) > 0;

                if (result)
                {
                    _logger.Information("Updated category for feed {FeedId} to {CategoryId}", feedId, categoryId);
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateFeedCategoryAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating category for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to update category for feed {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateFeedRetentionAsync(int feedId, int? retentionDays, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for retention update: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                if (retentionDays.HasValue && retentionDays.Value < 1)
                {
                    _logger.Warning("Invalid retention days value: {RetentionDays}", retentionDays);
                    throw new ArgumentException("Retention days must be at least 1", nameof(retentionDays));
                }

                _logger.Debug("Updating retention days for feed {FeedId} to {RetentionDays}", feedId, retentionDays);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for retention update", feedId);
                    return false;
                }

                feed.ArticleRetentionDays = retentionDays;
                var result = await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false) > 0;

                if (result)
                {
                    _logger.Information("Updated retention days for feed {FeedId} to {RetentionDays}", feedId, retentionDays);
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateFeedRetentionAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating retention days for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to update retention days for feed {feedId}", ex);
            }
        }

        #endregion

        #region Health and Monitoring

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetUnreadCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving unread counts by feed");
                var counts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);
                _logger.Debug("Retrieved unread counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetUnreadCountsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving unread counts by feed");
                throw new InvalidOperationException("Failed to retrieve unread counts", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<int, int>> GetArticleCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving article counts by feed");
                var feeds = await _feedRepository.GetAllAsync(true, cancellationToken).ConfigureAwait(false);
                var counts = feeds.ToDictionary(f => f.Id, f => f.TotalArticleCount);
                _logger.Debug("Retrieved article counts for {Count} feeds", counts.Count);
                return counts;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetArticleCountsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving article counts by feed");
                throw new InvalidOperationException("Failed to retrieve article counts", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<FeedHealthDto>> GetFailedFeedsAsync(int maxFailureCount = 3, CancellationToken cancellationToken = default)
        {
            try
            {
                if (maxFailureCount < 1)
                {
                    _logger.Warning("Invalid max failure count: {MaxFailureCount}", maxFailureCount);
                    throw new ArgumentException("Max failure count must be at least 1", nameof(maxFailureCount));
                }

                _logger.Debug("Retrieving failed feeds with threshold {MaxFailureCount}", maxFailureCount);

                var feeds = await _feedRepository.GetFailedFeedsAsync(maxFailureCount, cancellationToken).ConfigureAwait(false);
                var healthDtos = _mapper.Map<List<FeedHealthDto>>(feeds);

                _logger.Information("Found {Count} failed feeds", healthDtos.Count);
                return healthDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetFailedFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving failed feeds with threshold {MaxFailureCount}", maxFailureCount);
                throw new InvalidOperationException($"Failed to retrieve failed feeds with threshold {maxFailureCount}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<FeedSummaryDto>> GetHealthyFeedsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving healthy feeds");

                var feeds = await _feedRepository.GetHealthyFeedsAsync(cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);

                var feedDtos = _mapper.Map<List<FeedSummaryDto>>(feeds);
                foreach (var dto in feedDtos)
                {
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Information("Found {Count} healthy feeds", feedDtos.Count);
                return feedDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetHealthyFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving healthy feeds");
                throw new InvalidOperationException("Failed to retrieve healthy feeds", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ResetFeedFailuresAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for failure reset: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Resetting failure count for feed {FeedId}", feedId);
                var result = await _feedRepository.ResetFailureCountAsync(feedId, cancellationToken).ConfigureAwait(false) > 0;

                if (result)
                {
                    _logger.Information("Reset failure count for feed {FeedId}", feedId);
                }
                else
                {
                    _logger.Warning("Feed {FeedId} not found for failure reset", feedId);
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "ResetFeedFailuresAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error resetting failure count for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to reset failure count for feed {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<FeedHealthStatus, int>> GetFeedHealthStatsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving feed health statistics");

                var feeds = await _feedRepository.GetAllAsync(true, cancellationToken).ConfigureAwait(false);
                var stats = new Dictionary<FeedHealthStatus, int>
                {
                    { FeedHealthStatus.Healthy, 0 },
                    { FeedHealthStatus.Warning, 0 },
                    { FeedHealthStatus.Error, 0 }
                };

                foreach (var feed in feeds)
                {
                    stats[feed.HealthStatus]++;
                }

                _logger.Information("Feed health stats: Healthy={Healthy}, Warning={Warning}, Error={Error}",
                    stats[FeedHealthStatus.Healthy], stats[FeedHealthStatus.Warning], stats[FeedHealthStatus.Error]);
                return stats;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetFeedHealthStatsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feed health statistics");
                throw new InvalidOperationException("Failed to retrieve feed health statistics", ex);
            }
        }

        #endregion

        #region Search and Filtering

        /// <inheritdoc />
        public async Task<List<FeedSummaryDto>> SearchFeedsAsync(string searchText, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _logger.Debug("Empty search text provided");
                    return new List<FeedSummaryDto>();
                }

                _logger.Debug("Searching feeds for: {SearchText} (IncludeInactive: {IncludeInactive})", searchText, includeInactive);

                var feeds = await _feedRepository.SearchAsync(searchText, includeInactive, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);

                var feedDtos = _mapper.Map<List<FeedSummaryDto>>(feeds);
                foreach (var dto in feedDtos)
                {
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Information("Found {Count} feeds for search: {SearchText}", feedDtos.Count, searchText);
                return feedDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "SearchFeedsAsync operation was cancelled for: {SearchText}", searchText);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching feeds for: {SearchText}", searchText);
                throw new InvalidOperationException($"Failed to search feeds for: {searchText}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<FeedSummaryDto>> GetFeedsByCategoryAsync(int categoryId, bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                if (categoryId <= 0)
                {
                    _logger.Warning("Invalid category ID provided: {CategoryId}", categoryId);
                    throw new ArgumentOutOfRangeException(nameof(categoryId), "Category ID must be greater than 0");
                }

                _logger.Debug("Retrieving feeds for category {CategoryId} (IncludeInactive: {IncludeInactive})", categoryId, includeInactive);

                var feeds = await _feedRepository.GetByCategoryAsync(categoryId, includeInactive, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);

                var feedDtos = _mapper.Map<List<FeedSummaryDto>>(feeds);
                foreach (var dto in feedDtos)
                {
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Debug("Found {Count} feeds for category {CategoryId}", feedDtos.Count, categoryId);
                return feedDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetFeedsByCategoryAsync operation was cancelled for category ID: {CategoryId}", categoryId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving feeds for category {CategoryId}", categoryId);
                throw new InvalidOperationException($"Failed to retrieve feeds for category {categoryId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<FeedSummaryDto>> GetUncategorizedFeedsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving uncategorized feeds (IncludeInactive: {IncludeInactive})", includeInactive);

                var feeds = await _feedRepository.GetUncategorizedAsync(includeInactive, cancellationToken).ConfigureAwait(false);
                var unreadCounts = await _articleRepository.GetUnreadCountsByFeedAsync(cancellationToken).ConfigureAwait(false);

                var feedDtos = _mapper.Map<List<FeedSummaryDto>>(feeds);
                foreach (var dto in feedDtos)
                {
                    dto.UnreadCount = unreadCounts.GetValueOrDefault(dto.Id, 0);
                }

                _logger.Debug("Found {Count} uncategorized feeds", feedDtos.Count);
                return feedDtos;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetUncategorizedFeedsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving uncategorized feeds");
                throw new InvalidOperationException("Failed to retrieve uncategorized feeds", ex);
            }
        }

        #endregion

        #region Feed Properties

        /// <inheritdoc />
        public async Task<int> GetTotalArticleCountAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Retrieving total article count for feed {FeedId}", feedId);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                var count = feed?.TotalArticleCount ?? 0;

                _logger.Debug("Total articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetTotalArticleCountAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving total article count for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to retrieve total article count for feed {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> GetUnreadCountByFeedAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Retrieving unread count for feed {FeedId}", feedId);
                var count = await _articleRepository.GetUnreadCountByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Unread articles for feed {FeedId}: {Count}", feedId, count);
                return count;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "GetUnreadCountByFeedAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error retrieving unread count for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to retrieve unread count for feed {feedId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateFeedPropertiesAsync(int feedId, string? title = null, string? description = null, string? websiteUrl = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (feedId <= 0)
                {
                    _logger.Warning("Invalid feed ID provided for property update: {FeedId}", feedId);
                    throw new ArgumentOutOfRangeException(nameof(feedId), "Feed ID must be greater than 0");
                }

                _logger.Debug("Updating properties for feed {FeedId}", feedId);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed == null)
                {
                    _logger.Warning("Feed {FeedId} not found for property update", feedId);
                    return false;
                }

                if (title != null) feed.Title = title;
                if (description != null) feed.Description = description;
                if (websiteUrl != null) feed.WebsiteUrl = websiteUrl;

                var result = await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false) > 0;

                if (result)
                {
                    _logger.Information("Updated properties for feed {FeedId}", feedId);
                }

                return result;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateFeedPropertiesAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating properties for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to update properties for feed {feedId}", ex);
            }
        }

        #endregion

        #region Maintenance

        /// <inheritdoc />
        public async Task<int> CleanupOldArticlesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Cleaning up old articles based on retention settings");

                var feeds = await _feedRepository.GetFeedsWithRetentionAsync(cancellationToken).ConfigureAwait(false);
                var totalDeleted = 0;

                _logger.Information("Found {Count} feeds with retention settings", feeds.Count);

                foreach (var feed in feeds)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (feed.ArticleRetentionDays.HasValue)
                    {
                        var cutoffDate = DateTime.UtcNow.AddDays(-feed.ArticleRetentionDays.Value);
                        var deleted = await _articleRepository.DeleteOlderThanAsync(cutoffDate, cancellationToken).ConfigureAwait(false);
                        totalDeleted += deleted;

                        if (deleted > 0)
                        {
                            _logger.Information("Deleted {DeletedCount} old articles for feed {FeedId} (retention: {RetentionDays} days)",
                                deleted, feed.Id, feed.ArticleRetentionDays.Value);
                        }
                    }
                }

                _logger.Information("Total articles cleaned up: {TotalDeleted}", totalDeleted);
                return totalDeleted;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "CleanupOldArticlesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error cleaning up old articles");
                throw new InvalidOperationException("Failed to clean up old articles", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdateAllFeedCountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Updating article counts for all feeds");

                var feeds = await _feedRepository.GetAllAsync(true, cancellationToken).ConfigureAwait(false);
                var updatedCount = 0;

                foreach (var feed in feeds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UpdateFeedCountsAsync(feed.Id, cancellationToken).ConfigureAwait(false);
                    updatedCount++;
                }

                _logger.Information("Updated article counts for {Count} feeds", updatedCount);
                return updatedCount;
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateAllFeedCountsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating article counts for all feeds");
                throw new InvalidOperationException("Failed to update article counts for all feeds", ex);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Updates the article counts for a specific feed.
        /// </summary>
        private async Task UpdateFeedCountsAsync(int feedId, CancellationToken cancellationToken = default)
        {
            try
            {
                var totalCount = await _articleRepository.CountWhereAsync(a => a.FeedId == feedId, cancellationToken).ConfigureAwait(false);
                var unreadCount = await _articleRepository.GetUnreadCountByFeedAsync(feedId, cancellationToken).ConfigureAwait(false);

                var feed = await _feedRepository.GetByIdAsync(feedId, cancellationToken).ConfigureAwait(false);
                if (feed != null)
                {
                    feed.TotalArticleCount = totalCount;
                    feed.UnreadCount = unreadCount;
                    await _feedRepository.UpdateAsync(feed, cancellationToken).ConfigureAwait(false);

                    _logger.Debug("Updated counts for feed {FeedId}: Total={TotalCount}, Unread={UnreadCount}",
                        feedId, totalCount, unreadCount);
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.Debug(ex, "UpdateFeedCountsAsync operation was cancelled for feed ID: {FeedId}", feedId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating counts for feed {FeedId}", feedId);
                throw new InvalidOperationException($"Failed to update counts for feed {feedId}", ex);
            }
        }

        /// <summary>
        /// Normalizes a URL by ensuring it has a scheme and removing trailing slashes.
        /// </summary>
        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim();

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            return url.TrimEnd('/');
        }

        /// <summary>
        /// Calculates the next update time based on update frequency.
        /// </summary>
        private static DateTime CalculateNextUpdate(FeedUpdateFrequency frequency)
        {
            return frequency switch
            {
                FeedUpdateFrequency.Manual => DateTime.MaxValue,
                FeedUpdateFrequency.Every15Minutes => DateTime.UtcNow.AddMinutes(15),
                FeedUpdateFrequency.Every30Minutes => DateTime.UtcNow.AddMinutes(30),
                FeedUpdateFrequency.EveryHour => DateTime.UtcNow.AddHours(1),
                FeedUpdateFrequency.Every3Hours => DateTime.UtcNow.AddHours(3),
                FeedUpdateFrequency.Every6Hours => DateTime.UtcNow.AddHours(6),
                FeedUpdateFrequency.Every12Hours => DateTime.UtcNow.AddHours(12),
                FeedUpdateFrequency.Daily => DateTime.UtcNow.AddDays(1),
                _ => DateTime.UtcNow.AddHours(1)
            };
        }

        #endregion
    }
}