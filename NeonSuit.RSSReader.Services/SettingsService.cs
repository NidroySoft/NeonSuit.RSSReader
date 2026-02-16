using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of ISettingsService using a ConcurrentDictionary as a cache 
    /// to minimize database hits on frequently accessed configuration.
    /// Thread-safe implementation using SemaphoreSlim for cache synchronization.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly IUserPreferencesRepository _repository;
        private readonly ConcurrentDictionary<string, UserPreferences> _cache;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private readonly ILogger _logger;
        private bool _isCacheInitialized = false;

        /// <inheritdoc />
        public event EventHandler<PreferenceChangedEventArgs>? OnPreferenceChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="repository">The user preferences repository.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when repository or logger is null.</exception>
        public SettingsService(IUserPreferencesRepository repository, ILogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger?.ForContext<SettingsService>() ?? throw new ArgumentNullException(nameof(logger));
            _cache = new ConcurrentDictionary<string, UserPreferences>();
        }

        /// <summary>
        /// Internal helper that performs actual cache initialization from the database.
        /// WARNING: Caller MUST hold the _cacheLock before calling this method.
        /// </summary>
        private async Task InitializeCacheUnsafeAsync()
        {
            var allPrefs = await _repository.GetAllAsync().ConfigureAwait(false);

            foreach (var pref in allPrefs)
            {
                _cache[pref.Key] = pref;
            }

            _isCacheInitialized = true;
            _logger.Debug("Settings cache initialized with {Count} items.", _cache.Count);
        }

        /// <summary>
        /// Ensures the in-memory cache is populated from the database.
        /// Implements a double-check locking pattern for thread safety.
        /// </summary>
        private async Task EnsureCacheInitializedAsync()
        {
            if (_isCacheInitialized) return;

            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_isCacheInitialized) return;

                await InitializeCacheUnsafeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize settings cache.");
                // No relanzar - el cache no se inicializa pero la operación continúa
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<string> GetValueAsync(string key, string defaultValue = "")
        {
            await EnsureCacheInitializedAsync().ConfigureAwait(false);

            if (_cache.TryGetValue(key, out var cachedPref))
                return cachedPref.Value;

            var value = await _repository.GetValueAsync(key, defaultValue).ConfigureAwait(false);

            var pref = await _repository.GetByKeyAsync(key).ConfigureAwait(false);
            if (pref != null)
                _cache[key] = pref;

            return value;
        }

        /// <inheritdoc />
        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
        {
            var value = await GetValueAsync(key, defaultValue.ToString()).ConfigureAwait(false);
            return bool.TryParse(value, out bool result) ? result : defaultValue;
        }

        /// <inheritdoc />
        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            var value = await GetValueAsync(key, defaultValue.ToString()).ConfigureAwait(false);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        /// <inheritdoc />
        public async Task<double> GetDoubleAsync(string key, double defaultValue = 0.0)
        {
            var value = await GetValueAsync(key, defaultValue.ToString()).ConfigureAwait(false);
            return double.TryParse(value, out double result) ? result : defaultValue;
        }

        /// <inheritdoc />
        public async Task SetValueAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            var safeValue = value ?? string.Empty;

            await EnsureCacheInitializedAsync().ConfigureAwait(false);

            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!PreferenceHelper.ValidateValue(key, safeValue))
                {
                    _logger.Warning("Invalid setting attempt: {Key} = {Value}", key, safeValue);
                    return;
                }

                var existingPref = await _repository.GetByKeyAsync(key).ConfigureAwait(false);
                var shouldRaiseEvent = false;

                if (existingPref == null)
                {
                    // ✅ Nueva preferencia - siempre disparar evento
                    var newPref = new UserPreferences
                    {
                        Key = key,
                        Value = safeValue,
                        LastModified = DateTime.UtcNow
                    };

                    await _repository.InsertAsync(newPref).ConfigureAwait(false);
                    _cache[key] = newPref;
                    shouldRaiseEvent = true;
                }
                else
                {
                    // ✅ Solo disparar evento si el valor cambió realmente
                    if (existingPref.Value != safeValue)
                    {
                        await _repository.SetValueAsync(key, safeValue).ConfigureAwait(false);
                        shouldRaiseEvent = true;

                        if (_cache.TryGetValue(key, out var cached))
                        {
                            cached.Value = safeValue;
                            cached.LastModified = DateTime.UtcNow;
                        }
                        else
                        {
                            var updated = await _repository.GetByKeyAsync(key).ConfigureAwait(false);
                            if (updated != null) _cache[key] = updated;
                        }
                    }
                    else
                    {
                        _logger.Debug("Setting unchanged: {Key} = {Value} (no update)", key, safeValue);
                    }
                }

                // ✅ Disparar evento solo si shouldRaiseEvent es true
                if (shouldRaiseEvent)
                {
                    OnPreferenceChanged?.Invoke(this, new PreferenceChangedEventArgs(key, safeValue));
                    _logger.Debug("Setting updated: {Key} = {Value}", key, safeValue);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating setting {Key}.", key);
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public Task SetBoolAsync(string key, bool value) => SetValueAsync(key, value.ToString());

        /// <inheritdoc />
        public Task SetIntAsync(string key, int value) => SetValueAsync(key, value.ToString());

        /// <inheritdoc />
        public Task SetDoubleAsync(string key, double value) => SetValueAsync(key, value.ToString());

        /// <inheritdoc />
        public async Task ResetToDefaultAsync(string key)
        {
            var defaultValue = PreferenceHelper.GetDefaultValue(key);
            await SetValueAsync(key, defaultValue).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ResetAllToDefaultsAsync()
        {
            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _repository.DeleteAllAsync().ConfigureAwait(false);

                _cache.Clear();
                _isCacheInitialized = false;

                _logger.Information("All settings have been reset to default values.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset all settings.");
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ExportToFileAsync(string filePath)
        {
            try
            {
                var allPrefs = await _repository.GetAllAsync().ConfigureAwait(false);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(allPrefs, options);
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

                _logger.Information("Settings successfully exported to {Path}.", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Export failed.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ImportFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Import file not found.", filePath);

            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                string json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var prefs = JsonSerializer.Deserialize<List<UserPreferences>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (prefs == null)
                    throw new InvalidDataException("Invalid JSON format.");

                await _repository.DeleteAllAsync().ConfigureAwait(false);
                await _repository.InsertAllAsync(prefs).ConfigureAwait(false);

                _cache.Clear();
                _isCacheInitialized = false;
                await InitializeCacheUnsafeAsync().ConfigureAwait(false);

                _logger.Information("Settings successfully imported from {Path}.", filePath);
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Import failed - invalid JSON format.");
                throw;
            }
            catch (Exception ex) when (ex is not InvalidDataException)
            {
                _logger.Error(ex, "Import failed.");
                throw;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, List<UserPreferences>>> GetAllCategorizedAsync()
        {
            // No necesita inicializar el cache - obtiene datos directamente del repositorio
            return await _repository.GetAllCategorizedAsync().ConfigureAwait(false);
        }
    }
}