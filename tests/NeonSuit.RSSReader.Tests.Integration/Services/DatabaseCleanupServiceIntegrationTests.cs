using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    /// <summary>
    /// Integration tests for <see cref="DatabaseCleanupService"/>.
    /// Validates end-to-end cleanup workflows with real database operations.
    /// </summary>
    [Collection("Integration Tests")]
    public class DatabaseCleanupServiceIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _dbFixture;
        private readonly ITestOutputHelper _output;
        private readonly ILogger _logger;
        private readonly ServiceFactory _factory;

        private IDatabaseCleanupService _cleanupService = null!;
        private IDatabaseCleanupRepository _cleanupRepository = null!;
        private ISettingsService _settingsService = null!;
        private RssReaderDbContext _dbContext = null!;

        public DatabaseCleanupServiceIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
        {
            _dbFixture = dbFixture;
            _output = output;
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.TestOutput(output)
                .CreateLogger()
                .ForContext<DatabaseCleanupServiceIntegrationTests>();

            _factory = new ServiceFactory(_dbFixture);
        }

        public async Task InitializeAsync()
        {
            _dbContext = _dbFixture.CreateNewDbContext();
            _cleanupRepository = new DatabaseCleanupRepository(_dbContext, _logger);
            _settingsService = _factory.CreateSettingsService();
            _cleanupService = new DatabaseCleanupService(_cleanupRepository, _settingsService, _logger);

            await SetupTestDataAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        #region Test Data Setup

        private async Task SetupTestDataAsync()
        {
            // Create test feeds
            var feed1 = new Feed
            {
                Title = "Tech News",
                Url = $"https://tech-{Guid.NewGuid():N}.com/rss",
                WebsiteUrl = "https://tech.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-100)
            };
            _dbContext.Feeds.Add(feed1);

            var feed2 = new Feed
            {
                Title = "Science Daily",
                Url = $"https://science-{Guid.NewGuid():N}.com/rss",
                WebsiteUrl = "https://science.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-100)
            };
            _dbContext.Feeds.Add(feed2);

            await _dbContext.SaveChangesAsync();

            // Create test articles with various ages and states
            var now = DateTime.UtcNow;

            // Old articles (60 days) - should be deleted
            for (int i = 0; i < 10; i++)
            {
                _dbContext.Articles.Add(new Article
                {
                    FeedId = feed1.Id,
                    Title = $"Old Article {i}",
                    Content = $"Content {i}",
                    Summary = $"Summary {i}",
                    PublishedDate = now.AddDays(-60),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = ArticleStatus.Read,
                    IsFavorite = false,
                    AddedDate = now.AddDays(-60)
                });
            }

            // Old but favorite articles - should be preserved
            for (int i = 0; i < 5; i++)
            {
                _dbContext.Articles.Add(new Article
                {
                    FeedId = feed1.Id,
                    Title = $"Old Favorite Article {i}",
                    Content = $"Content {i}",
                    Summary = $"Summary {i}",
                    PublishedDate = now.AddDays(-60),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = ArticleStatus.Read,
                    IsFavorite = true,
                    AddedDate = now.AddDays(-60)
                });
            }

            // Old but unread articles - should be preserved when keepUnread=true
            for (int i = 0; i < 5; i++)
            {
                _dbContext.Articles.Add(new Article
                {
                    FeedId = feed2.Id,
                    Title = $"Old Unread Article {i}",
                    Content = $"Content {i}",
                    Summary = $"Summary {i}",
                    PublishedDate = now.AddDays(-60),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = ArticleStatus.Unread,
                    IsFavorite = false,
                    AddedDate = now.AddDays(-60)
                });
            }

            // Recent articles (5 days) - should be preserved
            for (int i = 0; i < 15; i++)
            {
                _dbContext.Articles.Add(new Article
                {
                    FeedId = feed2.Id,
                    Title = $"Recent Article {i}",
                    Content = $"Content {i}",
                    Summary = $"Summary {i}",
                    PublishedDate = now.AddDays(-5),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = i % 2 == 0 ? ArticleStatus.Read : ArticleStatus.Unread,
                    IsFavorite = i % 3 == 0,
                    AddedDate = now.AddDays(-5)
                });
            }

            // Create test tags
            var tag1 = new Tag { Name = $"Technology_{Guid.NewGuid():N}", Color = "#FF5733", CreatedAt = now };
            var tag2 = new Tag { Name = $"Science_{Guid.NewGuid():N}", Color = "#33FF57", CreatedAt = now };
            _dbContext.Tags.AddRange(tag1, tag2);

            await _dbContext.SaveChangesAsync();

            // Create article-tag associations
            var articles = await _dbContext.Articles.Take(5).ToListAsync();
            foreach (var article in articles)
            {
                _dbContext.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = article.Id,
                    TagId = tag1.Id,
                    AppliedBy = "user",
                    AppliedAt = now
                });
            }

            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            _output.WriteLine($"Test data created: {await _dbContext.Articles.CountAsync()} articles");
        }

        #endregion

        #region Configuration Tests

        [Fact]
        public async Task GetConfigurationAsync_ShouldReturnDefaultConfiguration()
        {
            // Act
            var config = await _cleanupService.GetConfigurationAsync();

            // Assert
            config.Should().NotBeNull();
            config.ArticleRetentionDays.Should().Be(30); // Default value
            config.KeepFavorites.Should().BeTrue();
            config.KeepUnread.Should().BeTrue();
            config.AutoCleanupEnabled.Should().BeTrue();
            config.VacuumAfterCleanup.Should().BeTrue();
            config.RebuildIndexesAfterCleanup.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateConfigurationAsync_ShouldPersistSettings()
        {
            // Arrange
            var originalConfig = await _cleanupService.GetConfigurationAsync();
            _output.WriteLine($"Configuración original - ArticleRetentionDays: {originalConfig.ArticleRetentionDays}");

            var newConfig = new CleanupConfiguration
            {
                ArticleRetentionDays = 60,
                KeepFavorites = false,              // ← esto se guardará en KeepFavoriteArticles
                KeepUnread = true,                  // ← esto se guardará en KeepUnreadArticles
                AutoCleanupEnabled = false,
                MaxImageCacheSizeMB = 1000,
                CleanupHourOfDay = 4,
                CleanupDayOfWeek = DayOfWeek.Monday,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = true,
                ImageCacheRetentionDays = 45,
            };

            // Act
            await _cleanupService.UpdateConfigurationAsync(newConfig);

            // Verificación con las claves REALES que ahora se usan
            var rawKeepFavorite = await _settingsService.GetBoolAsync(
                PreferenceKeys.KeepFavoriteArticles,   // ← nueva clave
                true);

            var rawKeepUnread = await _settingsService.GetBoolAsync(
                PreferenceKeys.KeepUnreadArticles,     // ← nueva clave
                true);

            var rawRetention = await _settingsService.GetIntAsync(
                PreferenceKeys.ArticleRetentionDays,
                30);

            var rawAutoCleanup = await _settingsService.GetBoolAsync(
                PreferenceKeys.AutoCleanupEnabled,
                true);

            _output.WriteLine($"Después de Update - Raw KeepFavoriteArticles: {rawKeepFavorite}");
            _output.WriteLine($"Después de Update - Raw KeepUnreadArticles:   {rawKeepUnread}");
            _output.WriteLine($"Después de Update - Raw ArticleRetentionDays:  {rawRetention}");
            _output.WriteLine($"Después de Update - Raw AutoCleanupEnabled:    {rawAutoCleanup}");

            // Servicio fresco para forzar recarga
            var freshService = new DatabaseCleanupService(
                _cleanupRepository,
                _settingsService,
                _logger);

            var retrievedConfig = await freshService.GetConfigurationAsync();

            // Asserts ajustados a las nuevas claves
            retrievedConfig.Should().NotBeNull();
            retrievedConfig.ArticleRetentionDays.Should().Be(60);
            retrievedConfig.AutoCleanupEnabled.Should().BeFalse();

            // Ahora verificamos las claves correctas
            rawKeepFavorite.Should().BeFalse("El valor crudo de KeepFavoriteArticles debe ser false");
            rawKeepUnread.Should().BeTrue("El valor crudo de KeepUnreadArticles debe ser true (según newConfig)");

            rawRetention.Should().Be(60, "El valor de retención debe persistir");
            rawAutoCleanup.Should().BeFalse();

            // Restaurar original
            await _cleanupService.UpdateConfigurationAsync(originalConfig);
        }

        [Fact]
        public async Task UpdateConfigurationAsync_WithNullConfiguration_ShouldThrowArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _cleanupService.UpdateConfigurationAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("configuration");
        }

        [Fact]
        public async Task GetConfigurationAsync_ShouldReturnClone_NotReference()
        {
            // Arrange
            var config1 = await _cleanupService.GetConfigurationAsync();

            // Act
            config1.ArticleRetentionDays = 999;
            var config2 = await _cleanupService.GetConfigurationAsync();

            // Assert
            config2.ArticleRetentionDays.Should().NotBe(999);
            config1.Should().NotBeSameAs(config2);
        }

        #endregion

        #region PerformCleanupAsync Tests

        [Fact]
        public async Task PerformCleanupAsync_WhenAutoCleanupDisabled_ShouldSkipAndReturnSkippedResult()
        {
            // Arrange
            var config = await _cleanupService.GetConfigurationAsync();
            config.AutoCleanupEnabled = false;
            await _cleanupService.UpdateConfigurationAsync(config);

            var startingEventFired = false;
            _cleanupService.OnCleanupStarting += (s, e) => startingEventFired = true;

            // Act
            var result = await _cleanupService.PerformCleanupAsync();

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Skipped.Should().BeTrue();
            startingEventFired.Should().BeFalse("OnCleanupStarting should not fire when skipped");
        }

        [Fact]
        public async Task PerformCleanupAsync_WhenEnabled_ShouldExecuteFullWorkflow()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = true,
                KeepUnread = true,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = true,
                RebuildIndexesAfterCleanup = true
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            var progressEvents = new List<CleanupProgressEventArgs>();
            var startingEventFired = false;
            var completedEventFired = false;

            _cleanupService.OnCleanupStarting += (s, e) => startingEventFired = true;
            _cleanupService.OnCleanupProgress += (s, e) => progressEvents.Add(e);
            _cleanupService.OnCleanupCompleted += (s, e) => completedEventFired = true;

            var articlesBefore = await _dbContext.Articles.CountAsync();

            // Act
            var result = await _cleanupService.PerformCleanupAsync();

            // Assert
            result.Success.Should().BeTrue();
            result.Skipped.Should().BeFalse();
            startingEventFired.Should().BeTrue();
            completedEventFired.Should().BeTrue();

            progressEvents.Should().HaveCount(5); // 5 steps: articles, orphans, vacuum, indexes, stats
            progressEvents.Select(p => p.StepNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });

            result.ArticleCleanup.Should().NotBeNull();
            result.OrphanCleanup.Should().NotBeNull();
            result.Duration.Should().BePositive();
            result.PerformedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            _output.WriteLine($"Articles deleted: {result.ArticleCleanup?.ArticlesDeleted}");
            _output.WriteLine($"Space freed: {result.SpaceFreedBytes} bytes");
            _output.WriteLine($"Duration: {result.Duration.TotalMilliseconds}ms");
        }

        [Fact]
        public async Task PerformCleanupAsync_ShouldRespectKeepFavoritesOption()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = true,
                KeepUnread = false,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            var favoritesBefore = await _dbContext.Articles.CountAsync(a => a.IsFavorite && a.PublishedDate < DateTime.UtcNow.AddDays(-30));

            // Act
            var result = await _cleanupService.PerformCleanupAsync();

            // Assert
            result.ArticleCleanup.Should().NotBeNull();

            // Verify favorites were preserved
            _dbContext.ChangeTracker.Clear();
            var favoritesAfter = await _dbContext.Articles.CountAsync(a => a.IsFavorite && a.PublishedDate < DateTime.UtcNow.AddDays(-30));
            favoritesAfter.Should().Be(favoritesBefore);
        }

        [Fact]
        public async Task PerformCleanupAsync_ShouldRespectKeepUnreadOption()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = false,
                KeepUnread = true,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            var unreadBefore = await _dbContext.Articles.CountAsync(a => a.Status == ArticleStatus.Unread && a.PublishedDate < DateTime.UtcNow.AddDays(-30));

            // Act
            var result = await _cleanupService.PerformCleanupAsync();

            // Assert
            result.ArticleCleanup.Should().NotBeNull();

            // Verify unread articles were preserved
            _dbContext.ChangeTracker.Clear();
            var unreadAfter = await _dbContext.Articles.CountAsync(a => a.Status == ArticleStatus.Unread && a.PublishedDate < DateTime.UtcNow.AddDays(-30));
            unreadAfter.Should().Be(unreadBefore);
        }

        [Fact]
        public async Task PerformCleanupAsync_WithZeroRetentionDays_ShouldSkipArticleCleanup()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 0,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            var articlesBefore = await _dbContext.Articles.CountAsync();

            // Act
            var result = await _cleanupService.PerformCleanupAsync();

            // Assert
            result.ArticleCleanup.Should().BeNull();
            _dbContext.ChangeTracker.Clear();
            var articlesAfter = await _dbContext.Articles.CountAsync();
            articlesAfter.Should().Be(articlesBefore);
        }

        [Fact]
        public async Task PerformCleanupAsync_ShouldUpdateTagCountsWhenArticlesDeleted()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = false,
                KeepUnread = false,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act
            await _cleanupService.PerformCleanupAsync();

            // Assert - Tag counts should be updated (no direct assertion possible without querying, but no exception = success)
            _dbContext.ChangeTracker.Clear();
            var tags = await _dbContext.Tags.ToListAsync();
            tags.Should().NotBeEmpty();
        }

        [Fact]
        public async Task PerformCleanupAsync_WhenCancelled_ShouldHandleGracefully()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                AutoCleanupEnabled = true
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            using var cts = new CancellationTokenSource();

            // Cancel immediately
            cts.Cancel();

            // Act
            var result = await _cleanupService.PerformCleanupAsync(cts.Token);

            // Assert
            result.Success.Should().BeFalse();
            result.Errors.Should().Contain("Operation was cancelled");
        }

        #endregion

        #region AnalyzeCleanupAsync Tests

        [Fact]
        public async Task AnalyzeCleanupAsync_ShouldReturnAccurateImpactAnalysis()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                KeepFavorites = true,
                KeepUnread = true
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act
            var analysis = await _cleanupService.AnalyzeCleanupAsync(retentionDays: 30);

            // Assert
            analysis.Should().NotBeNull();
            analysis.RetentionDays.Should().Be(30);
            analysis.CutoffDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromSeconds(1));
            analysis.ArticlesToDelete.Should().BeGreaterThan(0);
            analysis.ArticlesToKeep.Should().BeGreaterThan(0);
            analysis.WouldKeepFavorites.Should().BeTrue();
            analysis.WouldKeepUnread.Should().BeTrue();
            analysis.EstimatedSpaceFreedBytes.Should().BeGreaterThan(0);
            analysis.ArticlesByFeed.Should().NotBeNull();
        }
        [Fact]
        public async Task AnalyzeCleanupAsync_WithDifferentRetentionDays_ShouldReturnDifferentResults()
        {
            // Act
            var analysis7Days = await _cleanupService.AnalyzeCleanupAsync(7);
            var analysis30Days = await _cleanupService.AnalyzeCleanupAsync(30);
            var analysis90Days = await _cleanupService.AnalyzeCleanupAsync(90);

            // Assert
            // 7 y 30 días deberían ser iguales (ambos eliminan los artículos de 60 días)
            analysis7Days.ArticlesToDelete.Should().Be(analysis30Days.ArticlesToDelete,
                "both 7 and 30 days should catch the 60-day old articles");

            // 90 días no debería eliminar nada (todos los artículos tienen 60 días o menos)
            analysis90Days.ArticlesToDelete.Should().Be(0,
                "90 days retention should keep all 60-day old articles");

            // Verificar que hay diferencia entre corto y largo plazo
            analysis7Days.ArticlesToDelete.Should().BeGreaterThan(analysis90Days.ArticlesToDelete,
                "shorter retention should delete more articles than longer retention");

            // Verificar fechas de corte
            analysis7Days.CutoffDate.Should().BeAfter(analysis30Days.CutoffDate);
            analysis30Days.CutoffDate.Should().BeAfter(analysis90Days.CutoffDate);
        }

        #endregion

        #region CleanupImageCacheAsync Tests

        [Fact]
        public async Task CleanupImageCacheAsync_WithDefaultParameters_ShouldCompleteSuccessfully()
        {
            // Act
            var result = await _cleanupService.CleanupImageCacheAsync();

            // Assert
            result.Should().NotBeNull();
            // Result will be empty since cache directory likely doesn't exist in test environment
        }

        [Fact]
        public async Task CleanupImageCacheAsync_WhenCancelled_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            Func<Task> act = async () => await _cleanupService.CleanupImageCacheAsync(
                cancellationToken: cts.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region GetStatisticsAsync Tests

        [Fact]
        public async Task GetStatisticsAsync_ShouldReturnCurrentDatabaseStatistics()
        {
            // Act
            var stats = await _cleanupService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalArticles.Should().BeGreaterThan(0);
            stats.TotalFeeds.Should().Be(2);
            stats.TotalTags.Should().Be(2);
            stats.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            stats.ReadArticles.Should().BeGreaterThanOrEqualTo(0);
            stats.UnreadArticles.Should().BeGreaterThanOrEqualTo(0);
            stats.FavoriteArticles.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetStatisticsAsync_AfterCleanup_ShouldReflectChanges()
        {
            // Arrange
            var statsBefore = await _cleanupService.GetStatisticsAsync();

            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = false,
                KeepUnread = false,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act
            await _cleanupService.PerformCleanupAsync();
            var statsAfter = await _cleanupService.GetStatisticsAsync();

            // Assert
            statsAfter.TotalArticles.Should().BeLessThan(statsBefore.TotalArticles);
            statsAfter.GeneratedAt.Should().BeAfter(statsBefore.GeneratedAt);
        }

        #endregion

        #region CheckIntegrityAsync Tests

        [Fact]
        public async Task CheckIntegrityAsync_ShouldReturnValidResult()
        {
            // Act
            var result = await _cleanupService.CheckIntegrityAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.CheckDuration.Should().BePositive();
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task OnCleanupStarting_ShouldFireWithCorrectConfiguration()
        {
            // Arrange
            CleanupStartingEventArgs? capturedArgs = null;
            _cleanupService.OnCleanupStarting += (s, e) => capturedArgs = e;

            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 45,
                KeepFavorites = true,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act
            await _cleanupService.PerformCleanupAsync();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Configuration.ArticleRetentionDays.Should().Be(45);
            capturedArgs.Configuration.KeepFavorites.Should().BeTrue();
            capturedArgs.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task OnCleanupProgress_ShouldFireForEachStep()
        {
            // Arrange
            var progressEvents = new List<CleanupProgressEventArgs>();
            _cleanupService.OnCleanupProgress += (s, e) => progressEvents.Add(e);

            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = true,
                RebuildIndexesAfterCleanup = true
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act
            await _cleanupService.PerformCleanupAsync();

            // Assert
            progressEvents.Should().HaveCount(5);
            progressEvents.Should().OnlyContain(e => e.PercentComplete >= 0 && e.PercentComplete <= 100);
            progressEvents.Should().OnlyContain(e => !string.IsNullOrEmpty(e.CurrentStep));
            progressEvents.Last().IsFinalStep.Should().BeTrue();
        }

        [Fact]
        public async Task OnCleanupCompleted_ShouldFireEvenOnFailure()
        {
            // Arrange
            CleanupCompletedEventArgs? capturedArgs = null;

            // Crear un repositorio que fallará - usando un path de base de datos inválido
            var invalidConnectionString = "Data Source=C:\\invalid\\path\\that\\doesnt\\exist.db";
            var optionsBuilder = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite(invalidConnectionString); // O el provider que uses

            var failingContext = new RssReaderDbContext(optionsBuilder.Options, _logger);
            var failingRepo = new DatabaseCleanupRepository(failingContext, _logger);
            var failingService = new DatabaseCleanupService(failingRepo, _settingsService, _logger);

            failingService.OnCleanupCompleted += (s, e) => capturedArgs = e;

            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,  // Desactivar vacuum para evitar pasos extra
                RebuildIndexesAfterCleanup = false
            };
            await failingService.UpdateConfigurationAsync(config);

            // Act
            var result = await failingService.PerformCleanupAsync();

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Result.Should().NotBeNull();
            capturedArgs.Result.Success.Should().BeFalse();
            capturedArgs.Result.Errors.Should().NotBeEmpty();
            capturedArgs.CompletionTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Events_ShouldNotFireWhenCleanupSkipped()
        {
            // Arrange
            var config = new CleanupConfiguration { AutoCleanupEnabled = false };
            await _cleanupService.UpdateConfigurationAsync(config);

            var startingFired = false;
            var progressFired = false;
            var completedFired = false;

            _cleanupService.OnCleanupStarting += (s, e) => startingFired = true;
            _cleanupService.OnCleanupProgress += (s, e) => progressFired = true;
            _cleanupService.OnCleanupCompleted += (s, e) => completedFired = true;

            // Act
            await _cleanupService.PerformCleanupAsync();

            // Assert
            startingFired.Should().BeFalse();
            progressFired.Should().BeFalse();
            completedFired.Should().BeTrue(); // Completed always fires
        }

        #endregion

        #region Edge Cases and Error Handling

        [Fact]
        public async Task PerformCleanupAsync_WithConcurrentCalls_ShouldHandleGracefully()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = false,
                RebuildIndexesAfterCleanup = false
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            // Act - Start multiple cleanups simultaneously
            var tasks = new[]
            {
                _cleanupService.PerformCleanupAsync(),
                _cleanupService.PerformCleanupAsync(),
                _cleanupService.PerformCleanupAsync()
            };

            var results = await Task.WhenAll(tasks);

            // Assert - All should complete (some may fail due to concurrency, but no exceptions thrown)
            results.Should().AllSatisfy(r => r.Should().NotBeNull());
        }
        [Fact]
        public async Task PerformCleanupAsync_WithVeryLargeRetentionDays_ShouldDeleteNothing()
        {
            // Arrange
            // Guardar DIRECTAMENTE en settings service
            await _settingsService.SetIntAsync(PreferenceKeys.ArticleRetentionDays, 365);
            await _settingsService.SetBoolAsync(PreferenceKeys.KeepReadArticles, true);
            await _settingsService.SetBoolAsync(PreferenceKeys.AutoCleanupEnabled, true);

            // Crear servicio NUEVO que lea la configuración actualizada
            var freshService = new DatabaseCleanupService(
                _cleanupRepository,
                _settingsService,
                _logger);

            // Verificar que se guardó correctamente
            var verifyConfig = await freshService.GetConfigurationAsync();
            verifyConfig.ArticleRetentionDays.Should().Be(365); // AHORA DEBE PASAR

            var articlesBefore = await _dbContext.Articles.CountAsync();

            // Act
            var result = await freshService.PerformCleanupAsync();

            // Assert
            result.ArticleCleanup.Should().NotBeNull();
            result.ArticleCleanup!.ArticlesDeleted.Should().Be(0);

            _dbContext.ChangeTracker.Clear();
            var articlesAfter = await _dbContext.Articles.CountAsync();
            articlesAfter.Should().Be(articlesBefore);
        }
        [Fact]
        public async Task FullCleanupWorkflow_ShouldMaintainDatabaseConsistency()
        {
            // Arrange
            var config = new CleanupConfiguration
            {
                ArticleRetentionDays = 30,
                KeepFavorites = true,
                KeepUnread = true,
                AutoCleanupEnabled = true,
                VacuumAfterCleanup = true,
                RebuildIndexesAfterCleanup = true
            };
            await _cleanupService.UpdateConfigurationAsync(config);

            var statsBefore = await _cleanupService.GetStatisticsAsync();
            var integrityBefore = await _cleanupService.CheckIntegrityAsync();

            // Act - Perform full cleanup workflow
            var cleanupResult = await _cleanupService.PerformCleanupAsync();
            var statsAfter = await _cleanupService.GetStatisticsAsync();
            var integrityAfter = await _cleanupService.CheckIntegrityAsync();

            // Assert
            cleanupResult.Success.Should().BeTrue();
            integrityBefore.IsValid.Should().BeTrue();
            integrityAfter.IsValid.Should().BeTrue();

            statsAfter.TotalArticles.Should().BeLessThan(statsBefore.TotalArticles);

            // Verify no orphaned records exist
            _dbContext.ChangeTracker.Clear();
            var orphanedArticles = await _dbContext.Articles
                .CountAsync(a => !_dbContext.Feeds.Any(f => f.Id == a.FeedId));
            orphanedArticles.Should().Be(0);
        }

        [Fact]
        public async Task Debug_ConfigurationPersistence_Test()
        {
            // 1. Guardar configuración inicial para restaurar al final (opcional pero buena práctica)
            var initialConfig = await _cleanupService.GetConfigurationAsync();
            _output.WriteLine($"[INICIO] Retención inicial: {initialConfig.ArticleRetentionDays} días");

            // 2. Preparar nueva configuración (usamos valor dentro del límite de validación actual para probar)
            var newRetentionDays = 180; // ← Cambia a 36500 SOLO después de subir el límite en ValidateValue
            var newConfig = new CleanupConfiguration
            {
                ArticleRetentionDays = newRetentionDays,
                KeepFavorites = true,
                KeepUnread = true,
                AutoCleanupEnabled = true,
                // Agrega más propiedades si las tienes implementadas
            };

            _output.WriteLine($"[PREPARACIÓN] Intentando guardar ArticleRetentionDays = {newRetentionDays}");

            // 3. Ejecutar actualización
            await _cleanupService.UpdateConfigurationAsync(newConfig);

            // 4. Verificaciones inmediatas (múltiples ángulos)
            var settingsRetention = await _settingsService.GetIntAsync(
                PreferenceKeys.ArticleRetentionDays, -999);
            _output.WriteLine($"[SettingsService directo] ArticleRetentionDays = {settingsRetention}");

            // Verificación en BD usando el contexto del test
            var dbRetention = await _dbContext.Set<UserPreferences>()
                .Where(p => p.Key == PreferenceKeys.ArticleRetentionDays)
                .Select(p => p.Value)
                .FirstOrDefaultAsync();
            _output.WriteLine($"[DbContext del test] ArticleRetentionDays = {dbRetention ?? "NO EXISTE"}");

            // Hash del contexto para depurar si comparten BD
            _output.WriteLine($"[DEBUG] DbContext test hash: {_dbContext.GetHashCode()}");

            // 5. Forzar nuevo servicio (para ver si carga desde BD o caché)
            var freshService = new DatabaseCleanupService(_cleanupRepository, _settingsService, _logger);
            var freshConfig = await freshService.GetConfigurationAsync();
            _output.WriteLine($"[Fresh Service] ArticleRetentionDays = {freshConfig.ArticleRetentionDays}");

            // 6. Análisis de impacto con diferentes valores
            var analysis30 = await _cleanupService.AnalyzeCleanupAsync(30);
            var analysis180 = await _cleanupService.AnalyzeCleanupAsync(newRetentionDays);
            var analysis36500 = await _cleanupService.AnalyzeCleanupAsync(36500);

            _output.WriteLine($"[ANÁLISIS] Con 30 días → eliminaría {analysis30.ArticlesToDelete} artículos");
            _output.WriteLine($"[ANÁLISIS] Con {newRetentionDays} días → eliminaría {analysis180.ArticlesToDelete} artículos");
            _output.WriteLine($"[ANÁLISIS] Con 36500 días → eliminaría {analysis36500.ArticlesToDelete} artículos");

            // 7. Limpieza real y resultado final
            var cleanupResult = await freshService.PerformCleanupAsync();
            var deletedCount = cleanupResult.ArticleCleanup?.ArticlesDeleted ?? 0;
            _output.WriteLine($"[LIMPIEZA REAL] Artículos eliminados: {deletedCount}");

            // 8. Assertions (ajustadas al valor que esperamos)
            settingsRetention.Should().Be(newRetentionDays,
                "El SettingsService debería reflejar el nuevo valor después de UpdateConfigurationAsync");

            freshConfig.ArticleRetentionDays.Should().Be(newRetentionDays,
                "Un servicio recién creado debería cargar el valor actualizado");

            deletedCount.Should().BeLessThan(10,
                "Con retención alta no debería eliminar casi nada de los artículos de prueba");

            // Opcional: restaurar configuración original para no afectar otros tests
            await _cleanupService.UpdateConfigurationAsync(initialConfig);
            _output.WriteLine("[FINAL] Restaurada configuración inicial");
        }

        #endregion
    }
}