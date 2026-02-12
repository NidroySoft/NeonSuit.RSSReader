using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository implementation for Tag entity.
    /// Provides data access operations with Entity Framework Core.
    /// </summary>
    public class TagRepository : BaseRepository<Tag>, ITagRepository
    {
        private readonly ILogger _logger;
        private static readonly Regex _hexColorRegex =
            new Regex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);
        private const string DefaultColor = "#3498db";

        public TagRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<TagRepository>();
        }

        public async Task<Tag?> GetByNameAsync(string name)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t =>
                        EF.Functions.Collate(t.Name, "NOCASE") == name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tag by name: {Name}", name);
                throw;
            }
        }

        public async Task<List<Tag>> GetPopularTagsAsync(int limit = 50)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.IsVisible)
                    .OrderByDescending(t => t.UsageCount)
                    .ThenByDescending(t => t.LastUsedAt)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve popular tags with limit: {Limit}", limit);
                throw;
            }
        }

        public async Task<List<Tag>> GetPinnedTagsAsync()
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.IsPinned && t.IsVisible)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve pinned tags");
                throw;
            }
        }

        public async Task<List<Tag>> GetVisibleTagsAsync()
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.IsVisible)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve visible tags");
                throw;
            }
        }

        public async Task<List<Tag>> SearchByNameAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return await GetAllAsync();

                return await _dbSet
                    .AsNoTracking()
                    .Where(t => EF.Functions.Like(t.Name, $"%{searchTerm}%"))
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to search tags by name: {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task UpdateLastUsedAsync(int tagId)
        {
            try
            {
                var result = await _dbSet
                    .Where(t => t.Id == tagId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(t => t.LastUsedAt, DateTime.UtcNow));

                if (result == 0)
                {
                    _logger.Warning("Tag not found when updating LastUsedAt for ID: {TagId}", tagId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update LastUsedAt for tag ID: {TagId}", tagId);
                throw;
            }
        }

        public async Task IncrementUsageCountAsync(int tagId, int increment = 1)
        {
            try
            {
                var result = await _dbSet
                    .Where(t => t.Id == tagId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(t => t.UsageCount, t => t.UsageCount + increment));

                if (result == 0)
                {
                    _logger.Warning("Tag not found when incrementing usage count for ID: {TagId}", tagId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to increment usage count for tag ID: {TagId}", tagId);
                throw;
            }
        }

        public async Task<List<Tag>> GetTagsByArticleIdAsync(int articleId)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Join(_context.Set<ArticleTag>(),
                        tag => tag.Id,
                        articleTag => articleTag.TagId,
                        (tag, articleTag) => new { Tag = tag, ArticleTag = articleTag })
                    .Where(x => x.ArticleTag.ArticleId == articleId)
                    .Select(x => x.Tag)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags for article ID: {ArticleId}", articleId);
                throw;
            }
        }

        public async Task<List<Tag>> GetTagsByColorAsync(string color)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.Color == color)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags by color: {Color}", color);
                throw;
            }
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .AnyAsync(t =>
                        EF.Functions.Collate(t.Name, "NOCASE") == name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check tag existence by name: {Name}", name);
                throw;
            }
        }

        // En TagRepository.cs - InsertAsync
        public override async Task<int> InsertAsync(Tag entity)
        {
            try
            {
                // ✅ Validar longitud del nombre con ParamName
                if (entity.Name?.Length > 50)
                    throw new ArgumentException($"Tag name cannot exceed 50 characters. Provided: {entity.Name.Length}", nameof(entity.Name));

                // ✅ Validar nombre duplicado
                var exists = await _dbSet
                    .AnyAsync(t => EF.Functions.Collate(t.Name, "NOCASE") == entity.Name);

                if (exists)
                    throw new InvalidOperationException($"Tag with name '{entity.Name}' already exists.");

                ValidateAndSetColor(entity);
                entity.CreatedAt = DateTime.UtcNow;

                await _dbSet.AddAsync(entity);
                return await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert tag: {TagName}", entity.Name);
                throw;
            }
        }

        public override async Task<int> UpdateAsync(Tag entity)
        {
            try
            {
                if (entity.Name?.Length > 50)
                    throw new ArgumentException($"Tag name cannot exceed 50 characters. Provided: {entity.Name.Length}");

                ValidateAndSetColor(entity);
                _context.Entry(entity).State = EntityState.Modified;
                return await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update tag ID: {TagId}", entity.Id);
                throw;
            }
        }

        /// <summary>
        /// Batch update multiple tags' usage count efficiently.
        /// </summary>
        public async Task<int> BatchUpdateUsageCountAsync(Dictionary<int, int> tagUsageIncrements)
        {
            if (!tagUsageIncrements.Any()) return 0;

            try
            {
                var totalUpdated = 0;

                // Process in batches for performance
                foreach (var batch in tagUsageIncrements.Chunk(100))
                {
                    foreach (var (tagId, increment) in batch)
                    {
                        var result = await _dbSet
                            .Where(t => t.Id == tagId)
                            .ExecuteUpdateAsync(setters =>
                                setters.SetProperty(t => t.UsageCount, t => t.UsageCount + increment));

                        totalUpdated += result;
                    }
                }

                _logger.Debug("Batch updated usage count for {Count} tags", totalUpdated);
                return totalUpdated;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to batch update usage counts");
                throw;
            }
        }

        /// <summary>
        /// Get tags with usage count above a threshold.
        /// </summary>
        public async Task<List<Tag>> GetTagsWithMinUsageAsync(int minUsageCount)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.UsageCount >= minUsageCount)
                    .OrderByDescending(t => t.UsageCount)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags with min usage: {MinUsage}", minUsageCount);
                throw;
            }
        }

        /// <summary>
        /// Get tags created within a date range.
        /// </summary>
        public async Task<List<Tag>> GetTagsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve tags by date range: {StartDate} to {EndDate}",
                    startDate, endDate);
                throw;
            }
        }

        /// <summary>
        /// Get recently used tags (within last X days).
        /// </summary>
        public async Task<List<Tag>> GetRecentlyUsedTagsAsync(int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);

                return await _dbSet
                    .AsNoTracking()
                    .Where(t => t.LastUsedAt.HasValue && t.LastUsedAt >= cutoffDate)
                    .OrderByDescending(t => t.LastUsedAt)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recently used tags within {Days} days", days);
                throw;
            }
        }

        /// <summary>
        /// Toggle pin status for a tag.
        /// </summary>
        public async Task<bool> TogglePinStatusAsync(int tagId)
        {
            try
            {
                var result = await _dbSet
                    .Where(t => t.Id == tagId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(t => t.IsPinned, t => !t.IsPinned));

                if (result > 0)
                {
                    var tagName = await _dbSet
                        .Where(t => t.Id == tagId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync();

                    var isPinned = await _dbSet
                        .Where(t => t.Id == tagId)
                        .Select(t => t.IsPinned)
                        .FirstOrDefaultAsync();

                    _logger.Information("Tag '{TagName}' (ID: {TagId}) is now {Status}",
                        tagName, tagId, isPinned ? "pinned" : "unpinned");
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle pin status for tag ID: {TagId}", tagId);
                throw;
            }
        }

        /// <summary>
        /// Toggle visibility status for a tag.
        /// </summary>
        public async Task<bool> ToggleVisibilityAsync(int tagId)
        {
            try
            {
                var result = await _dbSet
                    .Where(t => t.Id == tagId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(t => t.IsVisible, t => !t.IsVisible));

                if (result > 0)
                {
                    var tagName = await _dbSet
                        .Where(t => t.Id == tagId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync();

                    var isVisible = await _dbSet
                        .Where(t => t.Id == tagId)
                        .Select(t => t.IsVisible)
                        .FirstOrDefaultAsync();

                    _logger.Information("Tag '{TagName}' (ID: {TagId}) is now {Status}",
                        tagName, tagId, isVisible ? "visible" : "hidden");
                }

                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle visibility for tag ID: {TagId}", tagId);
                throw;
            }
        }

        private void ValidateAndSetColor(Tag tag)
        {
            if (!IsValidHexColor(tag.Color))
            {
                _logger.Warning("Invalid color format for tag: {Color}. Using default.", tag.Color);
                tag.Color = DefaultColor;
            }
        }

        private static bool IsValidHexColor(string color)
        {
            return !string.IsNullOrEmpty(color) &&
                   _hexColorRegex.IsMatch(color);
        }
    }
}