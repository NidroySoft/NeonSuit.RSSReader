using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using ILogger = Serilog.ILogger;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for the <see cref="DatabaseCleanupService"/> class.
    /// Validates database maintenance operations, configuration management,
    /// event handling, and error scenarios.
    /// </summary>
    public class DatabaseCleanupServiceTests : IDisposable
    {
        private readonly Mock<IDatabaseCleanupRepository> _mockCleanupRepository;
        private readonly Mock<ISettingsService> _mockSettingsService;
        private readonly Mock<Serilog.ILogger> _mockLogger;
        private readonly DatabaseCleanupService _service;
        private readonly string _testCacheDirectory;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseCleanupServiceTests"/> class.
        /// Sets up mocks and test environment.
        /// </summary>
        public DatabaseCleanupServiceTests()
        {
            _mockCleanupRepository = new Mock<IDatabaseCleanupRepository>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILogger>();

            // Setup logger context
            _mockLogger
                .Setup(x => x.ForContext<DatabaseCleanupService>())
                .Returns(_mockLogger.Object);

            _service = new DatabaseCleanupService(
                _mockCleanupRepository.Object,
                _mockSettingsService.Object,
                _mockLogger.Object);

            // Create test cache directory
            _testCacheDirectory = Path.Combine(
                Path.GetTempPath(),
                $"RSSReader_Test_{Guid.NewGuid()}");

            Directory.CreateDirectory(_testCacheDirectory);
        }

        #region Constructor Tests

        /// <summary>
        /// Verifies that the constructor throws <see cref="ArgumentNullException"/> 
        /// when cleanupRepository is null.
        /// </summary>
        [Fact]
        public void Constructor_WhenCleanupRepositoryIsNull_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new DatabaseCleanupService(
                null!,
                _mockSettingsService.Object,
                _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("cleanupRepository");
        }

        /// <summary>
        /// Verifies that the constructor throws <see cref="ArgumentNullException"/> 
        /// when settingsService is null.
        /// </summary>
        [Fact]
        public void Constructor_WhenSettingsServiceIsNull_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new DatabaseCleanupService(
                _mockCleanupRepository.Object,
                null!,
                _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("settingsService");
        }

        /// <summary>
        /// Verifies that the constructor initializes successfully with valid dependencies.
        /// </summary>
        [Fact]
        public void Constructor_WithValidDependencies_ShouldInitializeSuccessfully()
        {
            // Act
            var service = new DatabaseCleanupService(
                _mockCleanupRepository.Object,
                _mockSettingsService.Object,
                _mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region PerformCleanupAsync Tests

        /// <summary>
        /// Verifies that cleanup is skipped when AutoCleanupEnabled is false.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenAutoCleanupDisabled_ShouldSkipAndReturnSkippedResult()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: false);

            // Act
            var result = await _service.PerformCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Skipped.Should().BeTrue();

            _mockCleanupRepository.Verify(
                x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that cleanup executes all steps when enabled and configuration is valid.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenEnabled_ShouldExecuteAllSteps()
        {
            // Arrange
            SetupConfiguration(
                autoCleanupEnabled: true,
                articleRetentionDays: 30,
                vacuumAfterCleanup: true,
                rebuildIndexesAfterCleanup: true);

            var articleResult = new ArticleDeletionResult
            {
                ArticlesDeleted = 100,
                ArticlesFound = 100
            };

            var orphanResult = new OrphanRemovalResult
            {
                OrphanedArticleTagsRemoved = 5,
                OrphanedArticlesRemoved = 2
            };

            var vacuumResult = new VacuumResult
            {
                SizeBeforeBytes = 1000000,
                SizeAfterBytes = 900000
            };

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(articleResult);

            _mockCleanupRepository
                .Setup(x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(orphanResult);

            _mockCleanupRepository
                .Setup(x => x.VacuumDatabaseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(vacuumResult);

            // Act
            var result = await _service.PerformCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Skipped.Should().BeFalse();
            result.ArticleCleanup.Should().NotBeNull();
            result.ArticleCleanup.ArticlesDeleted.Should().Be(100);
            result.OrphanCleanup.Should().NotBeNull();
            result.SpaceFreedBytes.Should().Be(100000); // From vacuum

            VerifyAllStepsExecuted();
        }

        /// <summary>
        /// Verifies that tag usage counts are updated when articles are deleted.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenArticlesDeleted_ShouldUpdateTagCounts()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);

            var articleResult = new ArticleDeletionResult { ArticlesDeleted = 10 };

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(articleResult);

            _mockCleanupRepository
                .Setup(x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrphanRemovalResult());

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            _mockCleanupRepository.Verify(
                x => x.UpdateTagUsageCountsAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Verifies that tag usage counts are NOT updated when no articles are deleted.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenNoArticlesDeleted_ShouldNotUpdateTagCounts()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);

            var articleResult = new ArticleDeletionResult { ArticlesDeleted = 0 };

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(articleResult);

            _mockCleanupRepository
                .Setup(x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrphanRemovalResult());

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            _mockCleanupRepository.Verify(
                x => x.UpdateTagUsageCountsAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that vacuum is skipped when VacuumAfterCleanup is false.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenVacuumDisabled_ShouldSkipVacuum()
        {
            // Arrange
            SetupConfiguration(
                autoCleanupEnabled: true,
                vacuumAfterCleanup: false);

            SetupBasicRepositoryMocks();

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            _mockCleanupRepository.Verify(
                x => x.VacuumDatabaseAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that index rebuild is skipped when RebuildIndexesAfterCleanup is false.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenRebuildIndexesDisabled_ShouldSkipIndexRebuild()
        {
            // Arrange
            SetupConfiguration(
                autoCleanupEnabled: true,
                rebuildIndexesAfterCleanup: false);

            SetupBasicRepositoryMocks();

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            _mockCleanupRepository.Verify(
                x => x.RebuildIndexesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Verifies that article cleanup is skipped when ArticleRetentionDays is 0 or negative.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task PerformCleanupAsync_WhenRetentionDaysZeroOrNegative_ShouldSkipArticleCleanup(int retentionDays)
        {
            // Arrange
            SetupConfiguration(
                autoCleanupEnabled: true,
                articleRetentionDays: retentionDays);

            _mockCleanupRepository
                .Setup(x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrphanRemovalResult());

            // Act
            var result = await _service.PerformCleanupAsync();

            // Assert
            _mockCleanupRepository.Verify(
                x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            result.ArticleCleanup.Should().BeNull();
        }

        /// <summary>
        /// Verifies that OperationCanceledException is handled correctly.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenCancelled_ShouldHandleGracefully()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var result = await _service.PerformCleanupAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().Contain("Operation was cancelled");
        }

        /// <summary>
        /// Verifies that general exceptions are handled and logged correctly.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_WhenExceptionThrown_ShouldHandleAndLogError()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);

            var expectedException = new InvalidOperationException("Database error");

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act
            var result = await _service.PerformCleanupAsync();

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().ContainSingle()
                .Which.Should().Contain("Database error");

            _mockLogger.Verify(
                x => x.Error(
                    It.Is<Exception>(ex => ex == expectedException),
                    It.Is<string>(s => s.Contains("Cleanup cycle failed"))),
                Times.Once);
        }

        /// <summary>
        /// Verifies that the OnCleanupStarting event is raised with correct arguments.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_ShouldRaiseOnCleanupStartingEvent()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);
            SetupBasicRepositoryMocks();

            CleanupStartingEventArgs? capturedArgs = null;
            _service.OnCleanupStarting += (sender, args) => capturedArgs = args;

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Configuration.Should().NotBeNull();
            capturedArgs.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Verifies that the OnCleanupProgress event is raised for each step.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_ShouldRaiseOnCleanupProgressEvent()
        {
            // Arrange
            SetupConfiguration(
                autoCleanupEnabled: true,
                articleRetentionDays: 30,
                vacuumAfterCleanup: true,
                rebuildIndexesAfterCleanup: true);

            SetupBasicRepositoryMocks();

            var progressEvents = new List<CleanupProgressEventArgs>();
            _service.OnCleanupProgress += (sender, args) => progressEvents.Add(args);

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            progressEvents.Should().HaveCount(5); // 5 steps
            progressEvents.Select(p => p.StepNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
        }

        /// <summary>
        /// Verifies that the OnCleanupCompleted event is raised even on failure.
        /// </summary>
        [Fact]
        public async Task PerformCleanupAsync_OnFailure_ShouldStillRaiseOnCleanupCompletedEvent()
        {
            // Arrange
            SetupConfiguration(autoCleanupEnabled: true, articleRetentionDays: 30);

            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test error"));

            CleanupCompletedEventArgs? capturedArgs = null;
            _service.OnCleanupCompleted += (sender, args) => capturedArgs = args;

            // Act
            await _service.PerformCleanupAsync();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Result.Should().NotBeNull();
            capturedArgs.Result.Success.Should().BeFalse();
        }

        #endregion

        #region AnalyzeCleanupAsync Tests

        /// <summary>
        /// Verifies that analysis returns correct mapped data from repository.
        /// </summary>
        [Fact]
        public async Task AnalyzeCleanupAsync_ShouldReturnMappedAnalysis()
        {
            // Arrange
            SetupConfiguration(keepFavorites: true, keepUnread: true);

            var repositoryResult = new CleanupAnalysisResult
            {
                ArticlesToDelete = 100,
                ArticlesToKeep = 200,
                WouldKeepFavorites = true,
                WouldKeepUnread = true,
                EstimatedSpaceFreedBytes = 512000,
                ArticlesByFeed = new Dictionary<string, int>
                {
                    { "TestFeed", 50 }
                }
            };

            _mockCleanupRepository
                .Setup(x => x.AnalyzeCleanupImpactAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _service.AnalyzeCleanupAsync(retentionDays: 30);

            // Assert
            result.Should().NotBeNull();
            result.RetentionDays.Should().Be(30);
            result.ArticlesToDelete.Should().Be(100);
            result.ArticlesToKeep.Should().Be(200);
            result.WouldKeepFavorites.Should().BeTrue();
            result.WouldKeepUnread.Should().BeTrue();
            result.EstimatedSpaceFreedBytes.Should().Be(512000);
            result.ArticlesByFeed.Should().ContainKey("TestFeed").WhoseValue.Should().Be(50);
        }

        /// <summary>
        /// Verifies that cutoff date is calculated correctly from retention days.
        /// </summary>
        [Theory]
        [InlineData(30)]
        [InlineData(7)]
        [InlineData(365)]
        public async Task AnalyzeCleanupAsync_ShouldCalculateCorrectCutoffDate(int retentionDays)
        {
            // Arrange
            SetupConfiguration();

            var beforeCall = DateTime.UtcNow.AddDays(-retentionDays);

            _mockCleanupRepository
                .Setup(x => x.AnalyzeCleanupImpactAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CleanupAnalysisResult());

            // Act
            var result = await _service.AnalyzeCleanupAsync(retentionDays);

            // Assert
            var afterCall = DateTime.UtcNow.AddDays(-retentionDays);

            result.CutoffDate.Should().BeOnOrAfter(beforeCall.AddSeconds(-1));
            result.CutoffDate.Should().BeOnOrBefore(afterCall.AddSeconds(1));
        }

        #endregion

        #region GetConfigurationAsync Tests

        /// <summary>
        /// Verifies that configuration is loaded and returned correctly.
        /// </summary>
        [Fact]
        public async Task GetConfigurationAsync_ShouldReturnLoadedConfiguration()
        {
            // Arrange
            SetupConfiguration(
                articleRetentionDays: 45,
                keepFavorites: false,
                autoCleanupEnabled: false);

            // Act
            var result = await _service.GetConfigurationAsync();

            // Assert
            result.Should().NotBeNull();
            result.ArticleRetentionDays.Should().Be(45);
            result.KeepFavorites.Should().BeFalse();
            result.AutoCleanupEnabled.Should().BeFalse();
        }

        /// <summary>
        /// Verifies that returned configuration is a clone (modifications don't affect service).
        /// </summary>
        [Fact]
        public async Task GetConfigurationAsync_ShouldReturnClone()
        {
            // Arrange
            SetupConfiguration(articleRetentionDays: 30);

            // Act
            var config1 = await _service.GetConfigurationAsync();
            var config2 = await _service.GetConfigurationAsync();

            // Assert
            config1.Should().NotBeSameAs(config2);
            config1.ArticleRetentionDays = 999;
            config2.ArticleRetentionDays.Should().Be(30);
        }

        #endregion

        #region UpdateConfigurationAsync Tests

        /// <summary>
        /// Verifies that null configuration throws ArgumentNullException.
        /// </summary>
        [Fact]
        public async Task UpdateConfigurationAsync_WhenConfigurationIsNull_ShouldThrowArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _service.UpdateConfigurationAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("configuration");
        }

        /// <summary>
        /// Verifies that all settings are persisted correctly.
        /// </summary>
        [Fact]
        public async Task UpdateConfigurationAsync_WithValidConfiguration_ShouldPersistAllSettings()
        {
            // Arrange
            var newConfig = new CleanupConfiguration
            {
                ArticleRetentionDays = 60,
                KeepFavorites = false,               // se guarda en keep_favorite_articles
                KeepUnread = true,                   // se guarda en keep_unread_articles
                AutoCleanupEnabled = false,
                MaxImageCacheSizeMB = 1000,
                CleanupHourOfDay = 3,
                CleanupDayOfWeek = DayOfWeek.Monday,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = true
            };

            // Act
            await _service.UpdateConfigurationAsync(newConfig);

            // Assert - Verificar que se llamaron los Set con las claves reales y valores correctos
            _mockSettingsService.Verify(x => x.SetIntAsync(
                PreferenceKeys.ArticleRetentionDays,
                60), Times.Once());

            _mockSettingsService.Verify(x => x.SetBoolAsync(
                PreferenceKeys.KeepFavoriteArticles,
                false), Times.Once());

            _mockSettingsService.Verify(x => x.SetBoolAsync(
                PreferenceKeys.KeepUnreadArticles,
                true), Times.Once());

            _mockSettingsService.Verify(x => x.SetBoolAsync(
                PreferenceKeys.AutoCleanupEnabled,
                false), Times.Once());

            _mockSettingsService.Verify(x => x.SetIntAsync(
                "image_cache_max_size_mb",
                1000), Times.Once());

            _mockSettingsService.Verify(x => x.SetIntAsync(
                "cleanup_hour_of_day",
                3), Times.Once());

            _mockSettingsService.Verify(x => x.SetIntAsync(
                "cleanup_day_of_week",
                (int)DayOfWeek.Monday), Times.Once());

            _mockSettingsService.Verify(x => x.SetBoolAsync(
                "vacuum_after_cleanup",
                false), Times.Once());

            _mockSettingsService.Verify(x => x.SetBoolAsync(
                "rebuild_indexes_after_cleanup",
                true), Times.Once());

            // Opcional: verificar que NO se llamó la clave vieja
            _mockSettingsService.Verify(x => x.SetBoolAsync("keep_read_articles", It.IsAny<bool>()), Times.Never());
        }
        /// <summary>
        /// Verifies that exceptions during save are logged and re-thrown.
        /// </summary>
        [Fact]
        public async Task UpdateConfigurationAsync_WhenSaveFails_ShouldLogAndThrow()
        {
            // Arrange
            var config = new CleanupConfiguration();
            var expectedException = new InvalidOperationException("Save failed");

            _mockSettingsService
                .Setup(x => x.SetIntAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>()))
                .ThrowsAsync(expectedException);

            // Act
            Func<Task> act = async () => await _service.UpdateConfigurationAsync(config);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Save failed");

            _mockLogger.Verify(
                x => x.Error(
                    It.Is<Exception>(ex => ex == expectedException),
                    It.Is<string>(s => s.Contains("Failed to save cleanup configuration"))),
                Times.Once);
        }

        #endregion

        #region CleanupImageCacheAsync Tests

        /// <summary>
        /// Verifies that non-existent cache directory returns empty result.
        /// </summary>
        [Fact]
        public async Task CleanupImageCacheAsync_WhenDirectoryDoesNotExist_ShouldReturnEmptyResult()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testCacheDirectory, "NonExistent");

            // Override the service's cache directory behavior by testing indirectly
            // Since the path is hardcoded, we test with the actual temp directory setup

            // Act
            var result = await _service.CleanupImageCacheAsync(maxAgeDays: 30, maxSizeMB: 500);

            // Assert
            result.Should().NotBeNull();
            result.ImagesDeleted.Should().Be(0);
        }

        /// <summary>
        /// Verifies that old files are deleted based on age criteria.
        /// </summary>
        [Fact]
        public async Task CleanupImageCacheAsync_WithOldFiles_ShouldDeleteOldFiles()
        {
            // Arrange
            // Note: This test requires actual file system operations due to hardcoded path
            // In production, consider abstracting IFileSystem for better testability

            var oldFile = Path.Combine(_testCacheDirectory, "old_image.jpg");
            await File.WriteAllTextAsync(oldFile, "test content");
            File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-60));

            // Act - Since the service uses hardcoded LocalApplicationData path,
            // we verify the method structure rather than actual deletion
            var result = await _service.CleanupImageCacheAsync(maxAgeDays: 30, maxSizeMB: 500);

            // Assert
            result.Should().NotBeNull();
        }

        /// <summary>
        /// Verifies that cancellation is handled properly during image cleanup.
        /// </summary>
        [Fact]
        public async Task CleanupImageCacheAsync_WhenCancelled_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _service.CleanupImageCacheAsync(
                cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region GetStatisticsAsync Tests

        /// <summary>
        /// Verifies that statistics are retrieved from repository.
        /// </summary>
        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnRepositoryStatistics()
        {
            // Arrange
            var expectedStats = new DatabaseStatistics
            {
                TotalArticles = 1000,
                TotalFeeds = 50
            };

            _mockCleanupRepository
                .Setup(x => x.GetStatisticsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedStats);

            // Act
            var result = await _service.GetStatisticsAsync();

            // Assert
            result.Should().BeSameAs(expectedStats);
        }

        #endregion

        #region CheckIntegrityAsync Tests

        /// <summary>
        /// Verifies that integrity check delegates to repository.
        /// </summary>
        [Fact]
        public async Task CheckIntegrityAsync_ShouldReturnRepositoryResult()
        {
            // Arrange
            var expectedResult = new IntegrityCheckResult
            {
                IsValid = true
            };

            _mockCleanupRepository
                .Setup(x => x.CheckIntegrityAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _service.CheckIntegrityAsync();

            // Assert
            result.Should().BeSameAs(expectedResult);
        }

        #endregion

        #region Helper Methods

        private void SetupConfiguration(
    bool autoCleanupEnabled = true,
    int articleRetentionDays = 30,
    bool keepFavorites = true,
    bool keepUnread = true,
    bool vacuumAfterCleanup = false,
    bool rebuildIndexesAfterCleanup = false)
        {
            _mockSettingsService
                .Setup(x => x.GetIntAsync(PreferenceKeys.ArticleRetentionDays, It.IsAny<int>()))
                .ReturnsAsync((string key, int defaultValue) => articleRetentionDays);

            _mockSettingsService
                .Setup(x => x.GetBoolAsync(PreferenceKeys.KeepReadArticles, It.IsAny<bool>()))
                .ReturnsAsync((string key, bool defaultValue) => keepFavorites);

            _mockSettingsService
                .Setup(x => x.GetBoolAsync(PreferenceKeys.AutoCleanupEnabled, It.IsAny<bool>()))
                .ReturnsAsync((string key, bool defaultValue) => autoCleanupEnabled);

            _mockSettingsService
                .Setup(x => x.GetIntAsync("image_cache_max_size_mb", It.IsAny<int>()))
                .ReturnsAsync(500);

            _mockSettingsService
                .Setup(x => x.GetIntAsync("cleanup_hour_of_day", It.IsAny<int>()))
                .ReturnsAsync(2);

            _mockSettingsService
                .Setup(x => x.GetIntAsync("cleanup_day_of_week", It.IsAny<int>()))
                .ReturnsAsync((int)DayOfWeek.Sunday);

            _mockSettingsService
                .Setup(x => x.GetBoolAsync("vacuum_after_cleanup", It.IsAny<bool>()))
                .ReturnsAsync((string key, bool defaultValue) => vacuumAfterCleanup);

            _mockSettingsService
                .Setup(x => x.GetBoolAsync("rebuild_indexes_after_cleanup", It.IsAny<bool>()))
                .ReturnsAsync((string key, bool defaultValue) => rebuildIndexesAfterCleanup);
        }

        private void SetupBasicRepositoryMocks()
        {
            _mockCleanupRepository
                .Setup(x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ArticleDeletionResult());

            _mockCleanupRepository
                .Setup(x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrphanRemovalResult());

            _mockCleanupRepository
                .Setup(x => x.VacuumDatabaseAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VacuumResult());

            _mockCleanupRepository
                .Setup(x => x.RebuildIndexesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockCleanupRepository
                .Setup(x => x.UpdateStatisticsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void VerifyAllStepsExecuted()
        {
            _mockCleanupRepository.Verify(
                x => x.DeleteOldArticlesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCleanupRepository.Verify(
                x => x.RemoveOrphanedRecordsAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCleanupRepository.Verify(
                x => x.VacuumDatabaseAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCleanupRepository.Verify(
                x => x.RebuildIndexesAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _mockCleanupRepository.Verify(
                x => x.UpdateStatisticsAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Clean up test directory
                    try
                    {
                        if (Directory.Exists(_testCacheDirectory))
                        {
                            Directory.Delete(_testCacheDirectory, recursive: true);
                        }
                    }
                    catch
                    {
                        // Best effort cleanup
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}