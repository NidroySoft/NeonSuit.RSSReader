using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for managing RSS feed categories.
/// Provides hierarchical organization, ordering, and optimized queries for category operations.
/// </summary>
internal class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryRepository"/> class.
    /// </summary>
    /// <param name="context">The EF Core database context.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public CategoryRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("CategoryRepository initialized");
#endif
    }

    #endregion

    #region Basic Retrieval & Existence Checks

    /// <inheritdoc />
    public override async Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.Feeds)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByIdAsync cancelled for category ID {CategoryId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve category by ID {CategoryId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.Feeds)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllAsync cancelled for categories");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve all categories");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<Category>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .Include(c => c.Feeds)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllOrderedAsync cancelled for categories");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve ordered categories");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == name, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByNameAsync cancelled for category '{CategoryName}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve category by name '{CategoryName}'", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbSet
                .AsNoTracking()
                .AnyAsync(c => c.Name == name, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ExistsByNameAsync cancelled for category '{CategoryName}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check existence of category '{CategoryName}'", name);
            throw;
        }
    }

    #endregion

    #region Change Tracking Management

    /// <inheritdoc />
    public void ClearChangeTracker()
    {
        try
        {
            _context.ChangeTracker.Clear();
            _logger.Debug("EF Core change tracker cleared");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to clear EF Core change tracker");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DetachEntityAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var category = await _dbSet
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (category != null)
            {
                _context.Entry(category).State = EntityState.Detached;
                _logger.Debug("Detached category ID {CategoryId} from change tracker", id);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DetachEntityAsync cancelled for category ID {CategoryId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to detach category ID {CategoryId}", id);
            throw;
        }
    }

    #endregion

    #region CRUD Operations

    /// <inheritdoc />
    public override async Task<int> InsertAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            var result = await base.InsertAsync(category, cancellationToken).ConfigureAwait(false);
            _logger.Information("Inserted new category '{CategoryName}' (ID: {CategoryId})",
                category.Name, category.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("InsertAsync cancelled for category '{CategoryName}'", category.Name);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error inserting category '{CategoryName}'", category.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to insert category '{CategoryName}'", category.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task<int> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);

        try
        {
            var result = await base.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
            _logger.Information("Updated category '{CategoryName}' (ID: {CategoryId})",
                category.Name, category.Id);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("UpdateAsync cancelled for category '{CategoryName}'", category.Name);
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Concurrency conflict updating category '{CategoryName}'", category.Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update category '{CategoryName}'", category.Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var category = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (category == null)
            {
                _logger.Warning("Attempted to delete non-existent category ID {CategoryId}", id);
                return 0;
            }

            var result = await base.DeleteAsync(category, cancellationToken).ConfigureAwait(false);
            _logger.Information("Deleted category '{CategoryName}' (ID: {CategoryId})",
                category.Name, id);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("DeleteAsync cancelled for category ID {CategoryId}", id);
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.Error(ex, "Concurrency conflict deleting category ID {CategoryId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete category ID {CategoryId}", id);
            throw;
        }
    }

    #endregion

    #region Aggregates & Statistics

    /// <inheritdoc />
    public async Task<Dictionary<int, int>> GetUnreadCountsByCategoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = from a in _context.Articles
                        where a.Status == ArticleStatus.Unread
                           && a.Feed != null
                           && a.Feed.CategoryId.HasValue
                        group a by a.Feed!.CategoryId!.Value into g
                        select new { CategoryId = g.Key, UnreadCount = g.Count() };

            var results = await query
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var dict = results.ToDictionary(x => x.CategoryId, x => x.UnreadCount);

            _logger.Debug("Retrieved unread counts for {Count} categories", dict.Count);
            return dict;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetUnreadCountsByCategoryAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to compute unread counts by category");
            throw;
        }
    }

    #endregion

    #region Ordering & Sorting

    /// <inheritdoc />
    public async Task<int> ReorderAsync(List<int> categoryIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);

        if (!categoryIds.Any())
        {
            _logger.Warning("Reorder attempted with empty category ID list");
            return 0;
        }

        try
        {
            var categories = await _dbSet
                .Where(c => categoryIds.Contains(c.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!categories.Any())
            {
                _logger.Debug("No categories found for reordering");
                return 0;
            }

            var dict = categories.ToDictionary(c => c.Id);
            int updated = 0;

            for (int i = 0; i < categoryIds.Count; i++)
            {
                if (dict.TryGetValue(categoryIds[i], out var cat) && cat.SortOrder != i)
                {
                    cat.SortOrder = i;
                    updated++;
                }
            }

            if (updated == 0)
            {
                _logger.Information("No sort order changes required");
                return 0;
            }

            var rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Reordered {Rows} categories ({Updated} had changes)", rows, updated);
            return rows;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ReorderAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reorder categories");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var max = await _dbSet
                .AsNoTracking()
                .MaxAsync(c => (int?)c.SortOrder, cancellationToken)
                .ConfigureAwait(false) ?? 0;

            var next = max + 1;
            _logger.Debug("Next sort order calculated: {NextOrder}", next);
            return next;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetNextSortOrderAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to calculate next sort order");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Category> CreateWithOrderAsync(
        string name,
        string? color = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            if (await ExistsByNameAsync(name, cancellationToken).ConfigureAwait(false))
            {
                _logger.Warning("Duplicate category name attempted: '{Name}'", name);
                throw new InvalidOperationException($"Category '{name}' already exists.");
            }

            var nextOrder = await GetNextSortOrderAsync(cancellationToken).ConfigureAwait(false);
            var category = new Category
            {
                Name = name,
                Description = description,
                SortOrder = nextOrder,
                CreatedAt = DateTime.UtcNow
            };

            await InsertAsync(category, cancellationToken).ConfigureAwait(false);

            _logger.Information("Created category '{Name}' (ID: {Id}, SortOrder: {Order})",
                category.Name, category.Id, category.SortOrder);

            return category;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("CreateWithOrderAsync cancelled for category '{Name}'", name);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create ordered category '{Name}'", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetMaxSortOrderAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var max = await _dbSet
                .AsNoTracking()
                .MaxAsync(c => (int?)c.SortOrder, cancellationToken)
                .ConfigureAwait(false) ?? 0;

            _logger.Debug("Maximum sort order: {MaxOrder}", max);
            return max;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetMaxSortOrderAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve maximum sort order");
            throw;
        }
    }

    #endregion
}