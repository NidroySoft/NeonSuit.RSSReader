namespace NeonSuit.RSSReader.Core.Interfaces.Repositories
{
    /// <summary>
    /// Generic interface for CRUD operations in repositories.
    /// Provides a standard contract for database interactions.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    public interface IRepository<T> where T : class, new()
    {
        /// <summary>
        /// Retrieves all records from the table.
        /// </summary>
        Task<List<T>> GetAllAsync();

        /// <summary>
        /// Retrieves a single record by its primary key ID.
        /// </summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>
        /// Inserts a new record into the database.
        /// </summary>
        Task<int> InsertAsync(T entity);

        /// <summary>
        /// Updates an existing record in the database.
        /// </summary>
        Task<int> UpdateAsync(T entity);

        /// <summary>
        /// Deletes a record from the database.
        /// </summary>
        Task<int> DeleteAsync(T entity);

        /// <summary>
        /// Deletes a record by its primary key ID.
        /// </summary>
        Task<int> DeleteByIdAsync(int id);

        /// <summary>
        /// Returns the total number of records in the table.
        /// </summary>
        Task<int> CountAsync();

        /// <summary>
        /// Deletes all records from the table.
        /// </summary>
        Task<int> DeleteAllAsync(); // <--- Fixes your SettingsService error

        /// <summary>
        /// Inserts or updates a record (Upsert).
        /// </summary>
        Task<int> InsertOrUpdateAsync(T entity);

        /// <summary>
        /// Inserts a collection of records in a single transaction.
        /// </summary>
        Task<int> InsertAllAsync(IEnumerable<T> entities); // <--- Needed for Import logic

        /// <summary>
        /// Updates a collection of records in a single transaction.
        /// </summary>
        Task<int> UpdateAllAsync(IEnumerable<T> entities);
    }
}