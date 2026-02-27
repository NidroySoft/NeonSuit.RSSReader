using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Globalization;

namespace NeonSuit.RSSReader.Data.Repositories;

/// <summary>
/// Repository implementation for managing <see cref="UserPreferences"/> entities.
/// Provides efficient key-based access, typed getters with defaults, and bulk/category operations.
/// </summary>
internal class UserPreferencesRepository : BaseRepository<UserPreferences>, IUserPreferencesRepository
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if context or logger is null.</exception>
    public UserPreferencesRepository(RSSReaderDbContext context, ILogger logger)
        : base(context, logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

#if DEBUG
        _logger.Debug("UserPreferencesRepository initialized");
#endif
    }

    #endregion

    #region Key-based Read Operations

    /// <inheritdoc />
    public async Task<UserPreferences?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            return await _dbSet
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Key == key, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetByKeyAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving preference by key '{Key}'", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetValueAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var pref = await GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
            return pref?.Value ?? defaultValue;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetValueAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting value for key '{Key}'", key);
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var value = await GetValueAsync(key, defaultValue.ToString(), cancellationToken).ConfigureAwait(false);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetBoolAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting bool for key '{Key}'", key);
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var value = await GetValueAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetIntAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting int for key '{Key}'", key);
            return defaultValue;
        }
    }

    #endregion

    #region Write Operations

    /// <inheritdoc />
    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var safeValue = value ?? string.Empty;
            var now = DateTime.UtcNow;

            var updated = await _dbSet
                .Where(p => p.Key == key)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Value, safeValue)
                    .SetProperty(p => p.LastModified, now),
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                var newPref = new UserPreferences
                {
                    Key = key,
                    Value = safeValue,
                    LastModified = now
                };

                await _dbSet.AddAsync(newPref, cancellationToken).ConfigureAwait(false);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.Debug("Set preference key '{Key}'", key);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SetValueAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.Error(ex, "Database error setting preference key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting preference key '{Key}'", key);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ResetToDefaultAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var defaultValue = PreferenceHelper.GetDefaultValue(key);
            await SetValueAsync(key, defaultValue, cancellationToken).ConfigureAwait(false);
            _logger.Debug("Reset preference key '{Key}' to default", key);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ResetToDefaultAsync cancelled for key '{Key}'", key);
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error resetting preference key '{Key}'", key);
            throw;
        }
    }

    #endregion

    #region Bulk & Aggregated Operations

    /// <inheritdoc />
    public async Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allPrefs = await _dbSet
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var categorized = new Dictionary<string, List<UserPreferences>>();

            foreach (var category in PreferenceHelper.GetCategorizedKeys())
            {
                var categoryPrefs = allPrefs
                    .Where(p => category.Value.Contains(p.Key))
                    .ToList();

                if (categoryPrefs.Any())
                    categorized[category.Key] = categoryPrefs;
            }

            return categorized;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("GetAllCategorizedAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving categorized preferences");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SetValuesBulkAsync(Dictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!values.Any())
            return 0;

        try
        {
            var now = DateTime.UtcNow;
            var updatedCount = 0;

            foreach (var kvp in values)
            {
                var updated = await _dbSet
                    .Where(p => p.Key == kvp.Key)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(p => p.Value, kvp.Value ?? string.Empty)
                        .SetProperty(p => p.LastModified, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updated == 0)
                {
                    var newPref = new UserPreferences
                    {
                        Key = kvp.Key,
                        Value = kvp.Value ?? string.Empty,
                        LastModified = now
                    };

                    await _dbSet.AddAsync(newPref, cancellationToken).ConfigureAwait(false);
                }

                updatedCount++;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.Information("Bulk updated {Count} preferences", updatedCount);
            return updatedCount;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("SetValuesBulkAsync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error bulk updating preferences");
            throw;
        }
    }

    #endregion
}