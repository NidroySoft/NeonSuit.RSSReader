using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing user preferences (<see cref="UserPreferences"/> entities).
/// Provides strongly-typed and key-based access to user-specific settings with default value fallback.
/// </summary>
/// <remarks>
/// <para>
/// This repository abstracts storage and retrieval of user preferences, supporting:
/// - Key-based lookup and update
/// - Strongly-typed getters (string, bool, int) with safe defaults
/// - Reset to default values
/// - Categorized/grouped retrieval for UI or bulk operations
/// </para>
/// <para>
/// Implementations must ensure:
/// - Case-insensitive or normalized key comparison
/// - Efficient single-key lookups (indexed on Key column)
/// - Safe handling of missing keys (return defaults instead of null/exceptions)
/// - Atomic updates to avoid partial writes
/// </para>
/// </remarks>
public interface IUserPreferencesRepository : IRepository<UserPreferences>
{
    #region Key-based Read Operations

    /// <summary>
    /// Retrieves a preference by its unique key.
    /// </summary>
    /// <param name="key">The preference key to look up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="UserPreferences"/> entity if found; otherwise <see langword="null"/>.</returns>
    Task<UserPreferences?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the string value of a preference with a fallback.
    /// </summary>
    /// <param name="key">The preference key.</param>
    /// <param name="defaultValue">The value to return if the key is not found.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored value or <paramref name="defaultValue"/>.</returns>
    Task<string> GetValueAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the boolean value of a preference with a fallback.
    /// </summary>
    /// <param name="key">The preference key.</param>
    /// <param name="defaultValue">The value to return if the key is not found or not a valid bool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The boolean value or <paramref name="defaultValue"/>.</returns>
    Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the integer value of a preference with a fallback.
    /// </summary>
    /// <param name="key">The preference key.</param>
    /// <param name="defaultValue">The value to return if the key is not found or not a valid integer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The integer value or <paramref name="defaultValue"/>.</returns>
    Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default);

    #endregion

    #region Write Operations

    /// <summary>
    /// Sets or updates a preference value.
    /// </summary>
    /// <param name="key">The unique preference key.</param>
    /// <param name="value">The new string value to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous set operation.</returns>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a preference to its default value.
    /// </summary>
    /// <param name="key">The preference key to reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    Task ResetToDefaultAsync(string key, CancellationToken cancellationToken = default);

    #endregion

    #region Bulk & Aggregated Operations

    /// <summary>
    /// Retrieves all preferences grouped by category.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary where key is category name and value is list of preferences in that category.</returns>
    Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple preference values in a single operation.
    /// </summary>
    /// <param name="values">A dictionary of key-value pairs to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of preferences updated.</returns>
    Task<int> SetValuesBulkAsync(Dictionary<string, string> values, CancellationToken cancellationToken = default);

    #endregion
}