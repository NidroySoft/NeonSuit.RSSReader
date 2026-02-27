// =======================================================
// Core/Interfaces/Repositories/IRepository.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Generic repository interface defining standard CRUD, bulk, and query operations 
    /// for entities in the NeonSuit RSS Reader application.
    /// </summary>
    /// <typeparam name="T">The entity type. Must be a reference type with a parameterless constructor.</typeparam>
    /// <remarks>
    /// <para>
    /// All asynchronous methods accept a <see cref="CancellationToken"/> to support 
    /// cooperative cancellation and timeout scenarios, especially important in background sync 
    /// and UI-responsive operations on low-resource hardware.
    /// </para>
    /// <para>
    /// Read operations default to no-tracking for memory efficiency.
    /// Write operations are transactional and support concurrency handling.
    /// </para>
    /// </remarks>
    public interface IRepository<T> where T : class, new()
    {
        #region Read Operations

        /// <summary>
        /// Retrieves all entities of type <typeparamref name="T"/> from the database.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the list of entities.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        /// <remarks>Use with caution on large tables; prefer filtered/paginated queries when possible.</remarks>
        Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single entity by its primary key identifier.
        /// </summary>
        /// <param name="id">The primary key value.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The entity if found, otherwise null.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        /// <remarks>This method returns a tracked entity suitable for updates.</remarks>
        Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves entities matching the specified predicate with no tracking.
        /// </summary>
        /// <param name="predicate">Filter expression (e.g., x => x.IsActive).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>List of matching entities.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the first entity matching the predicate, or null if none found.
        /// </summary>
        /// <param name="predicate">Filter expression.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The first matching entity, or null.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if any entity matches the specified predicate.
        /// </summary>
        /// <param name="predicate">Filter expression.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>True if at least one matching entity exists.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the total number of entities in the table.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The total count.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the number of entities matching the specified predicate.
        /// </summary>
        /// <param name="predicate">Filter expression.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The filtered count.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> CountWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

        #endregion

        #region Write Operations

        /// <summary>
        /// Inserts a new entity into the database.
        /// </summary>
        /// <param name="entity">The entity to insert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows (usually 1).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="DbUpdateException">Thrown if a database constraint is violated.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> InsertAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing entity in the database.
        /// </summary>
        /// <param name="entity">The entity with updated values (must have a valid primary key).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows (usually 1).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown if the entity was modified by another user.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity from the database.
        /// </summary>
        /// <param name="entity">The entity to delete (must be attached or have a valid primary key).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows (usually 1).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown if the entity was already deleted.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an entity by its primary key identifier using efficient bulk delete.
        /// </summary>
        /// <param name="id">The primary key value of the entity to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows (0 or 1).</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> DeleteByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs an upsert operation: inserts if the entity does not exist, updates if it does.
        /// </summary>
        /// <param name="entity">The entity to upsert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the entity type lacks an 'Id' property.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        Task<int> InsertOrUpdateAsync(T entity, CancellationToken cancellationToken = default);

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Inserts multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The collection of entities to insert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        /// <remarks>For large collections, consider batching to avoid memory pressure.</remarks>
        Task<int> InsertAllAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates multiple entities in a single batch operation.
        /// </summary>
        /// <param name="entities">The collection of entities to update.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of affected rows.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        /// <remarks>All entities must have valid primary keys. For large collections, consider batching.</remarks>
        Task<int> UpdateAllAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all records from the table. Use with extreme caution.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of deleted rows.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
        /// <exception cref="Exception">Thrown if a database error occurs.</exception>
        /// <remarks>
        /// This is a destructive operation with no undo. Consider soft-delete patterns instead when possible.
        /// Uses efficient bulk delete without loading entities into memory.
        /// </remarks>
        Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}