using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Professional interface for Category management.
    /// Provides comprehensive methods for hierarchical category operations,
    /// feed organization, and performance-optimized statistics.
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>
        /// Retrieves all categories with their feed and unread counts populated.
        /// </summary>
        /// <returns>Complete list of categories with calculated statistics.</returns>
        Task<List<Category>> GetAllCategoriesAsync();

        /// <summary>
        /// Retrieves a specific category by ID with populated feed and unread counts.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category.</param>
        /// <returns>The Category object with statistics or null if not found.</returns>
        Task<Category?> GetCategoryByIdAsync(int categoryId);

        /// <summary>
        /// Creates a new category with automatic sort order assignment.
        /// </summary>
        /// <param name="name">The name of the category.</param>
        /// <param name="color">Optional color in hexadecimal format.</param>
        /// <param name="description">Optional description of the category.</param>
        /// <returns>The newly created category.</returns>
        Task<Category> CreateCategoryAsync(string name, string? color = null, string? description = null);

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        /// <param name="category">The category with updated values.</param>
        /// <returns>True if the update was successful, otherwise false.</returns>
        Task<bool> UpdateCategoryAsync(Category category);

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        /// <param name="categoryId">The ID of the category to delete.</param>
        /// <returns>True if deletion was successful, otherwise false.</returns>
        Task<bool> DeleteCategoryAsync(int categoryId);

        /// <summary>
        /// Reorders categories based on the provided list of IDs.
        /// </summary>
        /// <param name="categoryIds">List of category IDs in the desired order.</param>
        /// <returns>True if reordering was successful, otherwise false.</returns>
        Task<bool> ReorderCategoriesAsync(List<int> categoryIds);

        /// <summary>
        /// Gets feed counts grouped by category ID.
        /// </summary>
        /// <returns>Dictionary mapping CategoryId to feed count.</returns>
        Task<Dictionary<int, int>> GetFeedCountsAsync();

        /// <summary>
        /// Gets unread article counts grouped by category ID.
        /// </summary>
        /// <returns>Dictionary mapping CategoryId to unread article count.</returns>
        Task<Dictionary<int, int>> GetUnreadCountsAsync();

        /// <summary>
        /// Checks if a category with the specified name already exists.
        /// </summary>
        /// <param name="name">The category name to check.</param>
        /// <returns>True if a category with this name exists, otherwise false.</returns>
        Task<bool> CategoryExistsByNameAsync(string name);

        /// <summary>
        /// Builds a complete category tree with hierarchical structure.
        /// </summary>
        /// <returns>Hierarchical tree of categories with populated children.</returns>
        Task<List<Category>> GetCategoryTreeAsync();

        /// <summary>
        /// Retrieves all categories with their associated feeds fully populated.
        /// Optimized for scenarios requiring complete category-feed relationships.
        /// </summary>
        /// <returns>List of categories with populated Feed collections.</returns>
        Task<List<Category>> GetAllCategoriesWithFeedsAsync();

        /// <summary>
        /// Retrieves a specific category with its associated feeds fully populated.
        /// </summary>
        /// <param name="categoryId">The ID of the category to retrieve.</param>
        /// <returns>Category with populated Feed collection or null if not found.</returns>
        Task<Category?> GetCategoryWithFeedsAsync(int categoryId);

        /// <summary>
        /// Gets or creates a category by name. If the category doesn't exist, 
        /// it will be created with default values.
        /// </summary>
        /// <param name="name">The name of the category to find or create.</param>
        /// <returns>The existing or newly created category.</returns>
        Task<Category> GetOrCreateCategoryAsync(string name);
    }
}