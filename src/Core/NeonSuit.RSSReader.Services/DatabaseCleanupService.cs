using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using Serilog;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IDatabaseCleanupService"/> that orchestrates 
    /// database maintenance operations. Manages business rules, configuration,
    /// and coordinates between multiple repositories for comprehensive cleanup workflows.
    /// </summary>
    public class DatabaseCleanupService : IDatabaseCleanupService
    {
        private readonly IDatabaseCleanupRepository _cleanupRepository;
        private readonly ISettingsService _settingsService;
        private readonly ILogger _logger;
        private CleanupConfiguration _configuration;

        /// <inheritdoc />
        public event EventHandler<CleanupStartingEventArgs>? OnCleanupStarting;

        /// <inheritdoc />
        public event EventHandler<CleanupProgressEventArgs>? OnCleanupProgress;

        /// <inheritdoc />
        public event EventHandler<CleanupCompletedEventArgs>? OnCleanupCompleted;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseCleanupService"/> class.
        /// </summary>
        /// <param name="cleanupRepository">Repository for database cleanup operations.</param>
        /// <param name="settingsService">Service for accessing application settings.</param>
        /// <param name="logger">Logger instance for diagnostic logging.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
        public DatabaseCleanupService(
            IDatabaseCleanupRepository cleanupRepository,
            ISettingsService settingsService,
            ILogger logger)
        {
            _cleanupRepository = cleanupRepository ?? throw new ArgumentNullException(nameof(cleanupRepository));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = logger.ForContext<DatabaseCleanupService>();
            _configuration = new CleanupConfiguration();
        }

        /// <inheritdoc />
        public async Task<CleanupResult> PerformCleanupAsync(CancellationToken cancellationToken = default)
        {
            var result = new CleanupResult();
            var startTime = DateTime.UtcNow;

            try
            {
                await LoadConfigurationAsync(cancellationToken);

                if (!_configuration.AutoCleanupEnabled)
                {
                    _logger.Information("Automatic cleanup is disabled in configuration. Skipping cleanup cycle.");
                    result.Success = true;
                    result.Skipped = true;
                    return result;
                }

                // Raise starting event
                OnCleanupStarting?.Invoke(this, new CleanupStartingEventArgs(_configuration, startTime));

                _logger.Information(
                    "Starting database cleanup cycle. Retention: {RetentionDays} days, KeepFavorites: {KeepFav}, KeepUnread: {KeepUnread}",
                    _configuration.ArticleRetentionDays,
                    _configuration.KeepFavorites,
                    _configuration.KeepUnread);

                // Step 1: Article cleanup
                if (_configuration.ArticleRetentionDays > 0)
                {
                    OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs("Cleaning up old articles", 1, 5));

                    var cutoffDate = DateTime.UtcNow.AddDays(-_configuration.ArticleRetentionDays);
                    result.ArticleCleanup = await _cleanupRepository.DeleteOldArticlesAsync(
                        cutoffDate,
                        _configuration.KeepFavorites,
                        _configuration.KeepUnread,
                        cancellationToken);

                    // Update tag counts if articles were deleted
                    if (result.ArticleCleanup.ArticlesDeleted > 0)
                    {
                        await _cleanupRepository.UpdateTagUsageCountsAsync(cancellationToken);
                    }
                }

                // Step 2: Orphan cleanup
                OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs("Removing orphaned records", 2, 5));
                result.OrphanCleanup = await _cleanupRepository.RemoveOrphanedRecordsAsync(cancellationToken);

                // Step 3: Vacuum
                if (_configuration.VacuumAfterCleanup)
                {
                    OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs("Vacuuming database", 3, 5));
                    var vacuumResult = await _cleanupRepository.VacuumDatabaseAsync(cancellationToken);
                    result.SpaceFreedBytes += vacuumResult.SpaceFreedBytes;
                }

                // Step 4: Index rebuild
                if (_configuration.RebuildIndexesAfterCleanup)
                {
                    OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs("Rebuilding indexes", 4, 5));
                    await _cleanupRepository.RebuildIndexesAsync(null, cancellationToken);
                }

                // Step 5: Update statistics
                OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs("Updating statistics", 5, 5));
                await _cleanupRepository.UpdateStatisticsAsync(cancellationToken);

                result.Success = true;
                _logger.Information(
                    "Cleanup cycle completed successfully. Duration: {Duration}, Articles deleted: {ArticlesDeleted}, Space freed: {SpaceFreedMB:F2} MB",
                    result.Duration,
                    result.ArticleCleanup?.ArticlesDeleted ?? 0,
                    result.SpaceFreedBytes / (1024.0 * 1024.0));
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Cleanup operation was cancelled by user request");
                result.Errors.Add("Operation was cancelled");
                result.Success = false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Cleanup cycle failed with exception");
                result.Errors.Add($"Cleanup failed: {ex.Message}");
                result.Success = false;
            }
            finally
            {
                result.Duration = DateTime.UtcNow - startTime;
                result.PerformedAt = DateTime.UtcNow;

                OnCleanupCompleted?.Invoke(this, new CleanupCompletedEventArgs(result, DateTime.UtcNow));
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<CleanupAnalysis> AnalyzeCleanupAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            await LoadConfigurationAsync(cancellationToken);

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            _logger.Debug("Analyzing cleanup impact for retention period of {RetentionDays} days", retentionDays);

            var analysis = await _cleanupRepository.AnalyzeCleanupImpactAsync(
                cutoffDate,
                _configuration.KeepFavorites,
                _configuration.KeepUnread,
                cancellationToken);

            // Map repository result to service model (could be different if needed)
            return new CleanupAnalysis
            {
                RetentionDays = retentionDays,
                CutoffDate = cutoffDate,
                ArticlesToDelete = analysis.ArticlesToDelete,
                ArticlesToKeep = analysis.ArticlesToKeep,
                WouldKeepFavorites = analysis.WouldKeepFavorites,
                WouldKeepUnread = analysis.WouldKeepUnread,
                EstimatedSpaceFreedBytes = analysis.EstimatedSpaceFreedBytes,
                ArticlesByFeed = analysis.ArticlesByFeed
            };
        }

        /// <inheritdoc />
        public async Task<CleanupConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            await LoadConfigurationAsync(cancellationToken);
            return _configuration.Clone(); // Return a copy to prevent external modification
        }

        /// <inheritdoc />
        public async Task UpdateConfigurationAsync(CleanupConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            try
            {
                await _settingsService.SetIntAsync(
                    PreferenceKeys.ArticleRetentionDays,
                    configuration.ArticleRetentionDays);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.KeepFavoriteArticles,
                    configuration.KeepFavorites);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.AutoCleanupEnabled,
                    configuration.AutoCleanupEnabled);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.KeepUnreadArticles,
                    configuration.KeepUnread);


                await _settingsService.SetIntAsync(
                    "image_cache_max_size_mb",
                    configuration.MaxImageCacheSizeMB);

                await _settingsService.SetIntAsync(
                    "cleanup_hour_of_day",
                    configuration.CleanupHourOfDay);

                await _settingsService.SetIntAsync(
                    "cleanup_day_of_week",
                    (int)configuration.CleanupDayOfWeek);

                await _settingsService.SetBoolAsync(
                    "vacuum_after_cleanup",
                    configuration.VacuumAfterCleanup);

                await _settingsService.SetBoolAsync(
                    "rebuild_indexes_after_cleanup",
                    configuration.RebuildIndexesAfterCleanup);

                _configuration = configuration;
                _logger.Information("Cleanup configuration updated successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save cleanup configuration");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<ImageCacheCleanupResult> CleanupImageCacheAsync(
            int maxAgeDays = 30,
            int maxSizeMB = 500,
            CancellationToken cancellationToken = default)
        {
            // Verificar cancelación INMEDIATAMENTE al entrar al método
            cancellationToken.ThrowIfCancellationRequested();

            var result = new ImageCacheCleanupResult();

            try
            {
                _logger.Debug(
                    "Starting image cache cleanup. Max age: {MaxAgeDays} days, Max size: {MaxSizeMB} MB",
                    maxAgeDays,
                    maxSizeMB);

                var cacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NeonSuit",
                    "ImageCache");

                if (!Directory.Exists(cacheDirectory))
                {
                    _logger.Debug("Image cache directory does not exist at: {Path}", cacheDirectory);
                    return result;
                }

                // Verificar cancelación después de validar directorio pero antes de operaciones de archivo
                cancellationToken.ThrowIfCancellationRequested();

                var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
                var maxSizeBytes = (long)maxSizeMB * 1024 * 1024;

                var imageFiles = Directory.GetFiles(cacheDirectory, "*.*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f))
                    .ToList();

                result.ImagesBeforeCleanup = imageFiles.Count;
                result.CacheSizeBeforeBytes = imageFiles.Sum(f => f.Length);

                // Phase 1: Delete files older than maxAgeDays
                var oldFiles = imageFiles.Where(f => f.LastWriteTimeUtc < cutoffDate).ToList();
                foreach (var file in oldFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        file.Delete();
                        result.ImagesDeleted++;
                        result.SpaceFreedBytes += file.Length;
                        _logger.Debug("Deleted old cache file: {FileName}", file.Name);
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to delete {file.Name}: {ex.Message}");
                        _logger.Warning(ex, "Failed to delete cache file: {Path}", file.FullName);
                    }
                }

                // Phase 2: If still over size limit, delete oldest files first
                var currentSize = result.CacheSizeBeforeBytes - result.SpaceFreedBytes;
                if (currentSize > maxSizeBytes)
                {
                    var remainingFiles = imageFiles.Except(oldFiles)
                        .OrderBy(f => f.LastWriteTimeUtc)
                        .ToList();

                    foreach (var file in remainingFiles)
                    {
                        if (currentSize <= maxSizeBytes)
                            break;

                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            file.Delete();
                            result.ImagesDeleted++;
                            result.SpaceFreedBytes += file.Length;
                            currentSize -= file.Length;
                            _logger.Debug("Deleted cache file for size constraint: {FileName}", file.Name);
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"Failed to delete {file.Name}: {ex.Message}");
                            _logger.Warning(ex, "Failed to delete cache file: {Path}", file.FullName);
                        }
                    }
                }

                // Calculate final statistics
                var remainingImages = Directory.GetFiles(cacheDirectory, "*.*", SearchOption.AllDirectories);
                result.ImagesRemaining = remainingImages.Length;
                result.CacheSizeAfterBytes = result.CacheSizeBeforeBytes - result.SpaceFreedBytes;

                _logger.Information(
                    "Image cache cleanup completed. Deleted: {Deleted} images, Freed: {FreedMB:F2} MB, Remaining: {Remaining} images",
                    result.ImagesDeleted,
                    result.SpaceFreedBytes / (1024.0 * 1024.0),
                    result.ImagesRemaining);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Image cache cleanup was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Image cache cleanup failed");
                throw;
            }
        }
        /// <inheritdoc />
        public async Task<DatabaseStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return await _cleanupRepository.GetStatisticsAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IntegrityCheckResult> CheckIntegrityAsync(CancellationToken cancellationToken = default)
        {
            return await _cleanupRepository.CheckIntegrityAsync(cancellationToken);
        }

        /// <summary>
        /// Loads configuration from settings service.
        /// </summary>
        private async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _configuration = new CleanupConfiguration
                {
                    ArticleRetentionDays = await _settingsService.GetIntAsync(
                        PreferenceKeys.ArticleRetentionDays,
                        PreferenceDefaults.ArticleRetentionDays),                  

                    AutoCleanupEnabled = await _settingsService.GetBoolAsync(
                        PreferenceKeys.AutoCleanupEnabled,
                        PreferenceDefaults.AutoCleanupEnabled),

                    KeepFavorites = await _settingsService.GetBoolAsync(
                        PreferenceKeys.KeepFavoriteArticles,
                        PreferenceDefaults.KeepFavoriteArticles),

                    KeepUnread = await _settingsService.GetBoolAsync(
                        PreferenceKeys.KeepUnreadArticles,
                        PreferenceDefaults.KeepUnreadArticles),

                    MaxImageCacheSizeMB = await _settingsService.GetIntAsync(
                        "image_cache_max_size_mb",
                        500),

                    CleanupHourOfDay = await _settingsService.GetIntAsync(
                        "cleanup_hour_of_day",
                        2),

                    CleanupDayOfWeek = (DayOfWeek)await _settingsService.GetIntAsync(
                        "cleanup_day_of_week",
                        (int)DayOfWeek.Sunday),

                    VacuumAfterCleanup = await _settingsService.GetBoolAsync(
                        "vacuum_after_cleanup",
                        true),

                    RebuildIndexesAfterCleanup = await _settingsService.GetBoolAsync(
                        "rebuild_indexes_after_cleanup",
                        false)
                };

                _logger.Debug("Cleanup configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load cleanup configuration. Using default values.");
                _configuration = new CleanupConfiguration(); // Use defaults
            }
        }
    }
}