using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using Serilog;
using System.Globalization;

namespace NeonSuit.RSSReader.Data.Repositories
{
    /// <summary>
    /// Repository for managing user preferences and application configuration.
    /// Uses Entity Framework Core for data access operations.
    /// </summary>
    public class UserPreferencesRepository : BaseRepository<UserPreferences>, IUserPreferencesRepository
    {
        private readonly ILogger _logger;

        public UserPreferencesRepository(RssReaderDbContext context, ILogger logger) : base(context)
        {
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<UserPreferencesRepository>();
        }

        /// <summary>
        /// Retrieves a preference record by its unique key.
        /// </summary>
        public async Task<UserPreferences?> GetByKeyAsync(string key)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Key == key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve preference by key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Gets the string value of a preference. If it doesn't exist, creates it with the default value.
        /// </summary>
        public async Task<string> GetValueAsync(string key, string defaultValue = "")
        {
            try
            {
                var pref = await GetByKeyAsync(key);
                if (pref != null)
                    return pref.Value;

                // If it doesn't exist, create it using the provided default or the system default
                var defaultVal = string.IsNullOrEmpty(defaultValue)
                    ? PreferenceHelper.GetDefaultValue(key)
                    : defaultValue;

                var newPref = new UserPreferences
                {
                    Key = key,
                    Value = defaultVal,
                    LastModified = DateTime.UtcNow
                };

                await InsertAsync(newPref);
                return defaultVal;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets or updates a preference value after validating it.
        /// </summary>
        public async Task SetValueAsync(string key, string value)
        {
            try
            {
                // ? Convertir null a string.Empty antes de insertar
                var safeValue = value ?? string.Empty;

                if (!PreferenceHelper.ValidateValue(key, safeValue))
                {
                    _logger.Warning("Invalid value for preference {Key}: {Value}", key, safeValue);
                    return;
                }

                await SetValueInternalAsync(key, safeValue);
                _logger.Debug("Preference set: {Key} = {Value}", key, safeValue);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to set preference: {Key} = {Value}", key, value);
                throw;
            }
        }

        /// <summary>
        /// Bulk set multiple preference values efficiently.
        /// </summary>
        public async Task SetValuesAsync(Dictionary<string, string> keyValuePairs)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var kvp in keyValuePairs)
                {
                    if (PreferenceHelper.ValidateValue(kvp.Key, kvp.Value))
                    {
                        await SetValueInternalAsync(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        _logger.Warning("Invalid value for preference {Key}: {Value}", kvp.Key, kvp.Value);
                    }
                }

                await transaction.CommitAsync();
                _logger.Debug("Set {Count} preferences in batch", keyValuePairs.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Failed to set batch preferences");
                throw;
            }
        }

        /// <summary>
        /// Retrieves a boolean preference value.
        /// </summary>
        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
        {
            try
            {
                var value = await GetValueAsync(key, defaultValue.ToString());
                return bool.TryParse(value, out bool result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get boolean preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves an integer preference value.
        /// </summary>
        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            try
            {
                var value = await GetValueAsync(key, defaultValue.ToString());
                return int.TryParse(value, out int result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get integer preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves a double preference value.
        /// </summary>
        public async Task<double> GetDoubleAsync(string key, double defaultValue = 0.0)
        {
            try
            {
                var value = await GetValueAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture));
                return double.TryParse(value, out double result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get double preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves a DateTime preference value.
        /// </summary>
        public async Task<DateTime> GetDateTimeAsync(string key, DateTime defaultValue)
        {
            try
            {
                var value = await GetValueAsync(key, defaultValue.ToString("O"));
                return DateTime.TryParse(value, out DateTime result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get DateTime preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Retrieves a TimeSpan preference value.
        /// </summary>
        public async Task<TimeSpan> GetTimeSpanAsync(string key, TimeSpan defaultValue)
        {
            try
            {
                var value = await GetValueAsync(key, defaultValue.ToString());
                return TimeSpan.TryParse(value, out TimeSpan result) ? result : defaultValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get TimeSpan preference: {Key}", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a boolean preference value.
        /// </summary>
        public async Task SetBoolAsync(string key, bool value)
        {
            await SetValueAsync(key, value.ToString());
        }

        /// <summary>
        /// Sets an integer preference value.
        /// </summary>
        public async Task SetIntAsync(string key, int value)
        {
            await SetValueAsync(key, value.ToString());
        }

        /// <summary>
        /// Sets a double preference value.
        /// </summary>
        public async Task SetDoubleAsync(string key, double value)
        {
            await SetValueAsync(key, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Sets a DateTime preference value.
        /// </summary>
        public async Task SetDateTimeAsync(string key, DateTime value)
        {
            await SetValueAsync(key, value.ToString("O"));
        }

        /// <summary>
        /// Sets a TimeSpan preference value.
        /// </summary>
        public async Task SetTimeSpanAsync(string key, TimeSpan value)
        {
            await SetValueAsync(key, value.ToString());
        }

        /// <summary>
        /// Resets a specific preference to its default defined value.
        /// </summary>
        public async Task ResetToDefaultAsync(string key)
        {
            try
            {
                var defaultValue = PreferenceHelper.GetDefaultValue(key);
                await SetValueAsync(key, defaultValue);
                _logger.Information("Reset preference to default: {Key} = {Value}", key, defaultValue);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset preference to default: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Resets multiple preferences to their default values.
        /// </summary>
        public async Task ResetToDefaultsAsync(IEnumerable<string> keys)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var key in keys)
                {
                    var defaultValue = PreferenceHelper.GetDefaultValue(key);
                    await SetValueInternalAsync(key, defaultValue);
                }

                await transaction.CommitAsync();
                _logger.Information("Reset {Count} preferences to defaults", keys.Count());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Failed to reset preferences to defaults");
                throw;
            }
        }

        /// <summary>
        /// Returns all stored preferences organized by their defined categories.
        /// </summary>
        public async Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync()
        {
            try
            {
                var allPrefs = await GetAllAsync();
                var categorized = new Dictionary<string, List<UserPreferences>>();

                foreach (var category in PreferenceHelper.GetCategorizedKeys())
                {
                    var categoryPrefs = allPrefs.Where(p => category.Value.Contains(p.Key)).ToList();
                    categorized[category.Key] = categoryPrefs;
                }

                return categorized;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get categorized preferences");
                throw;
            }
        }

        /// <summary>
        /// Checks if a preference exists.
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .AnyAsync(p => p.Key == key);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check preference existence: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Deletes a preference by its key.
        /// </summary>
        public async Task<bool> DeleteByKeyAsync(string key)
        {
            try
            {
                var result = await _dbSet
                    .Where(p => p.Key == key)
                    .ExecuteDeleteAsync();

                var deleted = result > 0;
                if (deleted)
                {
                    _logger.Information("Deleted preference: {Key}", key);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete preference: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Gets preferences with keys matching a pattern.
        /// </summary>
        public async Task<List<UserPreferences>> GetByKeyPatternAsync(string pattern)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(p => EF.Functions.Like(p.Key, pattern))
                    .OrderBy(p => p.Key)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preferences by pattern: {Pattern}", pattern);
                throw;
            }
        }

        /// <summary>
        /// Gets preferences that were modified after a specific date.
        /// </summary>
        public async Task<List<UserPreferences>> GetModifiedAfterAsync(DateTime date)
        {
            try
            {
                return await _dbSet
                    .AsNoTracking()
                    .Where(p => p.LastModified >= date)
                    .OrderByDescending(p => p.LastModified)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preferences modified after: {Date}", date);
                throw;
            }
        }

        /// <summary>
        /// Gets the count of preferences by category.
        /// </summary>
        public async Task<Dictionary<string, int>> GetCountByCategoryAsync()
        {
            try
            {
                var allPrefs = await GetAllAsync();
                var categories = PreferenceHelper.GetCategorizedKeys();
                var result = new Dictionary<string, int>();

                foreach (var category in categories)
                {
                    var count = allPrefs.Count(p => category.Value.Contains(p.Key));
                    result[category.Key] = count;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preference count by category");
                throw;
            }
        }

        /// <summary>
        /// Internal method to set preference value with optimized EF Core operations.
        /// </summary>
        private async Task SetValueInternalAsync(string key, string value)
        {
            var existing = await GetByKeyAsync(key);
            if (existing != null)
            {
                // Update existing using ExecuteUpdate for efficiency
                await _dbSet
                    .Where(p => p.Key == key)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(p => p.Value, value)
                               .SetProperty(p => p.LastModified, DateTime.UtcNow));
            }
            else
            {
                // Insert new preference
                var newPref = new UserPreferences
                {
                    Key = key,
                    Value = value,
                    LastModified = DateTime.UtcNow
                };

                await _dbSet.AddAsync(newPref);
                await _context.SaveChangesAsync();
            }
        }
    }
}