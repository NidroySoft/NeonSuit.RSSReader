using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;

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
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<int> InsertAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return await _context.SaveChangesAsync();
        }

        public virtual async Task<int> UpdateAsync(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            return await _context.SaveChangesAsync();
        }

        public virtual async Task<int> DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return await _context.SaveChangesAsync();
        }

        public virtual async Task<int> DeleteByIdAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                return await DeleteAsync(entity);
            }
            return 0;
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
                        var existing = await GetByIdAsync(idValue);
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
            var entities = await GetAllAsync();
            if (!entities.Any())
                return 0;

            _dbSet.RemoveRange(entities);
            return await _context.SaveChangesAsync();
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
    }
}