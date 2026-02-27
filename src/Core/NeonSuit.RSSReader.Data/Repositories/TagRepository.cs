using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="Tag"/> entity.
/// Provides efficient data access operations with Entity Framework Core and SQLite.
/// </summary>
internal class TagRepository : BaseRepository<Tag>, ITagRepository
{
    private static readonly Regex _hexColorRegex =
        new Regex("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);

    private const string DefaultColor = "#3498db";

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="TagRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public TagRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("TagRepository initialized");
#endif
    }

    #endregion

    #region Read Single Operations

    /// <inheritdoc />
    public async Task<Tag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == name, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByNameAsync cancelled for '{Name}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tag by name '{Name}'", name);
            throw;
        }
    }

    #endregion

    #region Read Collection Operations

    /// <inheritdoc />
    public async Task<List<Tag>> GetPopularTagsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.IsVisible)
                .OrderByDescending(t => t.UsageCount)
                .ThenByDescending(t => t.LastUsedAt)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPopularTagsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving popular tags");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetPinnedTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.IsPinned && t.IsVisible)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetPinnedTagsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving pinned tags");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetVisibleTagsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.IsVisible)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetVisibleTagsAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving visible tags");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetVisibleTagsAsync(cancellationToken).ConfigureAwait(false);

            return await _dbSet
                .AsNoTracking()
                .Where(t => EF.Functions.Like(t.Name, $"%{searchTerm}%"))
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SearchByNameAsync cancelled for '{SearchTerm}'", searchTerm);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching tags by '{SearchTerm}'", searchTerm);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetTagsByArticleIdAsync(int articleId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(articleId);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.ArticleTags.Any(at => at.ArticleId == articleId))
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagsByArticleIdAsync cancelled for article {ArticleId}", articleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tags for article {ArticleId}", articleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Tag>> GetTagsByColorAsync(string color, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(color);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .Where(t => t.Color == color)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetTagsByColorAsync cancelled for color '{Color}'", color);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving tags by color '{Color}'", color);
            throw;
        }
    }

    #endregion

    #region Existence Checks

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(t => t.Name == name, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByNameAsync cancelled for '{Name}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error checking tag existence for '{Name}'", name);
            throw;
        }
    }

    #endregion

    #region Insert / Update Overrides

    /// <inheritdoc />
    public override async Task<int> InsertAsync(Tag entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            ValidateTag(entity);

            if (await ExistsByNameAsync(entity.Name, cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException($"Tag with name '{entity.Name}' already exists.");

            var result = await base.InsertAsync(entity, cancellationToken).ConfigureAwait(false);
            _logger.Information("Inserted tag '{TagName}' (ID: {TagId})", entity.Name, entity.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("InsertAsync cancelled for tag '{TagName}'", entity.Name);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error inserting tag '{TagName}'", entity.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error inserting tag '{TagName}'", entity.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> UpdateAsync(Tag entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        try
        {
            ValidateTag(entity);

            var result = await base.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            _logger.Information("Updated tag '{TagName}' (ID: {TagId})", entity.Name, entity.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateAsync cancelled for tag ID {TagId}", entity.Id);
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Concurrency conflict updating tag ID {TagId}", entity.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating tag ID {TagId}", entity.Id);
            throw;
        }
    }

    #endregion

    #region Metadata Updates

    /// <inheritdoc />
    public async Task<int> UpdateUsageMetadataAsync(int tagId, int newUsageCount, DateTime lastUsedAt, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tagId);
        ArgumentOutOfRangeException.ThrowIfNegative(newUsageCount);

        try
        {
            var result = await _dbSet
                .Where(t => t.Id == tagId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.UsageCount, newUsageCount)
                    .SetProperty(t => t.LastUsedAt, lastUsedAt == default ? DateTime.UtcNow : lastUsedAt),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result > 0)
                _logger.Debug("Updated usage metadata for tag ID {TagId}", tagId);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateUsageMetadataAsync cancelled for tag ID {TagId}", tagId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating usage metadata for tag ID {TagId}", tagId);
            throw;
        }
    }

    #endregion

    #region Private Helpers

    private static void ValidateTag(Tag tag)
    {
        if (tag.Name?.Length > 50)
            throw new ArgumentException($"Tag name cannot exceed 50 characters. Provided: {tag.Name.Length}", nameof(tag.Name));

        if (!string.IsNullOrEmpty(tag.Color) && !_hexColorRegex.IsMatch(tag.Color))
            tag.Color = DefaultColor;
    }

    #endregion
}