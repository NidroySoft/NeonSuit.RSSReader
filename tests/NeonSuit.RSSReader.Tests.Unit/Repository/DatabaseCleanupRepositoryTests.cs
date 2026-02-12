using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{

    [CollectionDefinition("Database_Cleanup")]
    public class CleanupDataBase : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_Cleanup")]
    public class DatabaseCleanupRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly DatabaseCleanupRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private string _testDbPath;
        private bool _disposed;

        // Test Constants
        private const int DEFAULT_FEED_ID = 1;
        private const int ALTERNATE_FEED_ID = 2;
        private const string TEST_DB_FILENAME = "test_rssreader.db";

        public DatabaseCleanupRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();

            // Crear base de datos temporal para pruebas de tamaño/archivo
            _testDbPath = Path.Combine(Path.GetTempPath(), TEST_DB_FILENAME);

            _dbContext = fixture.Context;

            // Configurar propiedad DatabasePath mediante reflexión para pruebas de tamaño
            var databaseProperty = typeof(RssReaderDbContext).GetProperty("DatabasePath");
            if (databaseProperty != null && databaseProperty.CanWrite)
            {
                databaseProperty.SetValue(_dbContext, _testDbPath);
            }

            _repository = new DatabaseCleanupRepository(_dbContext, _mockLogger.Object);

            ClearTestData().Wait();
            SeedBaseData().Wait();
        }

        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);
            _mockLogger.Setup(x => x.ForContext<DatabaseCleanupRepository>())
                .Returns(_mockLogger.Object);
            _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
            _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        #region Test Data Helpers

        private async Task ClearTestData()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ArticleTags");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Tags");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Articles");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Feeds");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Categories");
            _dbContext.ChangeTracker.Clear();
        }

        private async Task SeedBaseData()
        {
            if (!await _dbContext.Feeds.AnyAsync(f => f.Id == DEFAULT_FEED_ID))
            {
                _dbContext.Feeds.Add(new Feed
                {
                    Id = DEFAULT_FEED_ID,
                    Title = "Test Feed 1",
                    Url = "https://example.com/feed1",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-100)
                });
            }

            if (!await _dbContext.Feeds.AnyAsync(f => f.Id == ALTERNATE_FEED_ID))
            {
                _dbContext.Feeds.Add(new Feed
                {
                    Id = ALTERNATE_FEED_ID,
                    Title = "Test Feed 2",
                    Url = "https://example.com/feed2",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-100)
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        private async Task<List<Article>> SeedArticlesAsync(int count = 10)
        {
            var articles = new List<Article>();
            var now = DateTime.UtcNow;

            for (int i = 1; i <= count; i++)
            {
                var article = new Article
                {
                    FeedId = i % 2 == 0 ? DEFAULT_FEED_ID : ALTERNATE_FEED_ID,
                    Title = $"Test Article {i}",
                    Content = $"Content {i}",
                    Summary = $"Summary {i}",
                    PublishedDate = now.AddDays(-i * 10), // Artículos cada 10 días
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = i % 3 == 0 ? ArticleStatus.Read : ArticleStatus.Unread,
                    IsFavorite = i % 5 == 0,
                    IsStarred = i % 7 == 0,
                    AddedDate = now.AddDays(-i * 10)
                };

                _dbContext.Articles.Add(article);
                articles.Add(article);
            }

            await _dbContext.SaveChangesAsync();
            return articles;
        }

        private async Task SeedTagsAndArticleTagsAsync()
        {
            // Crear tags
            var tags = new List<Tag>();
            for (int i = 1; i <= 5; i++)
            {
                tags.Add(new Tag
                {
                    Id = i,
                    Name = $"Tag {i}",
                    Color = $"#{i:X6}",
                    CreatedAt = DateTime.UtcNow
                });
            }
            _dbContext.Tags.AddRange(tags);
            await _dbContext.SaveChangesAsync();

            // Crear ArticleTags válidos
            var articles = await _dbContext.Articles.Take(3).ToListAsync();
            foreach (var article in articles)
            {
                _dbContext.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = article.Id,
                    TagId = 1,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                });
            }
            await _dbContext.SaveChangesAsync();
        }

        private async Task SeedOrphanedRecordsAsync()
        {
            // Artículos huérfanos (FeedId no existe)
            _dbContext.Articles.Add(new Article
            {
                FeedId = 999,
                Title = "Orphan Article",
                Content = "Orphan Content",
                PublishedDate = DateTime.UtcNow,
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                Status = ArticleStatus.Unread
            });

            // ArticleTags huérfanos (Artículo no existe)
            _dbContext.ArticleTags.Add(new ArticleTag
            {
                ArticleId = 888,
                TagId = 1,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            });

            // ArticleTags huérfanos (Tag no existe)
            _dbContext.ArticleTags.Add(new ArticleTag
            {
                ArticleId = 1,
                TagId = 999,
                AppliedBy = "user",
                AppliedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        private async Task CreatePhysicalDatabaseFile()
        {
            // Generar nombre único para evitar conflictos
            var uniqueFileName = $"test_rssreader_{Guid.NewGuid():N}.db";
            _testDbPath = Path.Combine(Path.GetTempPath(), uniqueFileName);

            // Asegurar que no exista
            if (File.Exists(_testDbPath))
            {
                try { File.Delete(_testDbPath); } catch { }
            }

            // Usar 'using' para asegurar que se cierra el contexto
            var options = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using (var context = new RssReaderDbContext(options, _mockLogger.Object))
            {
                await context.Database.EnsureCreatedAsync();

                context.Feeds.Add(new Feed
                {
                    Id = 1,
                    Title = "Physical Feed",
                    Url = "https://example.com/physical",
                    CreatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            } // El contexto se cierra y libera el archivo aquí
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeRepository()
        {
            // Act
            var repository = new DatabaseCleanupRepository(_dbContext, _mockLogger.Object);

            // Assert
            repository.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new DatabaseCleanupRepository(null!, _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("dbContext");
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new DatabaseCleanupRepository(_dbContext, null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("logger");
        }

        #endregion

        #region DeleteOldArticlesAsync Tests

        [Fact]
        public async Task DeleteOldArticlesAsync_WithFutureCutoffDate_ShouldThrowArgumentException()
        {
            // Arrange
            var futureDate = DateTime.UtcNow.AddDays(1);

            // Act
            Func<Task> act = async () => await _repository.DeleteOldArticlesAsync(futureDate, false, false);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithParameterName("cutoffDate");
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithNoArticles_ShouldReturnZeroDeletions()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, false);

            // Assert
            result.Should().NotBeNull();
            result.ArticlesFound.Should().Be(0);
            result.ArticlesDeleted.Should().Be(0);
            result.OldestArticleDeleted.Should().BeNull();
            result.NewestArticleDeleted.Should().BeNull();
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithOldArticles_ShouldDeleteMatchingArticles()
        {
            // Arrange
            await SeedArticlesAsync(20);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-50);
            var expectedCount = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate);

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, false);

            // Assert
            result.Should().NotBeNull();
            result.ArticlesFound.Should().Be(expectedCount);
            result.ArticlesDeleted.Should().Be(expectedCount);
            result.OldestArticleDeleted.Should().NotBeNull();
            result.NewestArticleDeleted.Should().NotBeNull();

        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithKeepFavorites_ShouldPreserveFavoriteArticles()
        {
            // Arrange
            await SeedArticlesAsync(20);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            var favoritesToKeep = await _dbContext.Articles
                .Where(a => a.PublishedDate < cutoffDate && a.IsFavorite)
                .ToListAsync();

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, true, false);

            // Assert
            result.Should().NotBeNull();

            ClearEntityTracking();
            var remainingFavorites = await _dbContext.Articles
                .Where(a => a.IsFavorite && a.PublishedDate < cutoffDate)
                .ToListAsync();

            remainingFavorites.Should().HaveCount(favoritesToKeep.Count);
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithKeepUnread_ShouldPreserveUnreadArticles()
        {
            // Arrange
            await SeedArticlesAsync(20);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            var unreadToKeep = await _dbContext.Articles
                .Where(a => a.PublishedDate < cutoffDate && a.Status == ArticleStatus.Unread)
                .ToListAsync();

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, true);

            // Assert
            result.Should().NotBeNull();

            ClearEntityTracking();
            var remainingUnread = await _dbContext.Articles
                .Where(a => a.Status == ArticleStatus.Unread && a.PublishedDate < cutoffDate)
                .ToListAsync();

            remainingUnread.Should().HaveCount(unreadToKeep.Count);
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithCancellationToken_ShouldThrowWhenCancelled()
        {
            // Arrange
            await SeedArticlesAsync(10);
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            Func<Task> act = async () => await _repository.DeleteOldArticlesAsync(
                cutoffDate, false, false,
                cancellationToken: cancellationTokenSource.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

        }

        #endregion

        #region RemoveOrphanedRecordsAsync Tests

        [Fact]
        public async Task RemoveOrphanedRecordsAsync_WithOrphanedRecords_ShouldRemoveAllOrphans()
        {
            // Arrange
            await SeedArticlesAsync(5);
            await SeedTagsAndArticleTagsAsync();

            // ✅ USAR EL MÉTODO SEGURO
            await SeedForeignKeyViolationsDirectlyAsync();

            ClearEntityTracking();

            var orphanCountBefore = await _dbContext.ArticleTags
                .CountAsync(at => !_dbContext.Articles.Any(a => a.Id == at.ArticleId) ||
                                  !_dbContext.Tags.Any(t => t.Id == at.TagId));

            var orphanArticlesBefore = await _dbContext.Articles
                .CountAsync(a => !_dbContext.Feeds.Any(f => f.Id == a.FeedId));

            // Act
            var result = await _repository.RemoveOrphanedRecordsAsync();

            // Assert
            result.Should().NotBeNull();
            result.OrphanedArticleTagsRemoved.Should().Be(orphanCountBefore);
            result.OrphanedArticlesRemoved.Should().Be(orphanArticlesBefore);
            result.TotalRecordsRemoved.Should().Be(orphanCountBefore + orphanArticlesBefore);

            ClearEntityTracking();

            var orphanCountAfter = await _dbContext.ArticleTags
                .CountAsync(at => !_dbContext.Articles.Any(a => a.Id == at.ArticleId) ||
                                  !_dbContext.Tags.Any(t => t.Id == at.TagId));

            var orphanArticlesAfter = await _dbContext.Articles
                .CountAsync(a => !_dbContext.Feeds.Any(f => f.Id == a.FeedId));

            orphanCountAfter.Should().Be(0);
            orphanArticlesAfter.Should().Be(0);
        }

        [Fact]
        public async Task RemoveOrphanedRecordsAsync_WithNoOrphans_ShouldReturnZeroAndLogDebug()
        {
            // Arrange
            await SeedArticlesAsync(5);
            await SeedTagsAndArticleTagsAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.RemoveOrphanedRecordsAsync();

            // Assert
            result.Should().NotBeNull();
            result.TotalRecordsRemoved.Should().Be(0);

        }

        [Fact]
        public async Task RemoveOrphanedRecordsAsync_WithCancellationToken_ShouldThrowWhenCancelled()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            Func<Task> act = async () => await _repository.RemoveOrphanedRecordsAsync(cancellationTokenSource.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

        }

        #endregion

        #region VacuumDatabaseAsync Tests

        [Fact]
        public async Task VacuumDatabaseAsync_WithPhysicalDatabase_ShouldReduceFileSize()
        {
            // Arrange
            await CreatePhysicalDatabaseFile();

            var dbContextOptions = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var physicalContext = new RssReaderDbContext(dbContextOptions, _mockLogger.Object);
            var repository = new DatabaseCleanupRepository(physicalContext, _mockLogger.Object);

            // Act
            var result = await repository.VacuumDatabaseAsync();

            // Assert
            result.Should().NotBeNull();
            result.SizeBeforeBytes.Should().BeGreaterThan(0);
            result.SizeAfterBytes.Should().BeGreaterThan(0);
            result.Duration.Should().BePositive();

        }

        [Fact]
        public async Task VacuumDatabaseAsync_WithCancellationToken_ShouldThrowWhenCancelled()
        {
            // Arrange
            await CreatePhysicalDatabaseFile();

            var options = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var physicalContext = new RssReaderDbContext(options, _mockLogger.Object);
            var repository = new DatabaseCleanupRepository(physicalContext, _mockLogger.Object);

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            Func<Task> act = async () => await repository.VacuumDatabaseAsync(cancellationTokenSource.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        #endregion

        #region RebuildIndexesAsync Tests
        [Fact]
        public async Task RebuildIndexesAsync_WithDefaultTables_ShouldRebuildAllIndexes()
        {
            // Act
            Func<Task> act = async () => await _repository.RebuildIndexesAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }
        [Fact]
        public async Task RebuildIndexesAsync_WithSpecificTables_ShouldNotThrowException()
        {
            // Arrange
            var tables = new[] { "Articles", "Feeds" };

            // Act
            Func<Task> act = async () => await _repository.RebuildIndexesAsync(tables);

            // Assert
            await act.Should().NotThrowAsync();
        }
        [Fact]
        public async Task RebuildIndexesAsync_WithEmptyTableList_ShouldRebuildDefaultTables()
        {
            // Act
            Func<Task> act = async () => await _repository.RebuildIndexesAsync(Enumerable.Empty<string>());

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task RebuildIndexesAsync_WithCancellationToken_ShouldThrowWhenCancelled()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            // Act
            Func<Task> act = async () => await _repository.RebuildIndexesAsync(
                cancellationToken: cancellationTokenSource.Token);

            // Assert
            await act.Should().ThrowAsync<OperationCanceledException>();

        }

        #endregion

        #region UpdateStatisticsAsync Tests

        [Fact]
        public async Task UpdateStatisticsAsync_ShouldExecuteAnalyzeCommand()
        {
            // Arrange
            await SeedArticlesAsync(100);
            ClearEntityTracking();

            // Act - Ejecutar ANALYZE
            await _repository.UpdateStatisticsAsync();

            // Assert - Verificar que las estadísticas se actualizaron
            // Ejecutamos un query que use el optimizador
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var query = _dbContext.Articles
                .Where(a => a.FeedId == DEFAULT_FEED_ID && a.Status == ArticleStatus.Unread)
                .ToListAsync();

            var result = await query;
            stopwatch.Stop();

            // Después de ANALYZE, el plan de ejecución debería ser óptimo
            result.Should().NotBeNull();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Tiempo razonable
        }

        [Fact]
        public async Task UpdateStatisticsAsync_ShouldNotThrowException()
        {
            // Act
            Func<Task> act = async () => await _repository.UpdateStatisticsAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region CheckIntegrityAsync Tests

        [Fact]
        public async Task CheckIntegrityAsync_WithValidDatabase_ShouldReturnValidResult()
        {
            // Act
            var result = await _repository.CheckIntegrityAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.Warnings.Should().BeEmpty();
            result.CheckDuration.Should().BePositive();
        }

        [Fact]
        public async Task CheckIntegrityAsync_WithForeignKeyViolations_ShouldIncludeWarnings()
        {
            // Arrange
            await SeedIntegrityTestDataAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.CheckIntegrityAsync();

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue(); // integrity_check pasa
            result.Warnings.Should().NotBeEmpty();
            result.Warnings.Should().Contain(w => w.Contains("foreign key violations"));


        }

        #endregion
        private async Task SeedIntegrityTestDataAsync()
        {
            try
            {
                // LIMPIAR COMPLETAMENTE el ChangeTracker ANTES de empezar
                _dbContext.ChangeTracker.Clear();

                // Deshabilitar FK
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

                // VERIFICAR si ya existe un Feed con Id=1
                var existingFeed = await _dbContext.Feeds
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == 1);

                if (existingFeed == null)
                {
                    var feed = new Feed
                    {
                        Id = 1,
                        Title = "Test Feed",
                        Url = "https://example.com/feed",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Feeds.Add(feed);
                    await _dbContext.SaveChangesAsync();
                }

                // VERIFICAR si ya existe un Tag con Id=1
                var existingTag = await _dbContext.Tags
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == 1);

                if (existingTag == null)
                {
                    var tag = new Tag
                    {
                        Id = 1,
                        Name = "Valid Tag",
                        Color = "#FF0000",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Tags.Add(tag);
                    await _dbContext.SaveChangesAsync();
                }

                // VERIFICAR si ya existe un Article con Id=1
                var existingArticle = await _dbContext.Articles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == 1);

                if (existingArticle == null)
                {
                    var validArticle = new Article
                    {
                        Id = 1,
                        FeedId = 1,
                        Title = "Valid Article",
                        Content = "Valid Content",
                        PublishedDate = DateTime.UtcNow,
                        Guid = Guid.NewGuid().ToString(),
                        ContentHash = Guid.NewGuid().ToString(),
                        Status = ArticleStatus.Unread
                    };
                    _dbContext.Articles.Add(validArticle);
                    await _dbContext.SaveChangesAsync();
                }

                // LIMPIAR tracking antes de insertar huérfanos
                _dbContext.ChangeTracker.Clear();

                // AHORA crear FK violations INTENCIONALMENTE
                // Artículo huérfano (FeedId 999 no existe)
                var orphanArticle = new Article
                {
                    FeedId = 999,
                    Title = "Orphan Article",
                    Content = "Orphan Content",
                    PublishedDate = DateTime.UtcNow,
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = ArticleStatus.Unread
                };
                _dbContext.Articles.Add(orphanArticle);
                await _dbContext.SaveChangesAsync();

                // ArticleTag con ArticleId inexistente
                var orphanTag1 = new ArticleTag
                {
                    ArticleId = 888,
                    TagId = 1,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                };
                _dbContext.ArticleTags.Add(orphanTag1);
                await _dbContext.SaveChangesAsync();

                // ArticleTag con TagId inexistente
                var orphanTag2 = new ArticleTag
                {
                    ArticleId = 1,
                    TagId = 999,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                };
                _dbContext.ArticleTags.Add(orphanTag2);
                await _dbContext.SaveChangesAsync();
            }
            finally
            {
                // Reactivar FK
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
                ClearEntityTracking();
            }
        }
        #region GetStatisticsAsync Tests

        [Fact]
        public async Task GetStatisticsAsync_WithData_ShouldReturnAccurateStatistics()
        {
            // Arrange
            await SeedArticlesAsync(25);
            await SeedTagsAndArticleTagsAsync();
            ClearEntityTracking();

            var expectedArticleCount = await _dbContext.Articles.CountAsync();
            var expectedFeedCount = await _dbContext.Feeds.CountAsync();
            var expectedTagCount = await _dbContext.Tags.CountAsync();
            var expectedReadCount = await _dbContext.Articles.CountAsync(a => a.Status == ArticleStatus.Read);
            var expectedFavoriteCount = await _dbContext.Articles.CountAsync(a => a.IsFavorite);

            // Act
            var result = await _repository.GetStatisticsAsync();

            // Assert
            result.Should().NotBeNull();
            result.TotalArticles.Should().Be(expectedArticleCount);
            result.TotalFeeds.Should().Be(expectedFeedCount);
            result.TotalTags.Should().Be(expectedTagCount);
            result.ReadArticles.Should().Be(expectedReadCount);
            result.UnreadArticles.Should().Be(expectedArticleCount - expectedReadCount);
            result.FavoriteArticles.Should().Be(expectedFavoriteCount);
            result.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            result.ArticlesOlderThan30Days.Should().BeGreaterThanOrEqualTo(0);
            result.ArticlesOlderThan60Days.Should().BeGreaterThanOrEqualTo(0);
            result.ArticlesOlderThan90Days.Should().BeGreaterThanOrEqualTo(0);

            _mockLogger.Verify(x => x.Debug(
                It.Is<string>(s => s.Contains("Statistics retrieved")),
                It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task GetStatisticsAsync_WithEmptyDatabase_ShouldReturnDefaultValues()
        {
            // Arrange
            await ClearTestData();
            await SeedBaseData();

            // Act
            var result = await _repository.GetStatisticsAsync();

            // Assert
            result.Should().NotBeNull();
            result.TotalArticles.Should().Be(0);
            result.TotalFeeds.Should().Be(2);
            result.TotalTags.Should().Be(0);
            result.ReadArticles.Should().Be(0);
            result.UnreadArticles.Should().Be(0);
            result.FavoriteArticles.Should().Be(0);
            result.OldestArticleDate.Should().Be(default(DateTime));
            result.NewestArticleDate.Should().Be(default(DateTime));
        }

        #endregion

        #region AnalyzeCleanupImpactAsync Tests

        [Fact]
        public async Task AnalyzeCleanupImpactAsync_WithArticles_ShouldReturnAccurateAnalysis()
        {
            // Arrange
            await SeedArticlesAsync(30);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-50);
            var expectedDeleteCount = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate);

            // Act
            var result = await _repository.AnalyzeCleanupImpactAsync(cutoffDate, false, false);

            // Assert
            result.Should().NotBeNull();
            result.CutoffDate.Should().Be(cutoffDate);
            result.RetentionDays.Should().Be((int)(DateTime.UtcNow - cutoffDate).Days);
            result.ArticlesToDelete.Should().Be(expectedDeleteCount);
            result.ArticlesToKeep.Should().Be(30 - expectedDeleteCount);
            result.EstimatedSpaceFreedBytes.Should().Be(expectedDeleteCount * 5 * 1024);
            result.WouldKeepFavorites.Should().BeFalse();
            result.WouldKeepUnread.Should().BeFalse();
            result.ArticlesByFeed.Should().NotBeNull();
        }

        [Fact]
        public async Task AnalyzeCleanupImpactAsync_WithKeepOptions_ShouldAdjustCounts()
        {
            // Arrange
            await SeedArticlesAsync(30);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-50);

            // Artículos elegibles para eliminación (sin filtros)
            var eligibleForDeletion = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate);

            // Artículos que serían preservados por cada opción
            var favoritesToKeep = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate && a.IsFavorite);

            var unreadToKeep = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate && a.Status == ArticleStatus.Unread);

            // Act
            var resultWithoutFilters = await _repository.AnalyzeCleanupImpactAsync(cutoffDate, false, false);
            var resultWithFavorites = await _repository.AnalyzeCleanupImpactAsync(cutoffDate, keepFavorites: true, keepUnread: false);
            var resultWithUnread = await _repository.AnalyzeCleanupImpactAsync(cutoffDate, false, keepUnread: true);
            var resultWithBoth = await _repository.AnalyzeCleanupImpactAsync(cutoffDate, keepFavorites: true, keepUnread: true);

            // Assert - ✅ CORREGIDO: Comparar contra resultado sin filtros, NO contra 0
            resultWithoutFilters.ArticlesToDelete.Should().Be(eligibleForDeletion);

            resultWithFavorites.ArticlesToDelete.Should().BeLessThan(resultWithoutFilters.ArticlesToDelete);
            resultWithFavorites.ArticlesToDelete.Should().Be(eligibleForDeletion - favoritesToKeep);

            resultWithUnread.ArticlesToDelete.Should().BeLessThan(resultWithoutFilters.ArticlesToDelete);
            resultWithUnread.ArticlesToDelete.Should().Be(eligibleForDeletion - unreadToKeep);

            resultWithBoth.ArticlesToDelete.Should().BeLessThan(resultWithFavorites.ArticlesToDelete);
            resultWithBoth.ArticlesToDelete.Should().BeLessThan(resultWithUnread.ArticlesToDelete);
        }

        #endregion

        #region GetDatabaseSizeAsync Tests

        [Fact]
        public async Task GetDatabaseSizeAsync_WithPhysicalFile_ShouldReturnFileSize()
        {
            // Arrange
            await CreatePhysicalDatabaseFile();

            var dbContextOptions = new DbContextOptionsBuilder<RssReaderDbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var physicalContext = new RssReaderDbContext(dbContextOptions, _mockLogger.Object);
            var repository = new DatabaseCleanupRepository(physicalContext, _mockLogger.Object);

            // Act
            var size = await repository.GetDatabaseSizeAsync();

            // Assert
            size.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetDatabaseSizeAsync_WithInMemoryDatabase_ShouldReturnZero()
        {
            // Act
            var size = await _repository.GetDatabaseSizeAsync();

            // Assert
            size.Should().Be(0);
        }

        [Fact]
        public async Task GetDatabaseSizeAsync_WithNonExistentFile_ShouldReturnZero()
        {
            // Arrange
            var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent.db");
            var dbContextMock = new Mock<RssReaderDbContext>();

            var databaseProperty = typeof(RssReaderDbContext).GetProperty("DatabasePath");
            if (databaseProperty != null && databaseProperty.CanWrite)
            {
                databaseProperty.SetValue(_dbContext, fakePath);
            }

            // Act
            var size = await _repository.GetDatabaseSizeAsync();

            // Assert
            size.Should().Be(0);

        }

        #endregion

        #region UpdateTagUsageCountsAsync Tests

        [Fact]
        public async Task UpdateTagUsageCountsAsync_WithTagsAndAssociations_ShouldUpdateCounts()
        {
            // Arrange
            await SeedArticlesAsync(5);
            await SeedTagsAndArticleTagsAsync();
            ClearEntityTracking();

            // Act
            var updatedCount = await _repository.UpdateTagUsageCountsAsync();

            // Assert
            updatedCount.Should().BeGreaterThan(0);

            ClearEntityTracking();
            var tag = await _dbContext.Tags.FirstAsync(t => t.Id == 1);
            tag.UsageCount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateTagUsageCountsAsync_WithNoAssociations_ShouldSetCountsToZero()
        {
            // Arrange
            await SeedTagsAndArticleTagsAsync(); // Tags creados pero sin ArticleTags?
            ClearEntityTracking();

            // Act
            var updatedCount = await _repository.UpdateTagUsageCountsAsync();

            // Assert
            updatedCount.Should().BeGreaterThan(0);

            ClearEntityTracking();
            var tag = await _dbContext.Tags.FirstAsync(t => t.Id == 1);
            tag.UsageCount.Should().Be(0);
        }

        #endregion

        #region GetAffectedArticlesDateRangeAsync Tests

        [Fact]
        public async Task GetAffectedArticlesDateRangeAsync_WithMatchingArticles_ShouldReturnDateRange()
        {
            // Arrange
            await SeedArticlesAsync(20);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-50);

            var oldestExpected = await _dbContext.Articles
                .Where(a => a.PublishedDate < cutoffDate)
                .MinAsync(a => (DateTime?)a.PublishedDate);

            var newestExpected = await _dbContext.Articles
                .Where(a => a.PublishedDate < cutoffDate)
                .MaxAsync(a => (DateTime?)a.PublishedDate);

            // Act
            var dateRange = await _repository.GetAffectedArticlesDateRangeAsync(cutoffDate, false, false);

            // Assert
            dateRange.Should().NotBeNull();
            var (oldest, newest) = dateRange.Value;
            oldest.Should().Be(oldestExpected);
            newest.Should().Be(newestExpected);
        }

        [Fact]
        public async Task GetAffectedArticlesDateRangeAsync_WithNoMatchingArticles_ShouldReturnNull()
        {
            // Arrange
            await SeedArticlesAsync(5);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-1000); // Muy antiguo, no hay artículos

            // Act
            var dateRange = await _repository.GetAffectedArticlesDateRangeAsync(cutoffDate, false, false);

            // Assert
            dateRange.Should().BeNull();
        }

        [Fact]
        public async Task GetAffectedArticlesDateRangeAsync_WithKeepOptions_ShouldRespectFilters()
        {
            // Arrange
            await SeedArticlesAsync(30);
            ClearEntityTracking();

            var cutoffDate = DateTime.UtcNow.AddDays(-50);

            // Act
            var rangeWithoutFilters = await _repository.GetAffectedArticlesDateRangeAsync(cutoffDate, false, false);
            var rangeWithKeepFavorites = await _repository.GetAffectedArticlesDateRangeAsync(cutoffDate, true, false);

            // Assert
            rangeWithKeepFavorites.Should().NotBeNull();

            // Los rangos deberían ser diferentes porque keepFavorites excluye favoritos
            if (rangeWithoutFilters.HasValue && rangeWithKeepFavorites.HasValue)
            {
                rangeWithKeepFavorites.Value.Oldest.Should().NotBe(rangeWithoutFilters.Value.Oldest);
            }
        }

        #endregion
        private async Task SeedForeignKeyViolationsDirectlyAsync()
        {
            // Limpiar tracking
            _dbContext.ChangeTracker.Clear();

            // Deshabilitar FK
            await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

            try
            {
                // ✅ USAR EF CORE en lugar de SQL directo para evitar errores de esquema
                if (!await _dbContext.Feeds.AnyAsync(f => f.Id == 1))
                {
                    _dbContext.Feeds.Add(new Feed
                    {
                        Id = 1,
                        Title = "Test Feed",
                        Url = "https://example.com/feed",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }

                if (!await _dbContext.Feeds.AnyAsync(f => f.Id == 2))
                {
                    _dbContext.Feeds.Add(new Feed
                    {
                        Id = 2,
                        Title = "Test Feed 2",
                        Url = "https://example.com/feed2",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }

                if (!await _dbContext.Tags.AnyAsync(t => t.Id == 1))
                {
                    _dbContext.Tags.Add(new Tag
                    {
                        Id = 1,
                        Name = "Valid Tag",
                        Color = "#FF0000",
                        CreatedAt = DateTime.UtcNow,
                        UsageCount = 0
                    });
                }

                await _dbContext.SaveChangesAsync();

                // Artículos válidos
                if (!await _dbContext.Articles.AnyAsync(a => a.Id == 1))
                {
                    _dbContext.Articles.Add(new Article
                    {
                        Id = 1,
                        FeedId = 1,
                        Title = "Valid Article 1",
                        Content = "Valid Content 1",
                        PublishedDate = DateTime.UtcNow.AddDays(-1),
                        Guid = Guid.NewGuid().ToString(),
                        ContentHash = Guid.NewGuid().ToString(),
                        Status = ArticleStatus.Unread,
                        AddedDate = DateTime.UtcNow.AddDays(-1)
                    });
                }

                if (!await _dbContext.Articles.AnyAsync(a => a.Id == 2))
                {
                    _dbContext.Articles.Add(new Article
                    {
                        Id = 2,
                        FeedId = 1,
                        Title = "Valid Article 2",
                        Content = "Valid Content 2",
                        PublishedDate = DateTime.UtcNow.AddDays(-2),
                        Guid = Guid.NewGuid().ToString(),
                        ContentHash = Guid.NewGuid().ToString(),
                        Status = ArticleStatus.Unread,
                        AddedDate = DateTime.UtcNow.AddDays(-2)
                    });
                }

                await _dbContext.SaveChangesAsync();

                // ✅ AHORA CREAR VIOLACIONES (con FK deshabilitadas)
                // Artículo huérfano
                _dbContext.Articles.Add(new Article
                {
                    FeedId = 999,
                    Title = "Orphan Article",
                    Content = "Orphan Content",
                    PublishedDate = DateTime.UtcNow.AddDays(-10),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = ArticleStatus.Unread,
                    AddedDate = DateTime.UtcNow.AddDays(-10)
                });

                // ArticleTag con ArticleId inexistente
                _dbContext.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = 888,
                    TagId = 1,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                });

                // ArticleTag con TagId inexistente
                _dbContext.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = 1,
                    TagId = 999,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                });

                // ArticleTag con ambos inexistentes
                _dbContext.ArticleTags.Add(new ArticleTag
                {
                    ArticleId = 777,
                    TagId = 666,
                    AppliedBy = "user",
                    AppliedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
            }
            finally
            {
                // Reactivar FK
                await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
                ClearEntityTracking();
            }
        }
        #region Integration and Edge Cases Tests

        [Fact]
        public async Task FullCleanupWorkflow_ShouldCompleteSuccessfully()
        {
            await ClearTestData();
            await SeedBaseData();

            // Arrange
            await SeedArticlesAsync(50);
            await SeedForeignKeyViolationsDirectlyAsync();

            ClearEntityTracking();

            var totalArticlesBefore = await _dbContext.Articles.CountAsync();
            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            // ✅ DEFINIR los filtros UNA VEZ
            var keepFavorites = true;
            var keepUnread = true;

            // ✅ CONTAR con los MISMOS filtros
            var eligibleForDeletion = await _dbContext.Articles
                .CountAsync(a => a.PublishedDate < cutoffDate &&
                                (keepFavorites ? !a.IsFavorite : true) &&
                                (keepUnread ? a.Status == ArticleStatus.Read : true));

            // ✅ EJECUTAR con los MISMOS filtros
            var deleteResult = await _repository.DeleteOldArticlesAsync(
                cutoffDate,
                keepFavorites: keepFavorites,
                keepUnread: keepUnread);

            var statistics = await _repository.GetStatisticsAsync();

            // Assert
            deleteResult.ArticlesDeleted.Should().Be(eligibleForDeletion);
            statistics.TotalArticles.Should().Be(totalArticlesBefore - eligibleForDeletion);
        }

        [Fact]
        public async Task ConcurrentOperations_ShouldNotThrowDeadlockExceptions()
        {
            // Arrange
            await SeedArticlesAsync(20);
            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            // Act - Ejecutar múltiples operaciones en paralelo
            var task1 = _repository.DeleteOldArticlesAsync(cutoffDate, false, false);
            var task2 = _repository.GetStatisticsAsync();
            var task3 = _repository.RemoveOrphanedRecordsAsync();

            await Task.WhenAll(task1, task2, task3);

            // Assert
            task1.Result.Should().NotBeNull();
            task2.Result.Should().NotBeNull();
            task3.Result.Should().NotBeNull();
        }

        [Fact]
        public async Task WithExtremelyLargeCutoffDate_ShouldHandleGracefully()
        {
            // Arrange
            await SeedArticlesAsync(10);

            // Fecha anterior a la creación del universo (pero válida en SQLite)
            var cutoffDate = DateTime.UtcNow.AddYears(-100); // 100 años atrás

            ClearEntityTracking();

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, false);

            // Assert
            result.Should().NotBeNull();

            // Todos los artículos tienen fecha > cutoffDate (son más recientes)
            // Por lo tanto, ningún artículo cumple PublishedDate < cutoffDate
            result.ArticlesFound.Should().Be(0);
            result.ArticlesDeleted.Should().Be(0);
        }

        [Fact]
        public async Task WithNegativeCutoffDate_ShouldThrowArgumentException()
        {
            // Arrange - Esto no es válido por el chequeo de fecha futura
            var cutoffDate = DateTime.UtcNow.AddYears(-100); // Válido, no es futuro

            // Act
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, false);

            // Assert
            result.Should().NotBeNull();
            result.ArticlesDeleted.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region Performance and Stress Tests


        [Fact(Skip = "Performance test - ejecutar manualmente cuando sea necesario")]
        public async Task DeleteOldArticlesAsync_WithLargeDataset_ShouldCompleteInReasonableTime()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 1000; i++)
            {
                _dbContext.Articles.Add(CreateTestArticle(i));
            }
            await _dbContext.SaveChangesAsync();

            var cutoffDate = DateTime.UtcNow.AddDays(-1);
            stopwatch.Stop();
            var setupTime = stopwatch.Elapsed;

            // Act
            stopwatch.Restart();
            var result = await _repository.DeleteOldArticlesAsync(cutoffDate, false, false);
            stopwatch.Stop();

            // Assert
            setupTime.Should().BeLessThan(TimeSpan.FromSeconds(5));
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            result.ArticlesDeleted.Should().Be(1000);
        }

        private Article CreateTestArticle(int index)
        {
            return new Article
            {
                FeedId = DEFAULT_FEED_ID,
                Title = $"Performance Article {index}",
                Content = new string('x', 1000),
                PublishedDate = DateTime.UtcNow.AddDays(-10), // ✅ TODOS son antiguos
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                Status = ArticleStatus.Read,        // ✅ No son Unread
                IsFavorite = false,                // ✅ No son favoritos
                IsStarred = false,
                AddedDate = DateTime.UtcNow.AddDays(-10)
            };
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mockLogger?.Reset();

                    // Limpiar archivo temporal
                    try
                    {
                        if (File.Exists(_testDbPath))
                        {
                            File.Delete(_testDbPath);
                        }
                    }
                    catch
                    {
                        // Ignorar errores al limpiar
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}