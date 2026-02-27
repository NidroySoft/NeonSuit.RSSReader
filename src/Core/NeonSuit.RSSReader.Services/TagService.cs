using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Tags;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Events;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="ITagService"/> providing comprehensive tag management.
    /// Implements business logic, validation, caching, and event notifications for tag operations.
    /// </summary>
    internal class TagService : ITagService
    {
        private readonly ITagRepository _tagRepository;
        private readonly IArticleRepository _articleRepository;
        private readonly IArticleTagRepository _articleTagRepository;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, Tag> _tagCache;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private bool _isCacheInitialized = false;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagService"/> class.
        /// </summary>
        /// <param name="tagRepository">The tag repository for data access.</param>
        /// <param name="articleRepository">The article repository for article operations.</param>
        /// <param name="articleTagRepository">The article-tag repository for tag assignments.</param>
        /// <param name="mapper">AutoMapper instance for DTO transformations.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public TagService(
            ITagRepository tagRepository,
            IArticleRepository articleRepository,
            IArticleTagRepository articleTagRepository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(tagRepository);
            ArgumentNullException.ThrowIfNull(articleRepository);
            ArgumentNullException.ThrowIfNull(articleTagRepository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _tagRepository = tagRepository;
            _articleRepository = articleRepository;
            _articleTagRepository = articleTagRepository;
            _mapper = mapper;
            _logger = logger.ForContext<TagService>();
            _tagCache = new ConcurrentDictionary<int, Tag>();

#if DEBUG
            _logger.Debug("TagService initialized");
#endif
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<TagChangedEventArgs>? OnTagChanged;

        #endregion

        #region Cache Management

        /// <summary>
        /// Ensures the tag cache is populated from the database.
        /// Implements double-check locking pattern for thread safety.
        /// </summary>
        private async Task EnsureCacheInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_isCacheInitialized) return;

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_isCacheInitialized) return;

                var allTags = await _tagRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                foreach (var tag in allTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _tagCache[tag.Id] = tag;
                }
                _isCacheInitialized = true;
                _logger.Debug("Tag cache initialized with {Count} items.", _tagCache.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Cache initialization was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize tag cache.");
                throw new InvalidOperationException("Failed to initialize tag cache", ex);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Clears the cache, forcing reload on next access.
        /// </summary>
        private void ClearCache()
        {
            _tagCache.Clear();
            _isCacheInitialized = false;
            _logger.Debug("Tag cache cleared");
        }

        #endregion

        #region Basic CRUD Operations

        /// <inheritdoc />
        public async Task<TagDto?> GetTagAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                if (id <= 0)
                {
                    _logger.Warning("Invalid tag ID provided: {TagId}", id);
                    throw new ArgumentOutOfRangeException(nameof(id), "Tag ID must be greater than 0");
                }

                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);

                if (_tagCache.TryGetValue(id, out var cachedTag))
                {
                    _logger.Verbose("Cache hit for tag ID: {TagId}", id);
                    return _mapper.Map<TagDto>(cachedTag);
                }

                _logger.Debug("Cache miss for tag ID: {TagId}, loading from repository", id);
                var tag = await _tagRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (tag != null)
                {
                    _tagCache[id] = tag;
                    return _mapper.Map<TagDto>(tag);
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagAsync operation was cancelled for ID: {TagId}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tag by ID: {TagId}", id);
                throw new InvalidOperationException($"Failed to retrieve tag with ID {id}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto?> GetTagByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to get tag with empty name");
                    throw new ArgumentException("Tag name cannot be empty", nameof(name));
                }

                _logger.Debug("Retrieving tag by name: {TagName}", name);
                var tag = await _tagRepository.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
                return tag != null ? _mapper.Map<TagDto>(tag) : null;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagByNameAsync operation was cancelled for name: {TagName}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tag by name: {TagName}", name);
                throw new InvalidOperationException($"Failed to retrieve tag by name '{name}'", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagSummaryDto>> GetAllTagsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);
                var tags = _tagCache.Values.OrderBy(t => t.Name).ToList();
                var tagDtos = _mapper.Map<List<TagSummaryDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    dto.DisplayName = dto.IsPinned ? $"📌 {dto.Name}" : dto.Name;
                }

                _logger.Debug("Retrieved {Count} tags from cache", tags.Count);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all tags");
                throw new InvalidOperationException("Failed to retrieve all tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto> CreateTagAsync(CreateTagDto createDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDto);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Validate required fields
                if (string.IsNullOrWhiteSpace(createDto.Name))
                {
                    _logger.Warning("Attempted to create tag with empty name");
                    throw new ArgumentException("Tag name cannot be empty.", nameof(createDto));
                }

                // Check for duplicates
                var exists = await _tagRepository.ExistsByNameAsync(createDto.Name, cancellationToken).ConfigureAwait(false);
                if (exists)
                {
                    _logger.Warning("Tag with name '{TagName}' already exists", createDto.Name);
                    throw new InvalidOperationException($"Tag '{createDto.Name}' already exists.");
                }

                // Validate color if provided
                if (!string.IsNullOrWhiteSpace(createDto.Color) && !IsValidHexColor(createDto.Color))
                {
                    _logger.Warning("Invalid color format: {Color}", createDto.Color);
                    throw new ArgumentException($"Invalid color format: {createDto.Color}. Use hex format (e.g., #FF5733 or #FF573388).", nameof(createDto));
                }

                var tag = _mapper.Map<Tag>(createDto);

                // Set default values
                if (string.IsNullOrWhiteSpace(tag.Color))
                {
                    tag.Color = GenerateColorFromName(tag.Name);
                }

                if (tag.CreatedAt == default)
                    tag.CreatedAt = DateTime.UtcNow;

                tag.UsageCount = 0;
                tag.LastUsedAt = null;

                var id = await _tagRepository.InsertAsync(tag, cancellationToken).ConfigureAwait(false);
                tag.Id = id;

                // Update cache
                _tagCache[id] = tag;

                var tagDto = _mapper.Map<TagDto>(tag);
                EnrichTagDto(tagDto);

                // Raise event
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(id, tag.Name, TagChangeType.Created));

                _logger.Information("Tag created: {TagName} (ID: {TagId})", tag.Name, id);
                return tagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CreateTagAsync operation was cancelled for tag: {TagName}", createDto.Name);
                throw;
            }
            catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to create tag: {TagName}", createDto.Name);
                throw new InvalidOperationException($"Failed to create tag '{createDto.Name}'", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto?> UpdateTagAsync(int tagId, UpdateTagDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (tagId <= 0)
                {
                    _logger.Warning("Invalid tag ID for update: {TagId}", tagId);
                    throw new ArgumentOutOfRangeException(nameof(tagId), "Tag ID must be greater than 0");
                }

                // Get tracked entity from repository (not cache)
                var existingTag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                if (existingTag == null)
                {
                    _logger.Warning("Tag with ID {TagId} not found for update", tagId);
                    return null;
                }

                // Track original values for event
                var originalName = existingTag.Name;

                // Apply updates
                if (updateDto.Name != null && updateDto.Name != existingTag.Name)
                {
                    // Validate name not empty
                    if (string.IsNullOrWhiteSpace(updateDto.Name))
                    {
                        throw new ArgumentException("Tag name cannot be empty", nameof(updateDto));
                    }

                    // Check uniqueness
                    var exists = await _tagRepository.ExistsByNameAsync(updateDto.Name, cancellationToken).ConfigureAwait(false);
                    if (exists)
                    {
                        _logger.Warning("Another tag with name '{TagName}' already exists", updateDto.Name);
                        throw new InvalidOperationException($"Tag '{updateDto.Name}' already exists.");
                    }
                    existingTag.Name = updateDto.Name;
                }

                if (updateDto.Description != null)
                {
                    existingTag.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description;
                }

                if (updateDto.Color != null)
                {
                    if (!string.IsNullOrWhiteSpace(updateDto.Color) && !IsValidHexColor(updateDto.Color))
                    {
                        _logger.Warning("Invalid color format: {Color}", updateDto.Color);
                        throw new ArgumentException($"Invalid color format: {updateDto.Color}", nameof(updateDto));
                    }
                    existingTag.Color = updateDto.Color;
                }

                if (updateDto.Icon != null)
                {
                    existingTag.Icon = string.IsNullOrWhiteSpace(updateDto.Icon) ? null : updateDto.Icon;
                }

                if (updateDto.IsPinned.HasValue)
                {
                    existingTag.IsPinned = updateDto.IsPinned.Value;
                }

                if (updateDto.IsVisible.HasValue)
                {
                    existingTag.IsVisible = updateDto.IsVisible.Value;
                }

                await _tagRepository.UpdateAsync(existingTag, cancellationToken).ConfigureAwait(false);

                // Update cache with updated entity
                _tagCache[tagId] = existingTag;

                var tagDto = _mapper.Map<TagDto>(existingTag);
                EnrichTagDto(tagDto);

                // Determine change type for event
                var changeType = TagChangeType.Updated;
                if (updateDto.IsPinned.HasValue)
                {
                    changeType = updateDto.IsPinned.Value ? TagChangeType.Pinned : TagChangeType.Unpinned;
                }
                else if (updateDto.IsVisible.HasValue)
                {
                    changeType = TagChangeType.VisibilityChanged;
                }

                OnTagChanged?.Invoke(this, new TagChangedEventArgs(tagId, existingTag.Name, changeType));

                _logger.Information("Tag updated: {TagName} (ID: {TagId})", existingTag.Name, tagId);
                return tagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateTagAsync operation was cancelled for tag ID: {TagId}", tagId);
                throw;
            }
            catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to update tag ID: {TagId}", tagId);
                throw new InvalidOperationException($"Failed to update tag with ID {tagId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTagAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                if (id <= 0)
                {
                    _logger.Warning("Invalid tag ID provided for deletion: {TagId}", id);
                    throw new ArgumentOutOfRangeException(nameof(id), "Tag ID must be greater than 0");
                }

                var tag = await _tagRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (tag == null)
                {
                    _logger.Warning("Tag with ID {TagId} not found for deletion", id);
                    return false;
                }

                // CORREGIDO: Usar GetByTagIdAsync para contar asociaciones
                var associations = await _articleTagRepository.GetByTagIdAsync(id, cancellationToken).ConfigureAwait(false);
                var usageCount = associations.Count;

                if (usageCount > 0)
                {
                    _logger.Warning("Cannot delete tag '{TagName}' (ID: {TagId}) because it is used by {Count} articles",
                        tag.Name, id, usageCount);
                    throw new InvalidOperationException($"Cannot delete tag '{tag.Name}' because it is used by {usageCount} articles. Merge or reassign articles first.");
                }

                await _tagRepository.DeleteAsync(tag, cancellationToken).ConfigureAwait(false);

                // Remove from cache
                _tagCache.TryRemove(id, out _);

                // Raise event
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(id, tag.Name, TagChangeType.Deleted));

                _logger.Information("Tag deleted: {TagName} (ID: {TagId})", tag.Name, id);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteTagAsync operation was cancelled for ID: {TagId}", id);
                throw;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to delete tag ID: {TagId}", id);
                throw new InvalidOperationException($"Failed to delete tag with ID {id}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> TagExistsAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to check tag existence with empty name");
                    throw new ArgumentException("Tag name cannot be empty", nameof(name));
                }

                _logger.Debug("Checking if tag exists: {TagName}", name);
                var exists = await _tagRepository.ExistsByNameAsync(name, cancellationToken).ConfigureAwait(false);
                _logger.Debug("Tag '{TagName}' exists: {Exists}", name, exists);
                return exists;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("TagExistsAsync operation was cancelled for name: {TagName}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check tag existence for name: {TagName}", name);
                throw new InvalidOperationException($"Failed to check tag existence for '{name}'", ex);
            }
        }

        #endregion

        #region Specialized Retrieval

        /// <inheritdoc />
        public async Task<List<TagDto>> GetPopularTagsAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            try
            {
                if (limit < 1)
                {
                    _logger.Warning("Invalid limit for popular tags: {Limit}", limit);
                    throw new ArgumentException("Limit must be at least 1", nameof(limit));
                }

                _logger.Debug("Retrieving top {Limit} popular tags", limit);
                var tags = await _tagRepository.GetPopularTagsAsync(limit, cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Retrieved {Count} popular tags", tags.Count);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetPopularTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve popular tags");
                throw new InvalidOperationException("Failed to retrieve popular tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> GetPinnedTagsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving pinned tags");
                var tags = await _tagRepository.GetPinnedTagsAsync(cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Retrieved {Count} pinned tags", tags.Count);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetPinnedTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve pinned tags");
                throw new InvalidOperationException("Failed to retrieve pinned tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> GetVisibleTagsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving visible tags");
                var tags = await _tagRepository.GetVisibleTagsAsync(cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Retrieved {Count} visible tags", tags.Count);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetVisibleTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve visible tags");
                throw new InvalidOperationException("Failed to retrieve visible tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> SearchTagsAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    _logger.Debug("Empty search term provided, returning all tags");
                    var allTags = await GetAllTagsAsync(cancellationToken).ConfigureAwait(false);
                    return _mapper.Map<List<TagDto>>(allTags);
                }

                _logger.Debug("Searching tags for: {SearchTerm}", searchTerm);
                var tags = await _tagRepository.SearchByNameAsync(searchTerm, cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Found {Count} tags matching '{SearchTerm}'", tags.Count, searchTerm);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SearchTagsAsync operation was cancelled for: {SearchTerm}", searchTerm);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to search tags for: {SearchTerm}", searchTerm);
                throw new InvalidOperationException($"Failed to search tags for '{searchTerm}'", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> GetTagsByArticleAsync(int articleId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID for tag retrieval: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                _logger.Debug("Retrieving tags for article ID: {ArticleId}", articleId);
                var tags = await _tagRepository.GetTagsByArticleIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Found {Count} tags for article {ArticleId}", tags.Count, articleId);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagsByArticleAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags for article ID: {ArticleId}", articleId);
                throw new InvalidOperationException($"Failed to retrieve tags for article {articleId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> GetTagsByColorAsync(string color, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(color))
                {
                    _logger.Warning("Attempted to get tags with empty color");
                    throw new ArgumentException("Color cannot be empty", nameof(color));
                }

                if (!IsValidHexColor(color))
                {
                    _logger.Warning("Invalid color format: {Color}", color);
                    throw new ArgumentException($"Invalid color format: {color}", nameof(color));
                }

                _logger.Debug("Retrieving tags with color: {Color}", color);
                var tags = await _tagRepository.GetTagsByColorAsync(color, cancellationToken).ConfigureAwait(false);
                var tagDtos = _mapper.Map<List<TagDto>>(tags);

                foreach (var dto in tagDtos)
                {
                    EnrichTagDto(dto);
                }

                _logger.Debug("Found {Count} tags with color {Color}", tags.Count, color);
                return tagDtos;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagsByColorAsync operation was cancelled for color: {Color}", color);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags for color: {Color}", color);
                throw new InvalidOperationException($"Failed to retrieve tags for color {color}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagCloudDto>> GetTagCloudAsync(int minWeight = 1, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Generating tag cloud with min weight {MinWeight}", minWeight);

                var popularTags = await _tagRepository.GetPopularTagsAsync(100, cancellationToken).ConfigureAwait(false);
                var tagsWithWeight = popularTags.Where(t => t.UsageCount >= minWeight).ToList();

                if (!tagsWithWeight.Any())
                {
                    return new List<TagCloudDto>();
                }

                var maxWeight = tagsWithWeight.Max(t => t.UsageCount);
                var minWeightFound = tagsWithWeight.Min(t => t.UsageCount);

                var tagCloud = new List<TagCloudDto>();

                foreach (var tag in tagsWithWeight)
                {
                    // Normalize weight between 0.1 and 1.0
                    double normalizedWeight = (tag.UsageCount - minWeightFound) / (double)(maxWeight - minWeightFound);
                    normalizedWeight = 0.1 + (normalizedWeight * 0.9);

                    // Determine size class based on normalized weight
                    string sizeClass = normalizedWeight switch
                    {
                        >= 0.8 => "tag-xxl",
                        >= 0.6 => "tag-xl",
                        >= 0.4 => "tag-lg",
                        >= 0.2 => "tag-md",
                        _ => "tag-sm"
                    };

                    tagCloud.Add(new TagCloudDto
                    {
                        Name = tag.Name,
                        Color = tag.Color,
                        Weight = tag.UsageCount,
                        NormalizedWeight = normalizedWeight,
                        SizeClass = sizeClass
                    });
                }

                _logger.Debug("Generated tag cloud with {Count} tags", tagCloud.Count);
                return tagCloud.OrderByDescending(t => t.Weight).ToList();
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagCloudAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to generate tag cloud");
                throw new InvalidOperationException("Failed to generate tag cloud", ex);
            }
        }

        #endregion

        #region Tag Operations

        /// <inheritdoc />
        public async Task<TagDto> GetOrCreateTagAsync(string name, string? color = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to get or create tag with empty name");
                    throw new ArgumentException("Tag name cannot be empty.", nameof(name));
                }

                _logger.Debug("Getting or creating tag: {TagName}", name);

                var existingTag = await _tagRepository.GetByNameAsync(name, cancellationToken).ConfigureAwait(false);
                if (existingTag != null)
                {
                    _logger.Debug("Found existing tag: {TagName} (ID: {TagId})", existingTag.Name, existingTag.Id);
                    var dto = _mapper.Map<TagDto>(existingTag);
                    EnrichTagDto(dto);
                    return dto;
                }

                var createDto = new CreateTagDto
                {
                    Name = name.Trim(),
                    Color = color,
                    IsVisible = true,
                    IsPinned = false
                };

                return await CreateTagAsync(createDto, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetOrCreateTagAsync operation was cancelled for name: {TagName}", name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get or create tag: {TagName}", name);
                throw new InvalidOperationException($"Failed to get or create tag '{name}'", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto?> UpdateTagUsageAsync(int tagId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (tagId <= 0)
                {
                    _logger.Warning("Invalid tag ID for usage update: {TagId}", tagId);
                    throw new ArgumentOutOfRangeException(nameof(tagId), "Tag ID must be greater than 0");
                }

                _logger.Debug("Updating usage for tag ID: {TagId}", tagId);

                // Get the tag to update LastUsedAt
                var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                if (tag == null)
                {
                    _logger.Warning("Tag with ID {TagId} not found for usage update", tagId);
                    return null;
                }

                // Update usage metadata
                tag.LastUsedAt = DateTime.UtcNow;
                tag.UsageCount++;

                await _tagRepository.UpdateUsageMetadataAsync(tagId, tag.UsageCount, tag.LastUsedAt.Value, cancellationToken).ConfigureAwait(false);

                // Update cache
                if (_tagCache.TryGetValue(tagId, out var cachedTag))
                {
                    cachedTag.LastUsedAt = tag.LastUsedAt;
                    cachedTag.UsageCount = tag.UsageCount;
                }

                var tagDto = _mapper.Map<TagDto>(tag);
                EnrichTagDto(tagDto);

                _logger.Debug("Tag usage updated successfully for ID: {TagId} (Count: {UsageCount})",
                    tagId, tag.UsageCount);

                return tagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateTagUsageAsync operation was cancelled for tag ID: {TagId}", tagId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update usage for tag ID: {TagId}", tagId);
                throw new InvalidOperationException($"Failed to update usage for tag {tagId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto?> TogglePinStatusAsync(int tagId, CancellationToken cancellationToken = default)
        {
            try
            {
                var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                if (tag == null)
                {
                    _logger.Warning("Tag with ID {TagId} not found for pin toggle", tagId);
                    return null;
                }

                tag.IsPinned = !tag.IsPinned;
                await _tagRepository.UpdateAsync(tag, cancellationToken).ConfigureAwait(false);

                // Update cache
                _tagCache[tagId] = tag;

                var tagDto = _mapper.Map<TagDto>(tag);
                EnrichTagDto(tagDto);

                var changeType = tag.IsPinned ? TagChangeType.Pinned : TagChangeType.Unpinned;
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(tagId, tag.Name, changeType));

                _logger.Information("Tag pin status toggled: {TagName} -> {IsPinned}", tag.Name, tag.IsPinned);
                return tagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("TogglePinStatusAsync operation was cancelled for tag ID: {TagId}", tagId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle pin status for tag ID: {TagId}", tagId);
                throw new InvalidOperationException($"Failed to toggle pin status for tag {tagId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto?> ToggleVisibilityAsync(int tagId, CancellationToken cancellationToken = default)
        {
            try
            {
                var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                if (tag == null)
                {
                    _logger.Warning("Tag with ID {TagId} not found for visibility toggle", tagId);
                    return null;
                }

                tag.IsVisible = !tag.IsVisible;
                await _tagRepository.UpdateAsync(tag, cancellationToken).ConfigureAwait(false);

                // Update cache
                _tagCache[tagId] = tag;

                var tagDto = _mapper.Map<TagDto>(tag);
                EnrichTagDto(tagDto);

                OnTagChanged?.Invoke(this, new TagChangedEventArgs(tagId, tag.Name, TagChangeType.VisibilityChanged));

                _logger.Information("Tag visibility toggled: {TagName} -> {IsVisible}", tag.Name, tag.IsVisible);
                return tagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ToggleVisibilityAsync operation was cancelled for tag ID: {TagId}", tagId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle visibility for tag ID: {TagId}", tagId);
                throw new InvalidOperationException($"Failed to toggle visibility for tag {tagId}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<TagDto> MergeTagsAsync(MergeTagsDto mergeDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(mergeDto);

            try
            {
                if (mergeDto.SourceTagId <= 0 || mergeDto.TargetTagId <= 0)
                {
                    _logger.Warning("Invalid tag IDs for merge: Source {SourceId}, Target {TargetId}",
                        mergeDto.SourceTagId, mergeDto.TargetTagId);
                    throw new ArgumentException("Tag IDs must be greater than 0", nameof(mergeDto));
                }

                if (mergeDto.SourceTagId == mergeDto.TargetTagId)
                {
                    _logger.Warning("Attempted to merge tag with itself: {TagId}", mergeDto.SourceTagId);
                    throw new ArgumentException("Source and target tags cannot be the same.");
                }

                var sourceTag = await _tagRepository.GetByIdAsync(mergeDto.SourceTagId, cancellationToken).ConfigureAwait(false);
                var targetTag = await _tagRepository.GetByIdAsync(mergeDto.TargetTagId, cancellationToken).ConfigureAwait(false);

                if (sourceTag == null)
                {
                    _logger.Warning("Source tag {SourceId} not found for merge", mergeDto.SourceTagId);
                    throw new InvalidOperationException($"Source tag with ID {mergeDto.SourceTagId} not found.");
                }

                if (targetTag == null)
                {
                    _logger.Warning("Target tag {TargetId} not found for merge", mergeDto.TargetTagId);
                    throw new InvalidOperationException($"Target tag with ID {mergeDto.TargetTagId} not found.");
                }

                _logger.Information("Merging tag '{SourceName}' (ID: {SourceId}) into '{TargetName}' (ID: {TargetId})",
                    sourceTag.Name, mergeDto.SourceTagId, targetTag.Name, mergeDto.TargetTagId);

                // CORREGIDO: Usar GetByTagIdAsync para obtener asociaciones del tag fuente
                var sourceAssociations = await _articleTagRepository.GetByTagIdAsync(mergeDto.SourceTagId, cancellationToken).ConfigureAwait(false);
                var sourceArticleIds = sourceAssociations.Select(a => a.ArticleId).Distinct().ToList();

                if (sourceArticleIds.Any())
                {
                    // Para cada artículo, asegurar que tenga el tag destino
                    foreach (var articleId in sourceArticleIds)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // CORREGIDO: Usar ExistsAsync para verificar si ya tiene el tag destino
                        var hasTargetTag = await _articleTagRepository.ExistsAsync(articleId, mergeDto.TargetTagId, cancellationToken).ConfigureAwait(false);

                        if (!hasTargetTag)
                        {
                            // CORREGIDO: Usar AssociateTagWithArticleAsync para crear nueva asociación
                            await _articleTagRepository.AssociateTagWithArticleAsync(
                                articleId,
                                mergeDto.TargetTagId,
                                appliedBy: "system",
                                ruleId: null,
                                confidence: null,
                                cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // CORREGIDO: Usar RemoveTagFromAllArticlesAsync para eliminar todas las asociaciones del tag fuente
                    await _articleTagRepository.RemoveTagFromAllArticlesAsync(mergeDto.SourceTagId, cancellationToken).ConfigureAwait(false);
                }

                // Actualizar estadísticas del tag destino
                var totalUsage = sourceTag.UsageCount + targetTag.UsageCount;
                var lastUsed = sourceTag.LastUsedAt > targetTag.LastUsedAt ? sourceTag.LastUsedAt : targetTag.LastUsedAt;

                targetTag.UsageCount = totalUsage;
                targetTag.LastUsedAt = lastUsed;

                await _tagRepository.UpdateAsync(targetTag, cancellationToken).ConfigureAwait(false);

                // Eliminar tag fuente si se solicita
                if (mergeDto.DeleteSource)
                {
                    await _tagRepository.DeleteAsync(sourceTag, cancellationToken).ConfigureAwait(false);
                    _tagCache.TryRemove(mergeDto.SourceTagId, out _);
                }

                // Actualizar cache para tag destino
                _tagCache[mergeDto.TargetTagId] = targetTag;

                var targetTagDto = _mapper.Map<TagDto>(targetTag);
                EnrichTagDto(targetTagDto);

                OnTagChanged?.Invoke(this, new TagChangedEventArgs(targetTag.Id, targetTag.Name, TagChangeType.Updated));
                if (mergeDto.DeleteSource)
                {
                    OnTagChanged?.Invoke(this, new TagChangedEventArgs(sourceTag.Id, sourceTag.Name, TagChangeType.Deleted));
                }

                _logger.Information("Tag merge completed successfully. Target '{TargetName}' now has {UsageCount} uses",
                    targetTag.Name, targetTag.UsageCount);

                return targetTagDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("MergeTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex) when (ex is not ArgumentException and not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to merge tags: Source {SourceId} -> Target {TargetId}",
                    mergeDto.SourceTagId, mergeDto.TargetTagId);
                throw new InvalidOperationException($"Failed to merge tags", ex);
            }
        }

        #endregion

        #region Batch Operations

        /// <inheritdoc />
        public async Task<List<TagDto>> ImportTagsAsync(List<CreateTagDto> importDtos, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(importDtos);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.Information("Importing {Count} tags", importDtos.Count);

                var importedTags = new List<TagDto>();
                var errors = new List<string>();

                foreach (var dto in importDtos)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var existingTag = await _tagRepository.GetByNameAsync(dto.Name, cancellationToken).ConfigureAwait(false);
                        if (existingTag == null)
                        {
                            var newTag = await CreateTagAsync(dto, cancellationToken).ConfigureAwait(false);
                            importedTags.Add(newTag);
                        }
                        else
                        {
                            // Update existing tag
                            var updateDto = new UpdateTagDto
                            {
                                Description = dto.Description,
                                Color = dto.Color,
                                Icon = dto.Icon,
                                IsPinned = dto.IsPinned,
                                IsVisible = dto.IsVisible
                            };
                            var updatedTag = await UpdateTagAsync(existingTag.Id, updateDto, cancellationToken).ConfigureAwait(false);
                            if (updatedTag != null)
                            {
                                importedTags.Add(updatedTag);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Failed to import tag '{dto.Name}': {ex.Message}");
                        _logger.Error(ex, "Failed to import tag: {TagName}", dto.Name);
                    }
                }

                if (errors.Any())
                {
                    _logger.Warning("Tag import completed with {ErrorCount} errors: {Errors}",
                        errors.Count, string.Join("; ", errors));
                }

                _logger.Information("Tag import completed: {ImportedCount} tags processed", importedTags.Count);
                return importedTags;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ImportTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import {Count} tags", importDtos.Count);
                throw new InvalidOperationException("Failed to import tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> GetOrCreateTagsAsync(IEnumerable<string> tagNames, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tagNames);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var distinctNames = tagNames.Where(n => !string.IsNullOrWhiteSpace(n))
                                             .Select(n => n.Trim())
                                             .Distinct(StringComparer.OrdinalIgnoreCase)
                                             .ToList();

                _logger.Debug("Getting or creating {Count} tags", distinctNames.Count);

                var tags = new List<TagDto>();

                foreach (var tagName in distinctNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tag = await GetOrCreateTagAsync(tagName, cancellationToken: cancellationToken).ConfigureAwait(false);
                    tags.Add(tag);
                }

                _logger.Debug("Successfully got/created {Count} tags", tags.Count);
                return tags;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetOrCreateTagsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get or create tags");
                throw new InvalidOperationException("Failed to get or create tags", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<TagDto>> AssignTagsToArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID for tag assignment: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                ArgumentNullException.ThrowIfNull(tagIds);

                var tagIdList = tagIds.ToList();
                _logger.Debug("Assigning {Count} tags to article {ArticleId}", tagIdList.Count, articleId);

                // Verify article exists
                var article = await _articleRepository.GetByIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                if (article == null)
                {
                    _logger.Warning("Article {ArticleId} not found for tag assignment", articleId);
                    throw new InvalidOperationException($"Article with ID {articleId} not found.");
                }

                var assignedTags = new List<TagDto>();
                var tagsToAssign = new List<int>();

                // Primero verificar qué tags no están ya asignados
                foreach (var tagId in tagIdList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Verify tag exists
                    var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                    if (tag == null)
                    {
                        _logger.Warning("Tag {TagId} not found for assignment to article {ArticleId}", tagId, articleId);
                        continue;
                    }

                    // CORREGIDO: Usar ExistsAsync para verificar si ya está asignado
                    var hasTag = await _articleTagRepository.ExistsAsync(articleId, tagId, cancellationToken).ConfigureAwait(false);

                    if (!hasTag)
                    {
                        tagsToAssign.Add(tagId);
                        assignedTags.Add(_mapper.Map<TagDto>(tag));
                    }
                    else
                    {
                        assignedTags.Add(_mapper.Map<TagDto>(tag));
                    }
                }

                // CORREGIDO: Usar AssociateTagsWithArticleAsync para asignación batch
                if (tagsToAssign.Any())
                {
                    var assignedCount = await _articleTagRepository.AssociateTagsWithArticleAsync(
                        articleId,
                        tagsToAssign,
                        appliedBy: "user",
                        ruleId: null,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    // Actualizar estadísticas de uso para cada tag asignado
                    foreach (var tagId in tagsToAssign)
                    {
                        await UpdateTagUsageAsync(tagId, cancellationToken).ConfigureAwait(false);
                    }

                    _logger.Debug("Assigned {Count} new tags to article {ArticleId}", assignedCount, articleId);
                }

                // Enriquecer los DTOs antes de retornar
                foreach (var dto in assignedTags)
                {
                    EnrichTagDto(dto);
                }

                _logger.Information("Returning {Count} tags for article {ArticleId}", assignedTags.Count, articleId);
                return assignedTags;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AssignTagsToArticleAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.Error(ex, "Failed to assign tags to article {ArticleId}", articleId);
                throw new InvalidOperationException($"Failed to assign tags to article {articleId}", ex);
            }
        }
        /// <inheritdoc />
        public async Task<List<TagDto>> RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds, CancellationToken cancellationToken = default)
        {
            try
            {
                if (articleId <= 0)
                {
                    _logger.Warning("Invalid article ID for tag removal: {ArticleId}", articleId);
                    throw new ArgumentOutOfRangeException(nameof(articleId), "Article ID must be greater than 0");
                }

                ArgumentNullException.ThrowIfNull(tagIds);

                var tagIdList = tagIds.ToList();
                _logger.Debug("Removing {Count} tags from article {ArticleId}", tagIdList.Count, articleId);

                // CORREGIDO: Usar RemoveTagsFromArticleAsync para eliminación batch
                if (tagIdList.Any())
                {
                    var removedCount = await _articleTagRepository.RemoveTagsFromArticleAsync(articleId, tagIdList, cancellationToken).ConfigureAwait(false);
                    _logger.Debug("Removed {Count} tags from article {ArticleId}", removedCount, articleId);
                }

                // Obtener tags restantes para el artículo
                var remainingAssociations = await _articleTagRepository.GetByArticleIdAsync(articleId, cancellationToken).ConfigureAwait(false);
                var remainingTagIds = remainingAssociations.Select(a => a.TagId).ToList();

                var remainingTags = new List<TagDto>();
                foreach (var tagId in remainingTagIds)
                {
                    var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken).ConfigureAwait(false);
                    if (tag != null)
                    {
                        var tagDto = _mapper.Map<TagDto>(tag);
                        EnrichTagDto(tagDto);
                        remainingTags.Add(tagDto);
                    }
                }

                _logger.Information("Removed {Count} tags from article {ArticleId}, {Remaining} tags remaining",
                    tagIdList.Count, articleId, remainingTags.Count);

                return remainingTags;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("RemoveTagsFromArticleAsync operation was cancelled for article ID: {ArticleId}", articleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to remove tags from article {ArticleId}", articleId);
                throw new InvalidOperationException($"Failed to remove tags from article {articleId}", ex);
            }
        }
        #endregion

        #region Statistics

        /// <inheritdoc />
        public async Task<int> GetTotalTagCountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);
                var count = _tagCache.Count;
                _logger.Debug("Total tag count: {Count}", count);
                return count;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTotalTagCountAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get total tag count");
                throw new InvalidOperationException("Failed to get total tag count", ex);
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, int>> GetTagUsageStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving tag usage statistics");
                var popularTags = await GetPopularTagsAsync(50, cancellationToken).ConfigureAwait(false);
                var statistics = popularTags.ToDictionary(t => t.Name, t => t.UsageCount);
                _logger.Debug("Retrieved statistics for {Count} tags", statistics.Count);
                return statistics;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetTagUsageStatisticsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tag usage statistics");
                throw new InvalidOperationException("Failed to retrieve tag usage statistics", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> GetSuggestedTagNamesAsync(string partialName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partialName))
                {
                    _logger.Debug("Empty partial name provided for suggestions");
                    return new List<string>();
                }

                _logger.Debug("Getting tag suggestions for: {PartialName}", partialName);
                var tags = await SearchTagsAsync(partialName, cancellationToken).ConfigureAwait(false);
                var suggestions = tags.OrderByDescending(t => t.UsageCount)
                                       .Select(t => t.Name)
                                       .Take(20)
                                       .ToList();

                _logger.Debug("Found {Count} suggestions for '{PartialName}'", suggestions.Count, partialName);
                return suggestions;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetSuggestedTagNamesAsync operation was cancelled for: {PartialName}", partialName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get tag suggestions for: {PartialName}", partialName);
                throw new InvalidOperationException($"Failed to get tag suggestions for '{partialName}'", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Validates if a string is a valid hex color.
        /// </summary>
        private static bool IsValidHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return false;

            // Match #RGB, #RRGGBB, #RGBA, #RRGGBBAA
            var regex = new Regex("^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{4}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$");
            return regex.IsMatch(color);
        }

        /// <summary>
        /// Generates a deterministic color from a tag name.
        /// </summary>
        private static string GenerateColorFromName(string name)
        {
            // Simple hash-based color generation
            int hash = name.GetHashCode();
            var random = new Random(hash);

            // Generate HSL with good saturation and lightness
            double hue = random.NextDouble() * 360;
            double saturation = 0.6 + (random.NextDouble() * 0.3); // 60-90%
            double lightness = 0.5 + (random.NextDouble() * 0.2); // 50-70%

            // Convert HSL to Hex
            return HslToHex(hue, saturation, lightness);
        }

        /// <summary>
        /// Converts HSL color to hex format.
        /// </summary>
        private static string HslToHex(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double hueToRgb(double p, double q, double t)
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
                    if (t < 1.0 / 2.0) return q;
                    if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
                    return p;
                }

                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                double hNorm = h / 360.0;

                r = hueToRgb(p, q, hNorm + 1.0 / 3.0);
                g = hueToRgb(p, q, hNorm);
                b = hueToRgb(p, q, hNorm - 1.0 / 3.0);
            }

            // Convert to 0-255 range and to hex
            int toRgb(double x) => (int)Math.Round(x * 255);

            return $"#{toRgb(r):X2}{toRgb(g):X2}{toRgb(b):X2}";
        }

        /// <summary>
        /// Calculates a darker shade of a color for borders/text.
        /// </summary>
        private static string GetDarkerColor(string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor) || hexColor.Length < 7)
                return "#666666";

            try
            {
                // Parse hex to RGB
                int r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

                // Darken by 20%
                r = (int)(r * 0.8);
                g = (int)(g * 0.8);
                b = (int)(b * 0.8);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return "#666666";
            }
        }

        /// <summary>
        /// Formats time ago string.
        /// </summary>
        private static string FormatTimeAgo(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
                return "Never";

            var timeSpan = DateTime.UtcNow - dateTime.Value;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} days ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} weeks ago";
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)} months ago";

            return $"{(int)(timeSpan.TotalDays / 365)} years ago";
        }

        /// <summary>
        /// Enriches a tag DTO with calculated properties.
        /// </summary>
        private void EnrichTagDto(TagDto dto)
        {
            dto.DarkColor = GetDarkerColor(dto.Color);
            dto.DisplayName = dto.IsPinned ? $"📌 {dto.Name}" : dto.Name;
            dto.LastUsedTimeAgo = FormatTimeAgo(dto.LastUsedAt);
        }

        #endregion
    }
}