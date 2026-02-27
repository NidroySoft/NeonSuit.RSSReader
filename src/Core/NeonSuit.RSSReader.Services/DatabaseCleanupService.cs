using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Cleanup;
using NeonSuit.RSSReader.Core.DTOs.System;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Core.Models.Cleanup;
using NeonSuit.RSSReader.Core.Models.Events;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IDatabaseCleanupService"/> that orchestrates 
    /// database maintenance operations. Manages business rules, configuration,
    /// and coordinates between multiple repositories for comprehensive cleanup workflows.
    /// </summary>
    internal class DatabaseCleanupService : IDatabaseCleanupService
    {
        private readonly IDatabaseCleanupRepository _cleanupRepository;
        private readonly ISettingsService _settingsService;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private CleanupConfiguration _configuration;

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseCleanupService"/> class.
        /// </summary>
        /// <param name="cleanupRepository">Repository for database cleanup operations.</param>
        /// <param name="settingsService">Service for accessing application settings.</param>
        /// <param name="mapper">AutoMapper instance for entity-DTO transformations.</param>
        /// <param name="logger">Logger instance for diagnostic logging.</param>
        /// <exception cref="ArgumentNullException">Thrown when any dependency is null.</exception>
        public DatabaseCleanupService(
            IDatabaseCleanupRepository cleanupRepository,
            ISettingsService settingsService,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(cleanupRepository);
            ArgumentNullException.ThrowIfNull(settingsService);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _cleanupRepository = cleanupRepository;
            _settingsService = settingsService;
            _mapper = mapper;
            _logger = logger.ForContext<DatabaseCleanupService>();
            _configuration = new CleanupConfiguration();

#if DEBUG
            _logger.Debug("DatabaseCleanupService initialized");
#endif
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<CleanupStartingEventArgs>? OnCleanupStarting;

        /// <inheritdoc />
        public event EventHandler<CleanupProgressEventArgs>? OnCleanupProgress;

        /// <inheritdoc />
        public event EventHandler<CleanupCompletedEventArgs>? OnCleanupCompleted;

        #endregion

        #region Core Cleanup Operations

        /// <inheritdoc />
        public async Task<CleanupResultDto> PerformCleanupAsync(CancellationToken cancellationToken = default)
        {
            var result = new CleanupResult();
            var startTime = DateTime.UtcNow;

            try
            {
                await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

                if (!_configuration.AutoCleanupEnabled)
                {
                    _logger.Information("Automatic cleanup is disabled in configuration. Skipping cleanup cycle.");
                    result.Success = true;
                    result.Skipped = true;
                    return _mapper.Map<CleanupResultDto>(result);
                }

                // Raise starting event
                OnCleanupStarting?.Invoke(this, new CleanupStartingEventArgs(
                    _mapper.Map<CleanupConfigurationDto>(_configuration),
                    startTime));

                _logger.Information(
                    "Starting database cleanup cycle. Retention: {RetentionDays} days, KeepFavorites: {KeepFav}, KeepUnread: {KeepUnread}",
                    _configuration.ArticleRetentionDays,
                    _configuration.KeepFavorites,
                    _configuration.KeepUnread);

                // Step 1: Article cleanup
                if (_configuration.ArticleRetentionDays > 0)
                {
                    ReportProgress("Cleaning up old articles", 1, 5);

                    var cutoffDate = DateTime.UtcNow.AddDays(-_configuration.ArticleRetentionDays);
                    result.ArticleCleanup = await _cleanupRepository.DeleteOldArticlesAsync(
                        cutoffDate,
                        _configuration.KeepFavorites,
                        _configuration.KeepUnread,
                        cancellationToken).ConfigureAwait(false);

                    // Update tag counts if articles were deleted
                    if (result.ArticleCleanup?.ArticlesDeleted > 0)
                    {
                        await _cleanupRepository.UpdateTagUsageCountsAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                // Step 2: Orphan cleanup
                ReportProgress("Removing orphaned records", 2, 5);
                result.OrphanCleanup = await _cleanupRepository.RemoveOrphanedRecordsAsync(cancellationToken).ConfigureAwait(false);

                // Step 3: Vacuum
                if (_configuration.VacuumAfterCleanup)
                {
                    ReportProgress("Vacuuming database", 3, 5);
                    var vacuumResult = await _cleanupRepository.VacuumDatabaseAsync(cancellationToken).ConfigureAwait(false);
                    result.SpaceFreedBytes += vacuumResult.SpaceFreedBytes;
                }

                // Step 4: Index rebuild
                if (_configuration.RebuildIndexesAfterCleanup)
                {
                    ReportProgress("Rebuilding indexes", 4, 5);
                    await _cleanupRepository.RebuildIndexesAsync(null, cancellationToken).ConfigureAwait(false);
                }

                // Step 5: Update statistics
                ReportProgress("Updating statistics", 5, 5);
                await _cleanupRepository.UpdateStatisticsAsync(cancellationToken).ConfigureAwait(false);

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
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Cleanup cycle failed with exception");
                result.Errors.Add($"Cleanup failed: {ex.Message}");
                result.Success = false;
                throw;
            }
            finally
            {
                result.Duration = DateTime.UtcNow - startTime;
                result.PerformedAt = DateTime.UtcNow;

                var resultDto = _mapper.Map<CleanupResultDto>(result);
                OnCleanupCompleted?.Invoke(this, new CleanupCompletedEventArgs(resultDto, DateTime.UtcNow));
            }

            return _mapper.Map<CleanupResultDto>(result);
        }

        /// <inheritdoc />
        public async Task<CleanupAnalysisDto> AnalyzeCleanupAsync(int retentionDays, CancellationToken cancellationToken = default)
        {
            try
            {
                if (retentionDays < 1)
                {
                    _logger.Warning("Invalid retention days provided for analysis: {RetentionDays}", retentionDays);
                    throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be at least 1");
                }

                await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

                _logger.Debug("Analyzing cleanup impact for retention period of {RetentionDays} days", retentionDays);

                var analysis = await _cleanupRepository.AnalyzeCleanupImpactAsync(
                    cutoffDate,
                    _configuration.KeepFavorites,
                    _configuration.KeepUnread,
                    cancellationToken).ConfigureAwait(false);

                return _mapper.Map<CleanupAnalysisDto>(analysis, opts =>
                {
                    opts.Items["RetentionDays"] = retentionDays;
                    opts.Items["CutoffDate"] = cutoffDate;
                });
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("AnalyzeCleanupAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to analyze cleanup impact for retention: {RetentionDays}", retentionDays);
                throw;
            }
        }

        #endregion

        #region Configuration Management

        /// <inheritdoc />
        public async Task<CleanupConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);
                return _mapper.Map<CleanupConfigurationDto>(_configuration);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetConfigurationAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get cleanup configuration");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateConfigurationAsync(CleanupConfigurationDto configuration, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            try
            {
                _logger.Information("Updating cleanup configuration");

                // Validate configuration values
                ValidateConfiguration(configuration);

                // Save to settings service
                await _settingsService.SetIntAsync(
                    PreferenceKeys.ArticleRetentionDays,
                    configuration.ArticleRetentionDays, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.KeepFavoriteArticles,
                    configuration.KeepFavorites, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.AutoCleanupEnabled,
                    configuration.AutoCleanupEnabled, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.KeepUnreadArticles,
                    configuration.KeepUnread, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetIntAsync(
                    PreferenceKeys.MaxImageCacheSizeMB,
                    configuration.MaxImageCacheSizeMB, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetIntAsync(
                    PreferenceKeys.CleanupHourOfDay,
                    configuration.CleanupHourOfDay, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetIntAsync(
                    PreferenceKeys.CleanupDayOfWeek,
                    (int)configuration.CleanupDayOfWeek, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.VacuumAfterCleanup,
                    configuration.VacuumAfterCleanup, cancellationToken).ConfigureAwait(false);

                await _settingsService.SetBoolAsync(
                    PreferenceKeys.RebuildIndexesAfterCleanup,
                    configuration.RebuildIndexesAfterCleanup, cancellationToken).ConfigureAwait(false);

                // Reload configuration to ensure consistency
                await LoadConfigurationAsync(cancellationToken).ConfigureAwait(false);

                _logger.Information("Cleanup configuration updated successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("UpdateConfigurationAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save cleanup configuration");
                throw;
            }
        }

        #endregion

        #region Specialized Cleanup Operations

        /// <inheritdoc />
        public async Task<ImageCacheCleanupResultDto> CleanupImageCacheAsync(
            int maxAgeDays = 30,
            int maxSizeMB = 500,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new ImageCacheCleanupResult();

            try
            {
                if (maxAgeDays < 1)
                {
                    _logger.Warning("Invalid max age days provided: {MaxAgeDays}", maxAgeDays);
                    throw new ArgumentOutOfRangeException(nameof(maxAgeDays), "Max age days must be at least 1");
                }

                if (maxSizeMB < 50)
                {
                    _logger.Warning("Invalid max size MB provided: {MaxSizeMB}", maxSizeMB);
                    throw new ArgumentOutOfRangeException(nameof(maxSizeMB), "Max cache size must be at least 50 MB");
                }

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
                    return _mapper.Map<ImageCacheCleanupResultDto>(result);
                }


                var cutoffDate = DateTime.UtcNow.AddDays(-maxAgeDays);
                var maxSizeBytes = (long)maxSizeMB * 1024 * 1024;

                cancellationToken.ThrowIfCancellationRequested();
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
                        _logger.Verbose("Deleted old cache file: {FileName}", file.Name);
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
                            _logger.Verbose("Deleted cache file for size constraint: {FileName}", file.Name);
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add($"Failed to delete {file.Name}: {ex.Message}");
                            _logger.Warning(ex, "Failed to delete cache file: {Path}", file.FullName);
                        }
                    }
                }

                // Calculate final statistics
                if (Directory.Exists(cacheDirectory))
                {
                    var remainingImages = Directory.GetFiles(cacheDirectory, "*.*", SearchOption.AllDirectories);
                    result.ImagesRemaining = remainingImages.Length;
                }
                result.CacheSizeAfterBytes = result.CacheSizeBeforeBytes - result.SpaceFreedBytes;

                _logger.Information(
                    "Image cache cleanup completed. Deleted: {Deleted} images, Freed: {FreedMB:F2} MB, Remaining: {Remaining} images",
                    result.ImagesDeleted,
                    result.SpaceFreedBytes / (1024.0 * 1024.0),
                    result.ImagesRemaining);

                return _mapper.Map<ImageCacheCleanupResultDto>(result);
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

        #endregion

        #region Monitoring and Diagnostics

        /// <inheritdoc />
        public async Task<DatabaseStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving database statistics");

                var statistics = await _cleanupRepository.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

                _logger.Debug("Database statistics retrieved successfully");
                return _mapper.Map<DatabaseStatisticsDto>(statistics);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("GetStatisticsAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve database statistics");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IntegrityCheckResultDto> CheckIntegrityAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Information("Starting database integrity check");

                var result = await _cleanupRepository.CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);

                if (result.Errors.Any())
                {
                    _logger.Error("Database integrity check failed: {Errors}", string.Join(", ", result.Errors));
                }
                else
                {
                    _logger.Information("Database integrity check passed successfully");
                }

                return _mapper.Map<IntegrityCheckResultDto>(result);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("CheckIntegrityAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database integrity check failed with exception");
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Loads configuration from settings service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _configuration = new CleanupConfiguration
                {
                    ArticleRetentionDays = await _settingsService.GetIntAsync(
                        PreferenceKeys.ArticleRetentionDays,
                        PreferenceDefaults.ArticleRetentionDays, cancellationToken).ConfigureAwait(false),

                    AutoCleanupEnabled = await _settingsService.GetBoolAsync(
                        PreferenceKeys.AutoCleanupEnabled,
                        PreferenceDefaults.AutoCleanupEnabled, cancellationToken).ConfigureAwait(false),

                    KeepFavorites = await _settingsService.GetBoolAsync(
                        PreferenceKeys.KeepFavoriteArticles,
                        PreferenceDefaults.KeepFavoriteArticles, cancellationToken).ConfigureAwait(false),

                    KeepUnread = await _settingsService.GetBoolAsync(
                        PreferenceKeys.KeepUnreadArticles,
                        PreferenceDefaults.KeepUnreadArticles, cancellationToken).ConfigureAwait(false),

                    MaxImageCacheSizeMB = await _settingsService.GetIntAsync(
                        PreferenceKeys.MaxImageCacheSizeMB,
                        PreferenceDefaults.MaxImageCacheSizeMB, cancellationToken).ConfigureAwait(false),

                    CleanupHourOfDay = await _settingsService.GetIntAsync(
                        PreferenceKeys.CleanupHourOfDay,
                        PreferenceDefaults.CleanupHourOfDay, cancellationToken).ConfigureAwait(false),

                    CleanupDayOfWeek = (DayOfWeek)await _settingsService.GetIntAsync(
                        PreferenceKeys.CleanupDayOfWeek,
                        (int)PreferenceDefaults.CleanupDayOfWeek, cancellationToken).ConfigureAwait(false),

                    VacuumAfterCleanup = await _settingsService.GetBoolAsync(
                        PreferenceKeys.VacuumAfterCleanup,
                        PreferenceDefaults.VacuumAfterCleanup, cancellationToken).ConfigureAwait(false),

                    RebuildIndexesAfterCleanup = await _settingsService.GetBoolAsync(
                        PreferenceKeys.RebuildIndexesAfterCleanup,
                        PreferenceDefaults.RebuildIndexesAfterCleanup, cancellationToken).ConfigureAwait(false)
                };

                _logger.Debug("Cleanup configuration loaded successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("LoadConfigurationAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load cleanup configuration. Using default values.");
                _configuration = new CleanupConfiguration(); // Use defaults
            }
        }

        /// <summary>
        /// Validates cleanup configuration values.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
        private static void ValidateConfiguration(CleanupConfigurationDto configuration)
        {
            var errors = new List<string>();

            if (configuration.ArticleRetentionDays is < 1 or > 365)
                errors.Add("Article retention days must be between 1 and 365");

            if (configuration.MaxImageCacheSizeMB < 50)
                errors.Add("Max image cache size must be at least 50 MB");

            if (configuration.CleanupHourOfDay is < 0 or > 23)
                errors.Add("Cleanup hour of day must be between 0 and 23");

            if (configuration.CleanupDayOfWeek is < DayOfWeek.Sunday or > DayOfWeek.Saturday)
                errors.Add("Cleanup day of week is invalid");

            if (errors.Any())
            {
                throw new ArgumentException($"Invalid configuration: {string.Join("; ", errors)}");
            }
        }

        /// <summary>
        /// Reports progress of the cleanup operation.
        /// </summary>
        private void ReportProgress(string message, int currentStep, int totalSteps)
        {
            OnCleanupProgress?.Invoke(this, new CleanupProgressEventArgs(message, currentStep, totalSteps));
        }

        #endregion
    }
}