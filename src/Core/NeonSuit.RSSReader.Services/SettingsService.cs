using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Preferences;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Events;
using Serilog;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="ISettingsService"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/> as a cache
    /// to minimize database hits on frequently accessed configuration.
    /// Thread-safe implementation using <see cref="SemaphoreSlim"/> for cache synchronization.
    /// </summary>
    internal class SettingsService : ISettingsService
    {
        private readonly IUserPreferencesRepository _repository;
        private readonly IMapper _mapper;
        private readonly ConcurrentDictionary<string, UserPreferences> _cache;
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private readonly ILogger _logger;
        private bool _isCacheInitialized = false;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class.
        /// </summary>
        /// <param name="repository">The user preferences repository.</param>
        /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when repository or logger is null.</exception>
        public SettingsService(
            IUserPreferencesRepository repository,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _repository = repository;
            _mapper = mapper;
            _logger = logger.ForContext<SettingsService>();
            _cache = new ConcurrentDictionary<string, UserPreferences>();

#if DEBUG
            _logger.Debug("SettingsService initialized");
#endif
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<PreferenceChangedEventArgs>? OnPreferenceChanged;

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Internal helper that performs actual cache initialization from the database.
        /// WARNING: Caller MUST hold the _cacheLock before calling this method.
        /// </summary>
        private async Task InitializeCacheUnsafeAsync(CancellationToken cancellationToken = default)
        {
            var allPrefs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

            foreach (var pref in allPrefs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _cache[pref.Key] = pref;
            }

            _isCacheInitialized = true;
            _logger.Debug("Settings cache initialized with {Count} items.", _cache.Count);
        }

        /// <summary>
        /// Ensures the in-memory cache is populated from the database.
        /// Implements a double-check locking pattern for thread safety.
        /// </summary>
        private async Task EnsureCacheInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_isCacheInitialized) return;

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isCacheInitialized) return;

                await InitializeCacheUnsafeAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Cache initialization was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize settings cache.");
                // No rethrow - cache not initialized but operation continues
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Validates and normalizes a key.
        /// </summary>
        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
        }

        /// <summary>
        /// Gets the display name from a preference key.
        /// </summary>
        private static string GetDisplayName(string key)
        {
            // Convert "Cleanup.ArticleRetention.Days" to "Article Retention Days"
            var parts = key.Split('.');
            var lastPart = parts.Last();

            // Insert spaces before capital letters
            return System.Text.RegularExpressions.Regex.Replace(lastPart, "([A-Z])", " $1").Trim();
        }

        /// <summary>
        /// Gets the category from a preference key.
        /// </summary>
        private static string? GetCategory(string key)
        {
            var parts = key.Split('.');
            return parts.Length > 1 ? parts[0] : null;
        }

        /// <summary>
        /// Gets the preference type from a key and value.
        /// </summary>
        private static PreferenceType GetPreferenceType(string key, string value)
        {
            // Try to infer type from key patterns or value
            if (key.Contains("Enable", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Is", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Use", StringComparison.OrdinalIgnoreCase) ||
                bool.TryParse(value, out _))
            {
                return PreferenceType.Boolean;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return key.Contains("Days", StringComparison.OrdinalIgnoreCase) ||
                       key.Contains("Count", StringComparison.OrdinalIgnoreCase) ||
                       key.Contains("Size", StringComparison.OrdinalIgnoreCase)
                    ? PreferenceType.Integer
                    : PreferenceType.Decimal;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                return PreferenceType.Decimal;
            }

            return PreferenceType.String;
        }

        #endregion

        #region Retrieval Methods

        /// <inheritdoc />
        public async Task<string> GetValueAsync(string key, string defaultValue = "", CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);

                if (_cache.TryGetValue(key, out var cachedPref))
                {
                    _logger.Verbose("Cache hit for key: {Key}", key);
                    return cachedPref.Value;
                }

                _logger.Debug("Cache miss for key: {Key}, loading from repository", key);
                var value = await _repository.GetValueAsync(key, defaultValue, cancellationToken).ConfigureAwait(false);

                var pref = await _repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
                if (pref != null)
                    _cache[key] = pref;

                return value;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetValueAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get value for key: {Key}", key);
                throw new InvalidOperationException($"Failed to get value for key: {key}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                var value = await GetValueAsync(key, defaultValue.ToString(), cancellationToken).ConfigureAwait(false);

                // Parse common boolean representations
                return value?.ToLowerInvariant() switch
                {
                    "true" or "yes" or "1" or "on" => true,
                    "false" or "no" or "0" or "off" => false,
                    _ => bool.TryParse(value, out bool result) ? result : defaultValue
                };
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetBoolAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get bool for key: {Key}", key);
                throw new InvalidOperationException($"Failed to get bool for key: {key}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                var value = await GetValueAsync(key, defaultValue.ToString(), cancellationToken).ConfigureAwait(false);

                return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result)
                    ? result
                    : defaultValue;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetIntAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get int for key: {Key}", key);
                throw new InvalidOperationException($"Failed to get int for key: {key}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<double> GetDoubleAsync(string key, double defaultValue = 0.0, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                var value = await GetValueAsync(key, defaultValue.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);

                return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result)
                    ? result
                    : defaultValue;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetDoubleAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get double for key: {Key}", key);
                throw new InvalidOperationException($"Failed to get double for key: {key}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<PreferenceDto?> GetPreferenceAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);

                if (_cache.TryGetValue(key, out var cachedPref))
                {
                    var dto = _mapper.Map<PreferenceDto>(cachedPref);
                    EnrichPreferenceDto(dto);
                    return dto;
                }

                var pref = await _repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
                if (pref == null)
                    return null;

                _cache[key] = pref;
                var resultDto = _mapper.Map<PreferenceDto>(pref);
                EnrichPreferenceDto(resultDto);

                return resultDto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetPreferenceAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preference for key: {Key}", key);
                throw new InvalidOperationException($"Failed to get preference for key: {key}", ex);
            }
        }

        #endregion

        #region Update Methods

        /// <inheritdoc />
        public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                var safeValue = value ?? string.Empty;

                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);

                await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var existingPref = await _repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
                    var shouldRaiseEvent = false;

                    if (existingPref == null)
                    {
                        // New preference - always raise event
                        var newPref = new UserPreferences
                        {
                            Key = key,
                            Value = safeValue,
                            LastModified = DateTime.UtcNow
                        };

                        await _repository.InsertAsync(newPref, cancellationToken).ConfigureAwait(false);
                        _cache[key] = newPref;
                        shouldRaiseEvent = true;
                    }
                    else
                    {
                        // Only raise event if value actually changed
                        if (existingPref.Value != safeValue)
                        {
                            await _repository.SetValueAsync(key, safeValue, cancellationToken).ConfigureAwait(false);
                            shouldRaiseEvent = true;

                            if (_cache.TryGetValue(key, out var cached))
                            {
                                cached.Value = safeValue;
                                cached.LastModified = DateTime.UtcNow;
                            }
                            else
                            {
                                var updated = await _repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
                                if (updated != null) _cache[key] = updated;
                            }
                        }
                        else
                        {
                            _logger.Verbose("Setting unchanged: {Key} = {Value} (no update)", key, safeValue);
                        }
                    }

                    // Raise event only if shouldRaiseEvent is true
                    if (shouldRaiseEvent)
                    {
                        OnPreferenceChanged?.Invoke(this, new PreferenceChangedEventArgs(key, safeValue));
                        _logger.Debug("Setting updated: {Key} = {Value}", key, safeValue);
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("SetValueAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating setting {Key}.", key);
                throw new InvalidOperationException($"Failed to update setting {key}", ex);
            }
        }

        /// <inheritdoc />
        public Task SetBoolAsync(string key, bool value, CancellationToken cancellationToken = default)
            => SetValueAsync(key, value.ToString().ToLowerInvariant(), cancellationToken);

        /// <inheritdoc />
        public Task SetIntAsync(string key, int value, CancellationToken cancellationToken = default)
            => SetValueAsync(key, value.ToString(CultureInfo.InvariantCulture), cancellationToken);

        /// <inheritdoc />
        public Task SetDoubleAsync(string key, double value, CancellationToken cancellationToken = default)
            => SetValueAsync(key, value.ToString("R", CultureInfo.InvariantCulture), cancellationToken);

        /// <inheritdoc />
        public async Task<PreferenceDto> UpdatePreferenceAsync(UpdatePreferenceDto updateDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updateDto);

            try
            {
                ValidateKey(updateDto.Key);

                await SetValueAsync(updateDto.Key, updateDto.Value, cancellationToken).ConfigureAwait(false);

                var pref = await _repository.GetByKeyAsync(updateDto.Key, cancellationToken).ConfigureAwait(false);
                if (pref == null)
                {
                    throw new InvalidOperationException($"Preference {updateDto.Key} not found after update");
                }

                var dto = _mapper.Map<PreferenceDto>(pref);
                EnrichPreferenceDto(dto);

                _logger.Information("Preference updated: {Key} = {Value}", updateDto.Key, updateDto.Value);
                return dto;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdatePreferenceAsync operation was cancelled for key: {Key}", updateDto.Key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update preference: {Key}", updateDto.Key);
                throw new InvalidOperationException($"Failed to update preference: {updateDto.Key}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> UpdatePreferencesBatchAsync(List<UpdatePreferenceDto> updates, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updates);

            if (updates.Count == 0)
                return 0;

            try
            {
                _logger.Debug("Batch updating {Count} preferences", updates.Count);

                var values = updates.ToDictionary(u => u.Key, u => u.Value);
                var updatedCount = await _repository.SetValuesBulkAsync(values, cancellationToken).ConfigureAwait(false);

                // Invalidate cache
                await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (var update in updates)
                    {
                        _cache.TryRemove(update.Key, out _);
                    }
                    _isCacheInitialized = false;
                }
                finally
                {
                    _cacheLock.Release();
                }

                // Raise events for each updated preference
                foreach (var update in updates)
                {
                    OnPreferenceChanged?.Invoke(this, new PreferenceChangedEventArgs(update.Key, update.Value));
                }

                _logger.Information("Batch updated {Count} preferences", updatedCount);
                return updatedCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdatePreferencesBatchAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to batch update preferences");
                throw new InvalidOperationException("Failed to batch update preferences", ex);
            }
        }

        #endregion

        #region Maintenance Methods

        /// <inheritdoc />
        public async Task ResetToDefaultAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateKey(key);

                _logger.Debug("Resetting setting to default: {Key}", key);

                var defaultValue = PreferenceHelper.GetDefaultValue(key);
                await SetValueAsync(key, defaultValue, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ResetToDefaultAsync operation was cancelled for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset setting to default: {Key}", key);
                throw new InvalidOperationException($"Failed to reset setting {key} to default", ex);
            }
        }

        /// <inheritdoc />
        public async Task<int> ResetPreferencesAsync(ResetPreferencesDto resetDto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resetDto);

            try
            {
                var resetCount = 0;

                if (resetDto.ResetAll)
                {
                    await ResetAllToDefaultsAsync(cancellationToken).ConfigureAwait(false);

                    var allPrefs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                    return allPrefs.Count;
                }

                if (resetDto.Keys != null)
                {
                    foreach (var key in resetDto.Keys)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await ResetToDefaultAsync(key, cancellationToken).ConfigureAwait(false);
                        resetCount++;
                    }
                }

                _logger.Information("Reset {Count} preferences to defaults", resetCount);
                return resetCount;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ResetPreferencesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset preferences");
                throw new InvalidOperationException("Failed to reset preferences", ex);
            }
        }

        /// <inheritdoc />
        public async Task ResetAllToDefaultsAsync(CancellationToken cancellationToken = default)
        {
            _logger.Information("Resetting all settings to defaults");

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _repository.DeleteAllAsync(cancellationToken).ConfigureAwait(false);

                _cache.Clear();
                _isCacheInitialized = false;

                _logger.Information("All settings have been reset to default values.");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ResetAllToDefaultsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to reset all settings.");
                throw new InvalidOperationException("Failed to reset all settings", ex);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task ExportToFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("File path cannot be empty", nameof(filePath));

                _logger.Information("Exporting settings to: {Path}", filePath);

                var allPrefs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
                var exportDto = new ExportImportDto
                {
                    Version = "1.0",
                    ExportedAt = DateTime.UtcNow,
                    AppVersion = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0.0",
                    Preferences = allPrefs.ToDictionary(p => p.Key, p => p.Value)
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                string json = JsonSerializer.Serialize(exportDto, options);

                // Write to temporary file first for atomic operation
                var tempFile = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(tempFile, json, cancellationToken).ConfigureAwait(false);
                    File.Move(tempFile, filePath, true);
                }
                catch
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                    throw;
                }

                _logger.Information("Settings successfully exported to {Path}.", filePath);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ExportToFileAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Export failed.");
                throw new InvalidOperationException($"Failed to export settings to {filePath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task ImportFromFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    throw new ArgumentException("File path cannot be empty", nameof(filePath));

                if (!File.Exists(filePath))
                    throw new FileNotFoundException("Import file not found.", filePath);

                _logger.Information("Importing settings from: {Path}", filePath);

                string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

                // Validate JSON structure
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("preferences", out var prefsElement))
                {
                    throw new InvalidDataException("Invalid export format: missing 'preferences' property");
                }

                var exportDto = JsonSerializer.Deserialize<ExportImportDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (exportDto?.Preferences == null)
                    throw new InvalidDataException("Invalid JSON format - deserialization returned null");

                await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    // Validate and filter preferences
                    var validPrefs = new List<UserPreferences>();
                    foreach (var kvp in exportDto.Preferences)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Skip invalid keys/values
                        if (string.IsNullOrWhiteSpace(kvp.Key))
                            continue;

                        validPrefs.Add(new UserPreferences
                        {
                            Key = kvp.Key,
                            Value = kvp.Value ?? string.Empty,
                            LastModified = DateTime.UtcNow
                        });
                    }

                    await _repository.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
                    await _repository.InsertAllAsync(validPrefs, cancellationToken).ConfigureAwait(false);

                    _cache.Clear();
                    _isCacheInitialized = false;
                    await InitializeCacheUnsafeAsync(cancellationToken).ConfigureAwait(false);

                    _logger.Information("Settings successfully imported from {Path}.", filePath);
                }
                finally
                {
                    _cacheLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ImportFromFileAsync operation was cancelled");
                throw;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, "Import failed - invalid JSON format.");
                throw new InvalidDataException("Invalid JSON format in import file", ex);
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not ArgumentException)
            {
                _logger.Error(ex, "Import failed.");
                throw new InvalidOperationException($"Failed to import settings from {filePath}", ex);
            }
        }

        #endregion

        #region Data Organization

        /// <inheritdoc />
        public async Task<List<PreferenceCategoryDto>> GetAllCategorizedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all settings categorized");

                var categorized = await _repository.GetAllCategorizedAsync(cancellationToken).ConfigureAwait(false);
                var result = new List<PreferenceCategoryDto>();

                foreach (var category in categorized.OrderBy(c => c.Key))
                {
                    var categoryDto = new PreferenceCategoryDto
                    {
                        Name = category.Key,
                        Preferences = _mapper.Map<List<PreferenceSummaryDto>>(category.Value)
                    };

                    // Enrich with display names
                    foreach (var pref in categoryDto.Preferences)
                    {
                        pref.DisplayName = GetDisplayName(pref.Key);
                        pref.Category = category.Key;
                    }

                    result.Add(categoryDto);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllCategorizedAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get categorized settings");
                throw new InvalidOperationException("Failed to get categorized settings", ex);
            }
        }

        /// <inheritdoc />
        public async Task<List<PreferenceSummaryDto>> GetAllSummariesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving all preference summaries");

                await EnsureCacheInitializedAsync(cancellationToken).ConfigureAwait(false);
                var summaries = _mapper.Map<List<PreferenceSummaryDto>>(_cache.Values.ToList());

                foreach (var summary in summaries)
                {
                    summary.DisplayName = GetDisplayName(summary.Key);
                    summary.Category = GetCategory(summary.Key);
                }

                return summaries.OrderBy(s => s.Category).ThenBy(s => s.Key).ToList();
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetAllSummariesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get preference summaries");
                throw new InvalidOperationException("Failed to get preference summaries", ex);
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Enriches a preference DTO with calculated properties.
        /// </summary>
        private void EnrichPreferenceDto(PreferenceDto dto)
        {
            dto.DisplayName = GetDisplayName(dto.Key);
            dto.Category = GetCategory(dto.Key);
            dto.Type = GetPreferenceType(dto.Key, dto.Value);

            // Set typed values
            switch (dto.Type)
            {
                case PreferenceType.Boolean:
                    dto.BoolValue = bool.TryParse(dto.Value, out bool b) && b;
                    break;
                case PreferenceType.Integer:
                    dto.IntValue = int.TryParse(dto.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int i) ? i : 0;
                    break;
                case PreferenceType.Decimal:
                    dto.DoubleValue = double.TryParse(dto.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
                    break;
            }
        }

        #endregion
    }
}