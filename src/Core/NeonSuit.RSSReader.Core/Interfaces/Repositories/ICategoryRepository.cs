using NeonSuit.RSSReader.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    public interface ICategoryRepository
    {
        #region Basic Retrieval & Existence Checks

        /// <summary>
        /// Retrieves a single category by its primary key.
        /// </summary>
        /// <param name="id">The unique category identifier.</param>
        /// <returns>The <see cref="Category"/> if found; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// Primary method for loading category details for editing or display.
        /// Returns tracked entity (EF Core default).
        /// </remarks>
        Task<Category?> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves all categories without any ordering guarantee.
        /// </summary>
        /// <returns>List of all categories in the database.</returns>
        /// <remarks>
        /// Use for administrative purposes or when order is irrelevant.
        /// Prefer <see cref="GetAllOrderedAsync"/> for UI display.
        /// </remarks>
        Task<List<Category>> GetAllAsync();

        /// <summary>
        /// Retrieves all categories sorted by <see cref="Category.SortOrder"/> ascending,
        /// then by <see cref="Category.Name"/> ascending as tie-breaker.
        /// </summary>
        /// <returns>Ordered list of categories ready for UI rendering (sidebar, dropdowns).</returns>
        /// <remarks>
        /// Default method for feed list/category tree views.
        /// Ensures consistent user-defined ordering.
        /// </remarks>
        Task<List<Category>> GetAllOrderedAsync();

        /// <summary>
        /// Finds a category by its exact name (case-insensitive in most implementations).
        /// </summary>
        /// <param name="name">The category name to search for.</param>
        /// <returns>The matching <see cref="Category"/> or <c>null</c>.</returns>
        /// <remarks>
        /// Used during creation to prevent duplicates or during import to match existing categories.
        /// </remarks>
        Task<Category?> GetByNameAsync(string name);

        /// <summary>
        /// Checks whether a category with the given name already exists.
        /// </summary>
        /// <param name="name">The category name to check (case-insensitive).</param>
        /// <returns><c>true</c> if a category with this name exists; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// Optimized scalar query — prevents full entity load.
        /// Used in validation before insert.
        /// </remarks>
        Task<bool> ExistsByNameAsync(string name);

        #endregion

        #region Change Tracking Management

        /// <summary>
        /// Clears the entire Entity Framework change tracker.
        /// </summary>
        /// <remarks>
        /// <para>Extreme caution advised:</para>
        /// <list type="bullet">
        ///     <item>Only use in bulk import/sync scenarios where many entities are loaded/read-only.</item>
        ///     <item>Causes loss of all pending changes — ensure SaveChanges was called if needed.</item>
        ///     <item>Not thread-safe in concurrent contexts.</item>
        /// </list>
        /// Intended to reduce memory pressure during large operations.
        /// </remarks>
        void ClearChangeTracker();

        /// <summary>
        /// Detaches a specific category entity from change tracking by ID.
        /// </summary>
        /// <param name="id">The ID of the category to detach.</param>
        /// <remarks>
        /// Prevents unintended persistence of modified read-only entities.
        /// Safe to call even if entity was not tracked.
        /// </remarks>
        Task DetachEntityAsync(int id);

        #endregion

        #region CRUD Operations

        /// <summary>
        /// Inserts a new category into the database.
        /// </summary>
        /// <param name="category">The <see cref="Category"/> entity to insert (Name required).</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Validates uniqueness of name.
        /// Sets creation timestamp and default SortOrder if not provided.
        /// Throws on duplicate name or invalid data.
        /// </remarks>
        Task<int> InsertAsync(Category category);

        /// <summary>
        /// Updates an existing category with new values.
        /// </summary>
        /// <param name="category">The modified <see cref="Category"/> (must have valid ID).</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Only updates changed properties (optimistic concurrency).
        /// Name changes are validated for uniqueness.
        /// </remarks>
        Task<int> UpdateAsync(Category category);

        /// <summary>
        /// Deletes a category by ID.
        /// </summary>
        /// <param name="id">The ID of the category to delete.</param>
        /// <returns>Number of rows affected (1 on success).</returns>
        /// <remarks>
        /// Does **not** delete associated feeds — sets Feed.CategoryId = null.
        /// Cascades to any dependent data if configured.
        /// Updates any cached counts or UI state via events.
        /// </remarks>
        Task<int> DeleteAsync(int id);

        #endregion

        #region Aggregates & Statistics

        /// <summary>
        /// Computes unread article counts grouped by category.
        /// </summary>
        /// <returns>Dictionary of CategoryId → unread article count.</returns>
        /// <remarks>
        /// Uses efficient JOIN + GROUP BY query (no full entity loading).
        /// Only includes categories with unread > 0 (or all — implementation-dependent).
        /// Used to display badges in category list/sidebar.
        /// </remarks>
        Task<Dictionary<int, int>> GetUnreadCountsByCategoryAsync();

        #endregion

        #region Ordering & Sorting

        /// <summary>
        /// Reorders categories according to the provided sequence of IDs.
        /// </summary>
        /// <param name="categoryIds">Ordered list of category IDs (new sort order = index in list).</param>
        /// <returns>Number of categories whose SortOrder was updated.</returns>
        /// <remarks>
        /// Atomic operation — should be wrapped in transaction.
        /// Updates SortOrder for all provided IDs; others remain unchanged.
        /// Used after drag-and-drop reordering in UI.
        /// </remarks>
        Task<int> ReorderAsync(List<int> categoryIds);

        /// <summary>
        /// Determines the next available SortOrder value for a new category.
        /// </summary>
        /// <returns>The next integer SortOrder (usually max + 1).</returns>
        /// <remarks>
        /// Safe for concurrent inserts (uses MAX + 1 pattern).
        /// </remarks>
        Task<int> GetNextSortOrderAsync();

        /// <summary>
        /// Creates a new category with automatically assigned next SortOrder.
        /// </summary>
        /// <param name="name">Required category name (must be unique).</param>
        /// <param name="color">Optional hex color code (e.g., "#FF5733").</param>
        /// <param name="description">Optional description or notes.</param>
        /// <returns>The newly created and persisted <see cref="Category"/> entity.</returns>
        /// <remarks>
        /// Convenience method combining creation + sort order assignment.
        /// Validates name uniqueness before insert.
        /// Sets default values for optional fields.
        /// </remarks>
        Task<Category> CreateWithOrderAsync(string name, string? color = null, string? description = null);

        /// <summary>
        /// Retrieves the highest SortOrder value currently in use.
        /// </summary>
        /// <returns>The maximum SortOrder value (or 0 if no categories exist).</returns>
        /// <remarks>
        /// Used internally by <see cref="GetNextSortOrderAsync"/> and ordering logic.
        /// Fast scalar query.
        /// </remarks>
        Task<int> GetMaxSortOrderAsync();

        #endregion
    }
}