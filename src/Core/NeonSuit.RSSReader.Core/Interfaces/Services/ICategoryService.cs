using NeonSuit.RSSReader.Core.DTOs.Categories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for managing category-related business logic and operations.
    /// Acts as an abstraction layer between UI/presentation and data repositories,
    /// coordinating hierarchical category management, feed organization, and optimized statistics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service centralizes all category-related use cases in the RSS reader application,
    /// ensuring consistent business rules, validation, and transaction boundaries.
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// </para>
    /// <para>
    /// Implementations must ensure:
    /// <list type="bullet">
    /// <item>All read operations are efficient and memory-safe (pagination, server-side filtering).</item>
    /// <item>Category operations maintain hierarchical integrity and referential integrity with feeds.</item>
    /// <item>Statistics are cached or computed efficiently to avoid N+1 queries.</item>
    /// <item>All operations support cancellation via <see cref="CancellationToken"/>.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public interface ICategoryService
    {
        #region Read Operations

        /// <summary>
        /// Retrieves all categories with their feed and unread counts populated.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of all categories with calculated statistics.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific category by ID with populated feed and unread counts.
        /// </summary>
        /// <param name="categoryId">The unique identifier of the category.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The category DTO with statistics or null if not found.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CategoryDto?> GetCategoryByIdAsync(int categoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds a complete category tree with hierarchical structure.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>Hierarchical tree of categories with populated children collections.</returns>
        /// <remarks>
        /// This method recursively builds the category hierarchy. Implementations should optimize
        /// to avoid multiple database round-trips and minimize memory allocation for deep hierarchies.
        /// </remarks>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all categories with their associated feeds fully populated.
        /// Optimized for scenarios requiring complete category-feed relationships.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list of categories with populated feed collections.</returns>
        /// <remarks>
        /// Use with caution as this loads all categories and their feeds into memory.
        /// Consider pagination or lazy loading for large datasets on low-resource machines.
        /// </remarks>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<List<CategoryDto>> GetAllCategoriesWithFeedsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific category with its associated feeds fully populated.
        /// </summary>
        /// <param name="categoryId">The ID of the category to retrieve.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The category with populated feed collection or null if not found.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CategoryDto?> GetCategoryWithFeedsAsync(int categoryId, CancellationToken cancellationToken = default);

        #endregion

        #region Write Operations

        /// <summary>
        /// Creates a new category with automatic sort order assignment.
        /// </summary>
        /// <param name="createDto">The DTO containing category creation data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The newly created category DTO with generated ID and sort order.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="createDto"/> has invalid data.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a category with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto createDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        /// <param name="categoryId">The ID of the category to update.</param>
        /// <param name="updateDto">The DTO containing updated category data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated category DTO or null if category not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="updateDto"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="InvalidOperationException">Thrown if another category with the same name already exists.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CategoryDto?> UpdateCategoryAsync(int categoryId, UpdateCategoryDto updateDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        /// <param name="categoryId">The ID of the category to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if deletion was successful; otherwise false (e.g., category not found).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="categoryId"/> is less than or equal to 0.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the category contains feeds and deletion is not allowed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reorders categories based on the provided list of IDs.
        /// </summary>
        /// <param name="reorderDto">The DTO containing reordering information.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if reordering was successful; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="reorderDto"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the category ID list is empty or contains duplicates.</exception>
        /// <exception cref="InvalidOperationException">Thrown if any category ID in the list does not exist.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> ReorderCategoriesAsync(ReorderCategoriesDto reorderDto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or creates a category by name. If the category doesn't exist, 
        /// it will be created with default values.
        /// </summary>
        /// <param name="name">The name of the category to find or create.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The existing or newly created category DTO.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null, empty, or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<CategoryDto> GetOrCreateCategoryAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics Operations

        /// <summary>
        /// Gets feed counts grouped by category ID.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping CategoryId to feed count.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, int>> GetFeedCountsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets unread article counts grouped by category ID.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A dictionary mapping CategoryId to unread article count.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<Dictionary<int, int>> GetUnreadCountsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Validation Operations

        /// <summary>
        /// Checks if a category with the specified name already exists.
        /// </summary>
        /// <param name="name">The category name to check.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>True if a category with this name exists; otherwise false.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="name"/> is null, empty, or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<bool> CategoryExistsByNameAsync(string name, CancellationToken cancellationToken = default);

        #endregion
    }
}