using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Professional repository implementation for managing RSS feed categories.
    /// Provides hierarchical organization, ordering, and optimized queries for category operations.
    /// </summary>
    public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
    {
        private readonly ILogger _logger;
        private readonly RssReaderDbContext _dbContext;

        /// <summary>
        /// Initializes a new instance of the CategoryRepository class.
        /// </summary>
        /// <param name="context">The database context for Entity Framework Core operations.</param>
        public CategoryRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = logger.ForContext<CategoryRepository>();
            _dbContext = context;
        }

        /// <summary>
        /// Retrieves all categories ordered by SortOrder and then by Name.
        /// </summary>
        /// <returns>Ordered list of categories.</returns>
        public async Task<List<Category>> GetAllOrderedAsync()
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                var categories = await _dbSet
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} categories in ordered format", categories.Count);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve ordered categories");
                throw;
            }
        }

        /// <summary>
        /// Finds a category by its exact name.
        /// </summary>
        /// <param name="name">The category name to search for.</param>
        /// <returns>The matching Category or null if not found.</returns>
        public async Task<Category?> GetByNameAsync(string name)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                var category = await _dbSet
                    .Where(c => c.Name == name)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                _logger.Debug("Category lookup by name: {Name} - {Result}",
                    name, category != null ? "Found" : "Not found");
                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve category by name: {Name}", name);
                throw;
            }
        }

        /// <summary>
        /// Checks if a category with the specified name already exists.
        /// </summary>
        /// <param name="name">The category name to check.</param>
        /// <returns>True if a category with this name exists, otherwise false.</returns>
        public async Task<bool> ExistsByNameAsync(string name)
        {
            try
            {
                // CHANGED: Optimized with direct AnyAsync query
                return await _dbSet
                    .Where(c => c.Name == name)
                    .AsNoTracking()
                    .AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check if category exists by name: {Name}", name);
                throw;
            }
        }

        /// <summary>
        /// Inserts a new category into the database.
        /// </summary>
        /// <param name="category">The category to insert.</param>
        /// <returns>The number of rows affected.</returns>
        public override async Task<int> InsertAsync(Category category)
        {
            try
            {
                var result = await base.InsertAsync(category);
                _logger.Information("Created new category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert category: {CategoryName}", category.Name);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        /// <param name="category">The category with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        public override async Task<int> UpdateAsync(Category category)
        {
            try
            {
                // Category model doesn't have LastModified property, so we just update
                var result = await base.UpdateAsync(category);
                _logger.Debug("Updated category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);
                throw;
            }
        }

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        /// <param name="id">The category ID to delete.</param>
        /// <returns>The number of rows affected.</returns>
        public async Task<int> DeleteAsync(int id)
        {
            try
            {
                var category = await GetByIdAsync(id);
                if (category == null)
                {
                    _logger.Warning("Attempted to delete non-existent category: ID {CategoryId}", id);
                    return 0;
                }

                var result = await base.DeleteAsync(category);
                _logger.Information("Deleted category: {CategoryName} (ID: {CategoryId})",
                    category.Name, id);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete category: ID {CategoryId}", id);
                throw;
            }
        }

        /// <summary>
        /// Gets the count of unread articles grouped by CategoryId.
        /// Uses an optimized SQL JOIN to avoid loading data into memory.
        /// </summary>
        /// <returns>Dictionary mapping CategoryId to unread article count.</returns>
        public async Task<Dictionary<int, int>> GetUnreadCountsByCategoryAsync()
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ and navigation properties
                var query = _dbContext.Articles
                    .Where(a => a.Status == ArticleStatus.Unread && a.Feed != null && a.Feed.CategoryId.HasValue)
                    .GroupBy(a => a.Feed!.CategoryId!.Value)
                    .Select(g => new { CategoryId = g.Key, UnreadCount = g.Count() });

                var results = await query.ToListAsync();
                var dictionary = results.ToDictionary(x => x.CategoryId, x => x.UnreadCount);

                _logger.Debug("Retrieved unread counts for {Count} categories", dictionary.Count);
                return dictionary;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve unread counts by category");
                throw;
            }
        }

        /// <summary>
        /// Reorders categories based on a provided list of IDs.
        /// Sets the SortOrder based on the index in the list.
        /// </summary>
        /// <param name="categoryIds">List of category IDs in the desired order.</param>
        /// <returns>The number of categories updated.</returns>
        public async Task<int> ReorderAsync(List<int> categoryIds)
        {
            if (categoryIds == null || !categoryIds.Any())
            {
                _logger.Warning("Attempted to reorder with empty category ID list");
                return 0;
            }

            try
            {
                // CHANGED: Optimized with batch update
                var categories = await _dbSet
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync();

                if (!categories.Any())
                    return 0;

                var categoryDict = categories.ToDictionary(c => c.Id);

                // Update sort orders
                foreach (var categoryId in categoryIds)
                {
                    if (categoryDict.TryGetValue(categoryId, out var category))
                    {
                        category.SortOrder = categoryIds.IndexOf(categoryId);
                    }
                }

                var count = await _dbContext.SaveChangesAsync();
                _logger.Information("Reordered {Count} categories", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reorder categories");
                throw;
            }
        }

        /// <summary>
        /// Calculates the next available SortOrder value.
        /// </summary>
        /// <returns>The next sort order integer.</returns>
        public async Task<int> GetNextSortOrderAsync()
        {
            try
            {
                // CHANGED: Replaced GetAllAsync() with optimized Max query
                var maxOrder = await _dbSet
                    .AsNoTracking()
                    .MaxAsync(c => (int?)c.SortOrder) ?? 0;

                var nextOrder = maxOrder + 1;
                _logger.Debug("Calculated next sort order: {NextSortOrder}", nextOrder);
                return nextOrder;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to calculate next sort order");
                throw;
            }
        }

        /// <summary>
        /// Creates a new category with the next available sort order.
        /// </summary>
        /// <param name="name">Category name.</param>
        /// <param name="color">Optional color in hexadecimal format.</param>
        /// <param name="description">Optional category description.</param>
        /// <returns>The newly created category.</returns>
        public async Task<Category> CreateWithOrderAsync(string name, string? color = null, string? description = null)
        {
            try
            {
                if (await ExistsByNameAsync(name))
                {
                    _logger.Warning("Category already exists: {CategoryName}", name);
                    throw new InvalidOperationException($"Category '{name}' already exists.");
                }

                var sortOrder = await GetNextSortOrderAsync();
                var category = new Category
                {
                    Name = name,
                    Color = color ?? "#0078D4",
                    Description = description,
                    SortOrder = sortOrder,
                    CreatedAt = DateTime.UtcNow
                };

                await InsertAsync(category);
                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create category with order: {CategoryName}", name);
                throw;
            }
        }

        /// <summary>
        /// Retrieves all root categories (categories with no parent).
        /// </summary>
        /// <returns>List of root categories.</returns>
        public async Task<List<Category>> GetRootCategoriesAsync()
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                var rootCategories = await _dbSet
                    .Where(c => c.ParentCategoryId == null)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} root categories", rootCategories.Count);
                return rootCategories;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve root categories");
                throw;
            }
        }

        /// <summary>
        /// Retrieves child categories for a specific parent category.
        /// </summary>
        /// <param name="parentId">The parent category ID.</param>
        /// <returns>List of child categories.</returns>
        public async Task<List<Category>> GetChildCategoriesAsync(int parentId)
        {
            try
            {
                // CHANGED: Replaced SQLite Table<T>() with EF Core DbSet
                var childCategories = await _dbSet
                    .Where(c => c.ParentCategoryId == parentId)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .AsNoTracking()
                    .ToListAsync();

                _logger.Debug("Retrieved {Count} child categories for parent ID: {ParentId}",
                    childCategories.Count, parentId);
                return childCategories;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve child categories for parent ID: {ParentId}", parentId);
                throw;
            }
        }

        /// <summary>
        /// Gets the maximum sort order value.
        /// </summary>
        /// <returns>The maximum sort order value.</returns>
        public async Task<int> GetMaxSortOrderAsync()
        {
            try
            {
                // CHANGED: Replaced raw SQL with EF Core LINQ
                var maxOrder = await _dbSet
                    .AsNoTracking()
                    .MaxAsync(c => (int?)c.SortOrder) ?? 0;

                return maxOrder;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get max sort order");
                return 0;
            }
        }
    }
}