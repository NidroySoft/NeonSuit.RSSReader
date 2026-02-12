using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional Category Service with comprehensive hierarchical support.
    /// Implements optimized database operations and intelligent caching strategies.
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IFeedRepository _feedRepository;
        private readonly ILogger _logger;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IFeedRepository feedRepository,
            ILogger logger)
        {
            _categoryRepository = categoryRepository;
            _feedRepository = feedRepository;
            _logger = logger.ForContext<CategoryService>();
        }

        /// <summary>
        /// Retrieves all categories with their respective feed and unread counts.
        /// </summary>
        public async Task<List<Category>> GetAllCategoriesAsync()
        {
            try
            {
                _logger.Debug("Retrieving all categories with statistics");

                var categories = await _categoryRepository.GetAllOrderedAsync();

                // Optimization: Get all counts in two quick DB hits instead of per-category queries
                var feedCounts = await GetFeedCountsAsync();
                var unreadCounts = await GetUnreadCountsAsync();

                foreach (var category in categories)
                {
                    category.FeedCount = feedCounts.GetValueOrDefault(category.Id, 0);
                    category.UnreadCount = unreadCounts.GetValueOrDefault(category.Id, 0);
                }

                _logger.Information("Retrieved {Count} categories with statistics", categories.Count);
                return categories;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all categories");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a specific category by ID with populated statistics.
        /// </summary>
        public async Task<Category?> GetCategoryByIdAsync(int categoryId)
        {
            try
            {
                _logger.Debug("Retrieving category by ID: {CategoryId}", categoryId);

                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category != null)
                {
                    // Attach counts for the specific category
                    var feedCounts = await GetFeedCountsAsync();
                    var unreadCounts = await GetUnreadCountsAsync();

                    category.FeedCount = feedCounts.GetValueOrDefault(categoryId, 0);
                    category.UnreadCount = unreadCounts.GetValueOrDefault(categoryId, 0);

                    _logger.Debug("Retrieved category: {CategoryName} (ID: {CategoryId})", category.Name, categoryId);
                }
                else
                {
                    _logger.Debug("Category not found with ID: {CategoryId}", categoryId);
                }

                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve category by ID: {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Creates a new category with automatic sort order assignment.
        /// </summary>
        public async Task<Category> CreateCategoryAsync(string name, string? color = null, string? description = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.Warning("Attempted to create category with empty name");
                    throw new ArgumentException("Category name cannot be empty", nameof(name));
                }

                _logger.Information("Creating new category: {CategoryName}", name);

                var category = await _categoryRepository.CreateWithOrderAsync(name, color, description);

                _logger.Information("Successfully created category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);

                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create category: {CategoryName}", name);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        public async Task<bool> UpdateCategoryAsync(Category category)
        {
            ArgumentNullException.ThrowIfNull(category);
            try
            {
                if (category == null)
                {
                    _logger.Warning("Attempted to update null category");
                    throw new ArgumentNullException(nameof(category));
                }

                _logger.Debug("Updating category: {CategoryName} (ID: {CategoryId})",
                    category.Name, category.Id);

                var result = await _categoryRepository.UpdateAsync(category);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully updated category: {CategoryName} (ID: {CategoryId})",
                        category.Name, category.Id);
                }
                else
                {
                    _logger.Warning("No changes made to category: {CategoryName} (ID: {CategoryId})",
                        category.Name, category.Id);
                }

                return success;
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
        public async Task<bool> DeleteCategoryAsync(int categoryId)
        {
            try
            {
                _logger.Information("Deleting category with ID: {CategoryId}", categoryId);

                // Check if category exists before attempting deletion
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null)
                {
                    _logger.Warning("Category not found for deletion: ID {CategoryId}", categoryId);
                    return false;
                }

                // Note: In a production scenario, you might want to handle feeds in this category
                // For now, we just delete the category (assuming database constraints handle orphaned feeds)
                var result = await _categoryRepository.DeleteAsync(categoryId);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully deleted category: {CategoryName} (ID: {CategoryId})",
                        category.Name, categoryId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete category with ID: {CategoryId}", categoryId);
                throw;
            }
        }

        /// <summary>
        /// Reorders categories based on the provided list of IDs.
        /// </summary>
        public async Task<bool> ReorderCategoriesAsync(List<int> categoryIds)
        {
            try
            {
                if (categoryIds == null || !categoryIds.Any())
                {
                    _logger.Warning("Attempted to reorder with empty category ID list");
                    return false;
                }

                _logger.Debug("Reordering {Count} categories", categoryIds.Count);

                var result = await _categoryRepository.ReorderAsync(categoryIds);
                var success = result > 0;

                if (success)
                {
                    _logger.Information("Successfully reordered {Count} categories", result);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reorder categories");
                throw;
            }
        }

        /// <summary>
        /// Gets feed counts grouped by category ID.
        /// </summary>
        public async Task<Dictionary<int, int>> GetFeedCountsAsync()
        {
            try
            {
                _logger.Debug("Retrieving feed counts by category");
                var counts = await _feedRepository.GetCountByCategoryAsync();
                _logger.Debug("Retrieved feed counts for {Count} categories", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve feed counts by category");
                throw;
            }
        }

        /// <summary>
        /// Gets unread article counts grouped by category ID.
        /// </summary>
        public async Task<Dictionary<int, int>> GetUnreadCountsAsync()
        {
            try
            {
                _logger.Debug("Retrieving unread counts by category");
                var counts = await _categoryRepository.GetUnreadCountsByCategoryAsync();
                _logger.Debug("Retrieved unread counts for {Count} categories", counts.Count);
                return counts;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve unread counts by category");
                throw;
            }
        }

        /// <summary>
        /// Checks if a category with the specified name already exists.
        /// </summary>
        public async Task<bool> CategoryExistsByNameAsync(string name)
        {
            try
            {
                var exists = await _categoryRepository.ExistsByNameAsync(name);
                _logger.Debug("Category existence check for '{Name}': {Exists}", name, exists);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check category existence for name: {Name}", name);
                throw;
            }
        }

        /// <summary>
        /// Builds a complete category tree with hierarchical structure.
        /// </summary>
        public async Task<List<Category>> GetCategoryTreeAsync()
        {
            try
            {
                _logger.Debug("Building complete category tree");

                var allCategories = await GetAllCategoriesAsync();
                var categoryDict = allCategories.ToDictionary(c => c.Id);

                // Build parent-child relationships
                foreach (var category in allCategories)
                {
                    if (category.ParentCategoryId.HasValue &&
                        categoryDict.TryGetValue(category.ParentCategoryId.Value, out var parent))
                    {
                        category.Parent = parent;
                        parent.Children.Add(category);
                    }
                }

                // Return only root categories (tree will be complete through Children property)
                var rootCategories = allCategories.Where(c => c.ParentCategoryId == null)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.Name)
                    .ToList();

                _logger.Information("Built category tree with {TotalCategories} total categories, {RootCount} root categories",
                    allCategories.Count, rootCategories.Count);

                return rootCategories;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to build category tree");
                throw;
            }
        }

        /// <summary>
        /// Helper method to get root categories (categories with no parent).
        /// </summary>
        private async Task<List<Category>> GetRootCategoriesInternalAsync()
        {
            var allCategories = await GetAllCategoriesAsync();
            return allCategories.Where(c => c.ParentCategoryId == null)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }

        /// <summary>
        /// Helper method to get child categories for a specific parent.
        /// </summary>
        private async Task<List<Category>> GetChildCategoriesInternalAsync(int parentId)
        {
            var allCategories = await GetAllCategoriesAsync();
            return allCategories.Where(c => c.ParentCategoryId == parentId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }

        public async Task<List<Category>> GetAllCategoriesWithFeedsAsync()
        {
            try
            {
                // Esto depende de cómo tengas implementado el repositorio
                // Si tu CategoryRepository tiene un método GetAllWithFeedsAsync(), úsalo
                // Si no, aquí está una implementación básica:

                var categories = await _categoryRepository.GetAllAsync();
                var feedsByCategory = await _feedRepository.GetFeedsGroupedByCategoryAsync();

                foreach (var category in categories)
                {
                    if (feedsByCategory.TryGetValue(category.Id, out var feeds))
                    {
                        category.Feeds = feeds;
                    }
                    else
                    {
                        category.Feeds = new List<Feed>();
                    }
                }

                return categories.OrderBy(c => c.SortOrder).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve categories with feeds");
                throw;
            }
        }

        public async Task<Category?> GetCategoryWithFeedsAsync(int categoryId)
        {
            try
            {
                var category = await _categoryRepository.GetByIdAsync(categoryId);
                if (category == null) return null;

                // Obtener los feeds de esta categoría
                var feeds = await _feedRepository.GetFeedsByCategoryAsync(categoryId);
                category.Feeds = feeds;

                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve category with feeds for ID: {CategoryId}", categoryId);
                throw;
            }
        }

        public async Task<Category> GetOrCreateCategoryAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Category name cannot be empty");

            try
            {
                // Buscar categoría existente
                var existing = await _categoryRepository.GetByNameAsync(name);
                if (existing != null)
                    return existing;

                // Crear nueva categoría
                var category = new Category
                {
                    Name = name.Trim(),
                    Color = GetDefaultCategoryColor(),
                    SortOrder = await GetNextSortOrderAsync(),
                    CreatedAt = DateTime.UtcNow
                };

                var id = await _categoryRepository.InsertAsync(category);
                category.Id = id;

                _logger.Information("Created new category: {CategoryName} (ID: {CategoryId})", name, id);
                return category;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get or create category: {CategoryName}", name);
                throw;
            }
        }

        // Métodos auxiliares privados
        private string GetDefaultCategoryColor()
        {
            // Puedes definir colores por defecto o usar una lógica aleatoria
            var colors = new[]
            {
            "#3498db", // Azul
            "#2ecc71", // Verde
            "#e74c3c", // Rojo
            "#f39c12", // Naranja
            "#9b59b6", // Púrpura
            "#1abc9c", // Turquesa
        };

            return colors[new Random().Next(colors.Length)];
        }

        private async Task<int> GetNextSortOrderAsync()
        {
            var maxOrder = await _categoryRepository.GetMaxSortOrderAsync();
            return maxOrder + 1;
        }
    }
}