using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Data.Database;
using System.Linq.Expressions;
using System.Reflection;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Generic base repository with CRUD operations using Entity Framework Core.
    /// Platform-agnostic implementation compatible with .NET Standard 2.0+.
    /// </summary>
    /// <typeparam name="T">The type of the entity (must have a parameterless constructor).</typeparam>
    public class BaseRepository<T> : IRepository<T> where T : class, new()
    {
        protected readonly RssReaderDbContext _context;
        protected DbSet<T> _dbSet => _context.Set<T>();

        public BaseRepository(RssReaderDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            var result = await _dbSet.AsNoTracking().ToListAsync();

            return result;
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        /// <summary>
        /// Gets an entity by ID without tracking (for read-only operations).
        /// </summary>
        public virtual async Task<T?> GetByIdNoTrackingAsync(int id)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        }

        public virtual async Task<int> InsertAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            var rowsAffected = await _context.SaveChangesAsync();

            // ? Buscar la propiedad Id
            var idProperty = entity.GetType().GetProperty("Id");
            if (idProperty != null)
            {
                var id = idProperty.GetValue(entity);

                // Si el ID es 0 pero rowsAffected = 1, forzar la recarga
                if (id is int intId && intId == 0 && rowsAffected == 1)
                {
                    // Opción 1: Recargar la entidad
                    await _context.Entry(entity).ReloadAsync();
                    id = idProperty.GetValue(entity);

                    // Opción 2: Si sigue siendo 0, devolver rowsAffected
                    if (id is int reloadedId && reloadedId > 0)
                        return reloadedId;
                }
                else if (id is int validId && validId > 0)
                {
                    return validId;
                }
            }

            // Fallback: devolver filas afectadas (1)
            return rowsAffected;
        }

        public virtual async Task<int> UpdateAsync(T entity)
        {
            try
            {
                var entry = _context.Entry(entity);

                // Si ya está siendo trackeada, actualizar el estado
                if (entry.State != EntityState.Detached)
                {
                    entry.State = EntityState.Modified;
                    return await _context.SaveChangesAsync();
                }

                // Si no está trackeada, obtener la entidad existente
                var idProperty = entity.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = (int?)idProperty.GetValue(entity) ?? 0;
                    if (idValue > 0)
                    {
                        var existing = await _dbSet.FindAsync(idValue);
                        if (existing != null)
                        {
                            _context.Entry(existing).CurrentValues.SetValues(entity);
                            return await _context.SaveChangesAsync();
                        }
                    }
                }

                // Fallback al attach
                _dbSet.Attach(entity);
                entry.State = EntityState.Modified;
                return await _context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // Si falla, intentar con una nueva instancia
                var idProperty = entity.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = (int?)idProperty.GetValue(entity) ?? 0;
                    var freshEntity = await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == idValue);
                    if (freshEntity != null)
                    {
                        _context.Entry(freshEntity).CurrentValues.SetValues(entity);
                        return await _context.SaveChangesAsync();
                    }
                }
                throw;
            }
        }

        public virtual async Task<int> DeleteAsync(T entity)
        {
            try
            {
                // ? VALIDACIÓN DE NULL PRIMERO
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                var entry = _context.Entry(entity);

                if (entry.State == EntityState.Detached)
                {
                    // Si está detached, attachar y eliminar
                    _dbSet.Attach(entity);
                }

                _dbSet.Remove(entity);
                return await _context.SaveChangesAsync();
            }
            catch (ArgumentNullException)
            {
                // ? RELANZAR ArgumentNullException sin modificarla
                throw;
            }
            catch
            {
                // Si falla, intentar eliminar por ID
                var idProperty = entity.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = (int?)idProperty.GetValue(entity) ?? 0;
                    return await DeleteByIdAsync(idValue);
                }
                throw;
            }
        }
        public virtual async Task<int> DeleteByIdAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);
            if (entity != null)
            {
                _dbSet.Remove(entity);
                return await _context.SaveChangesAsync();
            }
            return 0;
        }

        /// <summary>
        /// Elimina una entidad por ID usando SQL directo (sin tracking).
        /// </summary>
        public virtual async Task<int> DeleteDirectAsync(int id)
        {
            var tableName = GetTableName();
            var sql = $"DELETE FROM {tableName} WHERE Id = @p0";
            return await _context.ExecuteSqlCommandAsync(sql,cancellationToken:default, id);
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        /// <summary>
        /// Gets records that match a specific condition.
        /// </summary>
        protected async Task<List<T>> GetWhereAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).AsNoTracking().ToListAsync();
        }

        /// <summary>
        /// Gets the first record that matches a specific condition.
        /// </summary>
        protected async Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// Counts records that match a specific condition.
        /// </summary>
        protected async Task<int> CountWhereAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        /// <summary>
        /// Inserts or updates a record (Upsert).
        /// </summary>
        public virtual async Task<int> InsertOrUpdateAsync(T entity)
        {
            var entry = _context.Entry(entity);

            if (entry.State == EntityState.Detached)
            {
                var idProperty = entity.GetType().GetProperty("Id");
                if (idProperty != null)
                {
                    var idValue = (int?)idProperty.GetValue(entity) ?? 0;
                    if (idValue > 0)
                    {
                        var existing = await _dbSet.FindAsync(idValue);
                        if (existing != null)
                        {
                            _context.Entry(existing).CurrentValues.SetValues(entity);
                            return await _context.SaveChangesAsync();
                        }
                    }
                }

                await _dbSet.AddAsync(entity);
            }

            return await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Inserts multiple records in a single batch.
        /// </summary>
        public virtual async Task<int> InsertAllAsync(IEnumerable<T> entities)
        {
            if (!entities.Any())
                return 0;

            await _dbSet.AddRangeAsync(entities);
            return await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Updates multiple records in a single batch.
        /// </summary>
        public virtual async Task<int> UpdateAllAsync(IEnumerable<T> entities)
        {
            if (!entities.Any())
                return 0;

            _dbSet.UpdateRange(entities);
            return await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Deletes all records from the table.
        /// </summary>
        public virtual async Task<int> DeleteAllAsync()
        {
            var tableName = GetTableName();
            var sql = $"DELETE FROM {tableName}";
            return await _context.ExecuteSqlCommandAsync(sql);
        }

        /// <summary>
        /// Detaches an entity from the change tracker.
        /// </summary>
        public virtual void DetachEntity(T entity)
        {
            var entry = _context.Entry(entity);
            if (entry != null)
            {
                entry.State = EntityState.Detached;
            }
        }

        /// <summary>
        /// Detaches an entity by ID from the change tracker.
        /// </summary>
        public virtual async Task DetachEntityAsync(int id)
        {
            var tracked = _context.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => {
                    var prop = e.Property("Id");
                    return prop != null && prop.CurrentValue != null && (int)prop.CurrentValue == id;
                });

            if (tracked != null)
            {
                tracked.State = EntityState.Detached;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Begins a new transaction.
        /// </summary>
        protected async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        /// <summary>
        /// Executes an action within a transaction.
        /// </summary>
        protected async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            using var transaction = await BeginTransactionAsync();
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Checks if any record matches the predicate.
        /// </summary>
        protected async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }

        /// <summary>
        /// Gets records with pagination.
        /// </summary>
        protected async Task<List<T>> GetPagedAsync(
            Expression<Func<T, bool>> predicate,
            int page,
            int pageSize,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true)
        {
            var query = _dbSet.Where(predicate).AsNoTracking();

            if (orderBy != null)
            {
                query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        /// <summary>
        /// Gets a queryable for custom queries.
        /// </summary>
        protected IQueryable<T> GetQueryable()
        {
            return _dbSet.AsNoTracking().AsQueryable();
        }

        /// <summary>
        /// Gets a queryable for custom queries with tracking enabled.
        /// </summary>
        protected IQueryable<T> GetQueryableWithTracking()
        {
            return _dbSet.AsQueryable();
        }

        private string GetTableName()
        {
            var type = typeof(T);
            var tableAttr = type.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.TableAttribute>();
            return tableAttr?.Name ?? $"{type.Name}s";
        }

        public void ClearChangeTracker()
        {
            _context.ChangeTracker.Clear();
        }
    }
}