using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for managing <see cref="Category"/> entities, which group RSS feeds 
    /// into logical folders or collections with support for custom ordering, colors, and hierarchy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Categories serve as the primary organizational structure for feeds in the RSS reader.
    /// They enable users to group related feeds (e.g., "News", "Technology", "Work", "Personal") 
    /// and provide visual customization (colors, icons) and sorting.
    /// </para>
    ///
    /// <para>
    /// Key responsibilities and behavioral expectations:
    /// </para>
    /// <list type="bullet">
    ///     <item>CRUD operations for categories with validation (unique names, non-empty names)</item>
    ///     <item>Ordering and reordering support via SortOrder field (user-draggable lists)</item>
    ///     <item>Unread article count aggregation per category (for badges in feed list/category view)</item>
    ///     <item>Automatic sort order assignment on creation</item>
    ///     <item>Change tracking management (detach, clear tracker) for bulk or performance scenarios</item>
    /// </list>
    ///
    /// <para>
    /// Business rules:
    /// </para>
    /// <list type="bullet">
    ///     <item>Category names must be unique (case-insensitive in most implementations)</item>
    ///     <item>Deleting a category does **not** delete feeds — feeds become uncategorized (CategoryId = null)</item>
    ///     <item>SortOrder is 0-based or 1-based (implementation-dependent); gaps are allowed</item>
    ///     <item>Unread counts include only articles where ArticleStatus = Unread and Feed.CategoryId matches</item>
    ///     <item>Reorder operations should be atomic (transactional) to prevent inconsistent ordering</item>
    /// </list>
    ///
    /// <para>
    /// Performance considerations:
    /// </para>
    /// <list type="bullet">
    ///     <item>GetUnreadCountsByCategoryAsync uses optimized JOIN + GROUP BY (no full entity loading)</item>
    ///     <item>ReorderAsync should use batch UPDATE or CASE WHEN for efficiency</item>
    ///     <item>Avoid loading full Category objects when only ID/count is needed</item>
    /// </list>
    ///
    /// <para>
    /// Usage scenarios:
    /// </para>
    /// <list type="bullet">
    ///     <item>Sidebar/category tree rendering with unread badges</item>
    ///     <item>Drag-and-drop reordering in settings</item>
    ///     <item>New category creation during feed subscription</item>
    ///     <item>Bulk feed import → auto-create categories if needed</item>
    /// </list>
    /// </remarks>
    public interface ICategoryRepository : IRepository<Category>
    {
        #region Basic Retrieval & Existence Checks

        /// <summary>
        /// Retrieves all categories sorted by <see cref="Category.SortOrder"/> ascending,
        /// then by <see cref="Category.Name"/> ascending as tie-breaker.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Ordered list of categories ready for UI rendering.</returns>
        Task<List<Category>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a category by its exact name (case-insensitive).
        /// </summary>
        /// <param name="name">The category name to search for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The matching <see cref="Category"/> or <c>null</c>.</returns>
        Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a category with the given name already exists.
        /// </summary>
        /// <param name="name">The category name to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if a category with this name exists; otherwise <c>false</c>.</returns>
        Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

        #endregion

        #region Change Tracking Management

        /// <summary>
        /// Clears the entire Entity Framework change tracker.
        /// </summary>
        /// <remarks>
        /// Extreme caution advised: only use in bulk import/sync scenarios.
        /// </remarks>
        void ClearChangeTracker();

        /// <summary>
        /// Detaches a specific category entity from change tracking by ID.
        /// </summary>
        /// <param name="id">The ID of the category to detach.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DetachEntityAsync(int id, CancellationToken cancellationToken = default);

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Deletes a category by ID.
        /// </summary>
        /// <param name="id">The ID of the category to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Does **not** delete associated feeds — sets Feed.CategoryId = null.
        /// </remarks>
        Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);

        #endregion

        #region Aggregates & Statistics

        /// <summary>
        /// Computes unread article counts grouped by category.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary of CategoryId → unread article count.</returns>
        Task<Dictionary<int, int>> GetUnreadCountsByCategoryAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Ordering & Sorting

        /// <summary>
        /// Reorders categories according to the provided sequence of IDs.
        /// </summary>
        /// <param name="categoryIds">Ordered list of category IDs (new sort order = index in list).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of categories whose SortOrder was updated.</returns>
        Task<int> ReorderAsync(List<int> categoryIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines the next available SortOrder value for a new category.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The next integer SortOrder (usually max + 1).</returns>
        Task<int> GetNextSortOrderAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new category with automatically assigned next SortOrder.
        /// </summary>
        /// <param name="name">Required category name (must be unique).</param>
        /// <param name="color">Optional hex color code (e.g., "#FF5733").</param>
        /// <param name="description">Optional description or notes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The newly created and persisted <see cref="Category"/> entity.</returns>
        Task<Category> CreateWithOrderAsync(string name, string? color = null, string? description = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the highest SortOrder value currently in use.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The maximum SortOrder value (or 0 if no categories exist).</returns>
        Task<int> GetMaxSortOrderAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}