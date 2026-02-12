using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Collections.Concurrent;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Service implementation for tag management.
    /// Provides business logic, validation, and caching for tag operations.
    /// </summary>
    public class TagService : ITagService
    {
        private readonly ITagRepository _tagRepository;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, Tag> _tagCache;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private bool _isCacheInitialized = false;

        public event EventHandler<TagChangedEventArgs>? OnTagChanged;

        public TagService(ITagRepository tagRepository, ILogger logger)
        {
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _tagCache = new ConcurrentDictionary<int, Tag>();
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<TagService>();
        }

        private async Task EnsureCacheInitializedAsync()
        {
            if (_isCacheInitialized) return;

            await _cacheLock.WaitAsync();
            try
            {
                if (_isCacheInitialized) return;

                var allTags = await _tagRepository.GetAllAsync();
                foreach (var tag in allTags)
                {
                    _tagCache[tag.Id] = tag;
                }
                _isCacheInitialized = true;
                _logger.Debug("Tag cache initialized with {Count} items.", _tagCache.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize tag cache.");
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private void ClearCache()
        {
            _tagCache.Clear();
            _isCacheInitialized = false;
        }

        public async Task<Tag> GetTagAsync(int id)
        {
            await EnsureCacheInitializedAsync();

            if (_tagCache.TryGetValue(id, out var cachedTag))
                return cachedTag;

            var tag = await _tagRepository.GetByIdAsync(id);
            if (tag != null)
                _tagCache[id] = tag;

            return tag ?? throw new KeyNotFoundException($"Tag with ID {id} not found.");
        }

        public async Task<Tag?> GetTagByNameAsync(string name)
        {
            return await _tagRepository.GetByNameAsync(name);
        }

        public async Task<List<Tag>> GetAllTagsAsync()
        {
            await EnsureCacheInitializedAsync();
            return _tagCache.Values.OrderBy(t => t.Name).ToList();
        }

        public async Task<int> CreateTagAsync(Tag tag)
        {
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            // Validate required fields
            if (string.IsNullOrWhiteSpace(tag.Name))
                throw new ArgumentException("Tag name cannot be empty.");

            // Check for duplicates
            if (await _tagRepository.ExistsByNameAsync(tag.Name))
                throw new InvalidOperationException($"Tag '{tag.Name}' already exists.");

            // Set default values
            if (string.IsNullOrWhiteSpace(tag.Color))
                tag.Color = "#3498db";

            if (tag.CreatedAt == default)
                tag.CreatedAt = DateTime.UtcNow;

            try
            {
                var id = await _tagRepository.InsertAsync(tag);
                tag.Id = id;

                // Update cache
                _tagCache[id] = tag;

                // Raise event
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(id, tag.Name, TagChangeType.Created));

                _logger.Information("Tag created: {TagName} (ID: {TagId})", tag.Name, id);
                return id;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create tag: {TagName}", tag.Name);
                throw;
            }
        }

        public async Task UpdateTagAsync(Tag tag)
        {
            if (tag == null)
                throw new ArgumentNullException(nameof(tag));

            if (tag.Id <= 0)
                throw new ArgumentException("Invalid tag ID.");

            // Get existing tag to preserve some properties
            var existingTag = await GetTagAsync(tag.Id);
            if (existingTag == null)
                throw new KeyNotFoundException($"Tag with ID {tag.Id} not found.");

            // Prevent duplicate names
            if (!existingTag.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await _tagRepository.ExistsByNameAsync(tag.Name))
                    throw new InvalidOperationException($"Tag '{tag.Name}' already exists.");
            }

            try
            {
                await _tagRepository.UpdateAsync(tag);

                // Update cache
                _tagCache[tag.Id] = tag;

                // Raise event
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(tag.Id, tag.Name, TagChangeType.Updated));

                _logger.Debug("Tag updated: {TagName} (ID: {TagId})", tag.Name, tag.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update tag ID: {TagId}", tag.Id);
                throw;
            }
        }

        public async Task DeleteTagAsync(int id)
        {
            try
            {
                var tag = await GetTagAsync(id);
                await _tagRepository.DeleteAsync(tag);

                // Remove from cache
                _tagCache.TryRemove(id, out _);

                // Raise event
                OnTagChanged?.Invoke(this, new TagChangedEventArgs(id, tag.Name, TagChangeType.Deleted));

                _logger.Information("Tag deleted: {TagName} (ID: {TagId})", tag.Name, id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete tag ID: {TagId}", id);
                throw;
            }
        }

        public async Task<bool> TagExistsAsync(string name)
        {
            return await _tagRepository.ExistsByNameAsync(name);
        }

        public async Task<List<Tag>> GetPopularTagsAsync(int limit = 50)
        {
            return await _tagRepository.GetPopularTagsAsync(limit);
        }

        public async Task<List<Tag>> GetPinnedTagsAsync()
        {
            return await _tagRepository.GetPinnedTagsAsync();
        }

        public async Task<List<Tag>> GetVisibleTagsAsync()
        {
            return await _tagRepository.GetVisibleTagsAsync();
        }

        public async Task<List<Tag>> SearchTagsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllTagsAsync();

            return await _tagRepository.SearchByNameAsync(searchTerm);
        }

        public async Task<List<Tag>> GetTagsByArticleAsync(int articleId)
        {
            return await _tagRepository.GetTagsByArticleIdAsync(articleId);
        }

        public async Task<List<Tag>> GetTagsByColorAsync(string color)
        {
            return await _tagRepository.GetTagsByColorAsync(color);
        }

        public async Task<Tag> GetOrCreateTagAsync(string name, string? color = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tag name cannot be empty.");

            var existingTag = await _tagRepository.GetByNameAsync(name);
            if (existingTag != null)
                return existingTag;

            var newTag = new Tag
            {
                Name = name.Trim(),
                Color = color ?? "#3498db",
                CreatedAt = DateTime.UtcNow
            };

            var id = await CreateTagAsync(newTag);
            return newTag;
        }

        public async Task UpdateTagUsageAsync(int tagId)
        {
            try
            {
                await _tagRepository.UpdateLastUsedAsync(tagId);
                await _tagRepository.IncrementUsageCountAsync(tagId);

                // Update cache
                if (_tagCache.TryGetValue(tagId, out var tag))
                {
                    tag.LastUsedAt = DateTime.UtcNow;
                    tag.UsageCount++;
                }

                _logger.Debug("Tag usage updated: {TagId}", tagId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update usage for tag ID: {TagId}", tagId);
                throw;
            }
        }

        public async Task TogglePinStatusAsync(int tagId)
        {
            var tag = await GetTagAsync(tagId);
            tag.IsPinned = !tag.IsPinned;

            await UpdateTagAsync(tag);

            var changeType = tag.IsPinned ? TagChangeType.Pinned : TagChangeType.Unpinned;
            OnTagChanged?.Invoke(this, new TagChangedEventArgs(tagId, tag.Name, changeType));
        }

        public async Task ToggleVisibilityAsync(int tagId)
        {
            var tag = await GetTagAsync(tagId);
            tag.IsVisible = !tag.IsVisible;

            await UpdateTagAsync(tag);

            OnTagChanged?.Invoke(this, new TagChangedEventArgs(tagId, tag.Name, TagChangeType.VisibilityChanged));
        }

        public async Task MergeTagsAsync(int sourceTagId, int targetTagId)
        {
            if (sourceTagId == targetTagId)
                throw new ArgumentException("Source and target tags cannot be the same.");

            var sourceTag = await GetTagAsync(sourceTagId);
            var targetTag = await GetTagAsync(targetTagId);

            // Implementation would involve:
            // 1. Update all ArticleTag associations from source to target
            // 2. Update usage count on target tag
            // 3. Delete source tag
            // Note: This requires access to ArticleTag repository

            _logger.Warning("Tag merge not fully implemented. Source: {SourceId}, Target: {TargetId}",
                sourceTagId, targetTagId);
        }

        public async Task<List<Tag>> ImportTagsAsync(List<Tag> tags)
        {
            var importedTags = new List<Tag>();
            var errors = new List<string>();

            foreach (var tag in tags)
            {
                try
                {
                    var existingTag = await _tagRepository.GetByNameAsync(tag.Name);
                    if (existingTag == null)
                    {
                        var id = await CreateTagAsync(tag);
                        tag.Id = id;
                        importedTags.Add(tag);
                    }
                    else
                    {
                        // Update existing tag
                        existingTag.Description = tag.Description;
                        existingTag.Color = tag.Color;
                        existingTag.Icon = tag.Icon;
                        await UpdateTagAsync(existingTag);
                        importedTags.Add(existingTag);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to import tag '{tag.Name}': {ex.Message}");
                    _logger.Error(ex, "Failed to import tag: {TagName}", tag.Name);
                }
            }

            if (errors.Any())
            {
                _logger.Warning("Tag import completed with {ErrorCount} errors", errors.Count);
            }

            return importedTags;
        }

        public async Task<List<Tag>> GetOrCreateTagsAsync(IEnumerable<string> tagNames)
        {
            var tags = new List<Tag>();

            foreach (var tagName in tagNames.Distinct())
            {
                var tag = await GetOrCreateTagAsync(tagName);
                tags.Add(tag);
            }

            return tags;
        }

        public async Task AssignTagsToArticleAsync(int articleId, IEnumerable<int> tagIds)
        {
            // Implementation requires ArticleTag repository/service
            // Would associate tags with article and update usage counts
            _logger.Debug("Assigning {Count} tags to article {ArticleId}",
                tagIds.Count(), articleId);
        }

        public async Task RemoveTagsFromArticleAsync(int articleId, IEnumerable<int> tagIds)
        {
            // Implementation requires ArticleTag repository/service
            // Would remove tag associations from article
            _logger.Debug("Removing {Count} tags from article {ArticleId}",
                tagIds.Count(), articleId);
        }

        public async Task<int> GetTotalTagCountAsync()
        {
            await EnsureCacheInitializedAsync();
            return _tagCache.Count;
        }

        public async Task<Dictionary<string, int>> GetTagUsageStatisticsAsync()
        {
            var popularTags = await GetPopularTagsAsync(20);
            return popularTags.ToDictionary(t => t.Name, t => t.UsageCount);
        }

        public async Task<List<string>> GetSuggestedTagNamesAsync(string partialName)
        {
            if (string.IsNullOrWhiteSpace(partialName))
                return new List<string>();

            var tags = await SearchTagsAsync(partialName);
            return tags.Select(t => t.Name).ToList();
        }
    }
}