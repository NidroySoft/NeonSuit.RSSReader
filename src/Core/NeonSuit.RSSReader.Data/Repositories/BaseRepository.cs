// =======================================================
// Infrastructure/Repositories/BaseRepository.cs
// =======================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Generic base repository implementation providing CRUD and bulk operations 
    /// using Entity Framework Core with SQLite, optimized for low-resource environments.
    /// </summary>
    /// <typeparam name="T">The entity type. Must be a reference type with a parameterless constructor.</typeparam>
    /// <remarks>
    /// <para>
    /// All derived repositories inherit protected members (_context, _logger, _dbSet) 
    /// and should use them directly instead of redeclaring fields.
    /// </para>
    /// <para>
    /// Performance optimizations:
    /// <list type="bullet">
    /// <item><description>Read-only queries use <c>AsNoTracking()</c> by default</description></item>
    /// <item><description>Bulk operations use <c>ExecuteUpdateAsync</c>/<c>ExecuteDeleteAsync</c> where possible</description></item>
    /// <item><description>Property info caching for reflection-heavy operations</description></item>
    /// <item><description>All async methods support cancellation tokens</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    internal class BaseRepository<T> : IRepository<T> where T : class, new()
    {
        #region Protected Fields

        /// <summary>
        /// The Entity Framework Core database context instance.
        /// </summary>
        protected readonly RSSReaderDbContext _context;

        /// <summary>
        /// The Serilog logger instance enriched with context for the current entity type.
        /// </summary>
        protected readonly ILogger _logger;

        /// <summary>
        /// Provides typed access to the DbSet&lt;T&gt; for the current entity.
        /// </summary>
        protected DbSet<T> _dbSet => _context.Set<T>();

        // Cache for Id property reflection (performance optimization)
        private static readonly PropertyInfo? _idProperty = typeof(T).GetProperty("Id");

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseRepository{T}"/> class.
        /// </summary>
        /// <param name="context">The EF Core database context.</param>
        /// <param name="logger">The Serilog logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="logger"/> is null.</exception>
        public BaseRepository(RSSReaderDbContext context, ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _context = context;
            _logger = logger.ForContext<BaseRepository<T>>();

#if DEBUG
            _logger.Debug("{EntityType} repository initialized", typeof(T).Name);
#endif
        }

        #endregion

        #region Read Operations

        /// <inheritdoc />
        public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Use FindAsync for tracked entities (needed for updates)
                // For read-only scenarios, use GetByIdReadOnlyAsync
                return await _dbSet
                    .FindAsync(new object[] { id }, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByIdAsync operation cancelled for {EntityType} ID {Id}", typeof(T).Name, id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve {EntityType} with ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <summary>
        /// Retrieves an entity by ID with no tracking (read-only).
        /// </summary>
        /// <param name="id">The primary key value.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The entity if found; otherwise null.</returns>
        public virtual async Task<T?> GetByIdReadOnlyAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetByIdReadOnlyAsync cancelled for {EntityType} ID {Id}", typeof(T).Name, id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve {EntityType} with ID {Id} (read-only)", typeof(T).Name, id);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<List<T>> GetWhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(predicate)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetWhereAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve filtered {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<T?> FirstOrDefaultAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .FirstOrDefaultAsync(predicate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("FirstOrDefaultAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve first {EntityType} matching predicate", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<bool> AnyAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .AnyAsync(predicate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AnyAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check existence for {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .CountAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CountAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to count {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> CountWhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .CountAsync(predicate, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CountWhereAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to count filtered {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        #endregion

        #region Write Operations

        /// <inheritdoc />
        public virtual async Task<int> InsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                int rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Inserted new {EntityType} entity", typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("InsertAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.Error(ex, "Database error inserting {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var entry = _context.Entry(entity);

                if (entry.State == EntityState.Detached)
                {
                    _dbSet.Attach(entity);
                    entry.State = EntityState.Modified;
                }
                else
                {
                    entry.State = EntityState.Modified;
                }

                int rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Updated {EntityType} entity", typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.Error(ex, "Concurrency conflict updating {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var entry = _context.Entry(entity);

                if (entry.State == EntityState.Detached)
                {
                    _dbSet.Attach(entity);
                }

                _dbSet.Remove(entity);
                int rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Deleted {EntityType} entity", typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.Error(ex, "Concurrency conflict deleting {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete {EntityType}", typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Use ExecuteDeleteAsync for better performance (no entity loading)
                return await _dbSet
                    .Where(e => EF.Property<int>(e, "Id") == id)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteByIdAsync operation cancelled for {EntityType} ID {Id}", typeof(T).Name, id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete {EntityType} by ID {Id}", typeof(T).Name, id);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> InsertOrUpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                if (_idProperty == null)
                {
                    throw new InvalidOperationException($"Entity {typeof(T).Name} must have an 'Id' property for upsert.");
                }

                var idValue = (int?)_idProperty.GetValue(entity);
                if (idValue.HasValue && idValue.Value > 0)
                {
                    var existing = await GetByIdAsync(idValue.Value, cancellationToken).ConfigureAwait(false);
                    if (existing != null)
                    {
                        _context.Entry(existing).CurrentValues.SetValues(entity);
                        return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("InsertOrUpdateAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to upsert {EntityType}", typeof(T).Name);
                throw;
            }
        }

        #endregion

        #region Bulk Operations

        /// <inheritdoc />
        public virtual async Task<int> InsertAllAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null || !entities.Any())
                return 0;

            var entityList = entities.ToList();

            try
            {
                await _dbSet.AddRangeAsync(entityList, cancellationToken).ConfigureAwait(false);
                int rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Bulk inserted {Count} {EntityType} entities", entityList.Count, typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("InsertAllAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to bulk insert {Count} {EntityType} entities", entityList.Count, typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> UpdateAllAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            if (entities == null || !entities.Any())
                return 0;

            var entityList = entities.ToList();

            try
            {
                _dbSet.UpdateRange(entityList);
                int rows = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Bulk updated {Count} {EntityType} entities", entityList.Count, typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateAllAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to bulk update {Count} {EntityType} entities", entityList.Count, typeof(T).Name);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Use ExecuteDeleteAsync for efficient bulk delete without loading entities
                int rows = await _dbSet.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

                _logger.Warning("Deleted all {Count} records from {EntityType}", rows, typeof(T).Name);
                return rows;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("DeleteAllAsync operation cancelled for {EntityType}", typeof(T).Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete all {EntityType}", typeof(T).Name);
                throw;
            }
        }

        #endregion

        #region Protected Helpers

        /// <summary>
        /// Retrieves the table name for the current entity type from attributes or naming convention.
        /// </summary>
        /// <returns>The resolved table name.</returns>
        protected virtual string GetTableName()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<TableAttribute>();
            if (tableAttr?.Name != null)
                return tableAttr.Name;

            string name = type.Name;
            return name.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? name : name + "s";
        }

        #endregion
    }
}