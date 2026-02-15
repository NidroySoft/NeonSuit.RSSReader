using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Defines the contract for category repository operations.
    /// Provides methods for managing RSS feed categories with support for hierarchy and ordering.
    /// </summary>
    public interface ICategoryRepository
    {
        /// <summary>
        /// Retrieves a category by its unique identifier.
        /// </summary>
        /// <param name="id">The category ID.</param>
        /// <returns>The Category object or null if not found.</returns>
        Task<Category?> GetByIdAsync(int id);

        /// <summary>
        /// Clears the Entity Framework ChangeTracker.
        /// Use with caution - only in specific scenarios like bulk imports.
        /// </summary>
        void ClearChangeTracker();

        /// <summary>
        /// Detaches a category entity from the change tracker by ID.
        /// </summary>
        /// <param name="id">The ID of the category to detach.</param>
        Task DetachEntityAsync(int id);

        /// <summary>
        /// Retrieves all categories from the database.
        /// </summary>
        /// <returns>A list of all categories.</returns>
        Task<List<Category>> GetAllAsync();

        /// <summary>
        /// Retrieves all categories ordered by SortOrder and then by Name.
        /// </summary>
        /// <returns>Ordered list of categories.</returns>
        Task<List<Category>> GetAllOrderedAsync();

        /// <summary>
        /// Finds a category by its exact name.
        /// </summary>
        /// <param name="name">The category name to search for.</param>
        /// <returns>The matching Category or null if not found.</returns>
        Task<Category?> GetByNameAsync(string name);

        /// <summary>
        /// Checks if a category with the specified name already exists.
        /// </summary>
        /// <param name="name">The category name to check.</param>
        /// <returns>True if a category with this name exists, otherwise false.</returns>
        Task<bool> ExistsByNameAsync(string name);

        /// <summary>
        /// Inserts a new category into the database.
        /// </summary>
        /// <param name="category">The category to insert.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> InsertAsync(Category category);

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        /// <param name="category">The category with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> UpdateAsync(Category category);

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        /// <param name="id">The category ID to delete.</param>
        /// <returns>The number of rows affected.</returns>
        Task<int> DeleteAsync(int id);

        /// <summary>
        /// Gets the count of unread articles grouped by CategoryId.
        /// Uses an optimized SQL JOIN to avoid loading data into memory.
        /// </summary>
        /// <returns>Dictionary mapping CategoryId to unread article count.</returns>
        Task<Dictionary<int, int>> GetUnreadCountsByCategoryAsync();

        /// <summary>
        /// Reorders categories based on a provided list of IDs.
        /// Sets the SortOrder based on the index in the list.
        /// </summary>
        /// <param name="categoryIds">List of category IDs in the desired order.</param>
        /// <returns>The number of categories updated.</returns>
        Task<int> ReorderAsync(List<int> categoryIds);

        /// <summary>
        /// Calculates the next available SortOrder value.
        /// </summary>
        /// <returns>The next sort order integer.</returns>
        Task<int> GetNextSortOrderAsync();

        /// <summary>
        /// Creates a new category with the next available sort order.
        /// </summary>
        /// <param name="name">Category name.</param>
        /// <param name="color">Optional color in hexadecimal format.</param>
        /// <param name="description">Optional category description.</param>
        /// <returns>The newly created category.</returns>
        Task<Category> CreateWithOrderAsync(string name, string? color = null, string? description = null);

        /// <summary>
        /// Gets the maximum sort order value from all categories.
        /// Used for determining the next available position.
        /// </summary>
        Task<int> GetMaxSortOrderAsync();
    }
}