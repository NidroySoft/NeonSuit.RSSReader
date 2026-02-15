using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;
using System.Diagnostics;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{
    [CollectionDefinition("Database_Feed")]
    public class DatabaseCollectionFeed : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_Feed")]
    public class FeedRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly FeedRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private bool _disposed;

        // Test Constants
        private const int DEFAULT_FEED_ID = 1;
        private const int DEFAULT_CATEGORY_ID = 100;
        private const string DEFAULT_FEED_TITLE = "Test Feed";
        private const string DEFAULT_FEED_URL = "https://example.com/feed.xml";
        private const int SEED_COUNT = 5;

        public FeedRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();

            _dbContext = fixture.Context;

            ClearTestData().Wait();
            SeedBaseData().Wait();

            _repository = new FeedRepository(_dbContext, _mockLogger.Object);
        }

        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext<FeedRepository>())
                .Returns(_mockLogger.Object);  // ✅ CRÍTICO - Sin esto, ForContext devuelve null

            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        #region Test Data Helpers

        private async Task ClearTestData()
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ArticleTags");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Articles");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Feeds");
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Categories");
            _dbContext.ChangeTracker.Clear();
        }

        private async Task SeedBaseData()
        {
            if (!await _dbContext.Categories.AnyAsync(c => c.Id == DEFAULT_CATEGORY_ID))
            {
                _dbContext.Categories.Add(new Category
                {
                    Id = DEFAULT_CATEGORY_ID,
                    Name = "Test Category",
                    Color = "#FF0000",
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();
        }

        private Feed CreateTestFeed(
            int id = DEFAULT_FEED_ID,
            string title = DEFAULT_FEED_TITLE,
            string url = DEFAULT_FEED_URL,
            int? categoryId = null,
            bool isActive = true,
            int failureCount = 0,
            FeedHealthStatus healthStatus = FeedHealthStatus.Healthy,
            int? retentionDays = null,
            DateTime? lastUpdated = null)
        {
            return new Feed
            {
                Id = id,
                Title = title,
                Url = url,
                Description = $"Description for {title}",
                CategoryId = categoryId,
                IsActive = isActive,
                FailureCount = failureCount,
                LastError = failureCount > 0 ? $"Error {failureCount}" : null,
                ArticleRetentionDays = retentionDays,
                LastUpdated = lastUpdated,
                UpdateFrequency = FeedUpdateFrequency.EveryHour,
                NextUpdateSchedule = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                TotalArticleCount = 0,
            };
        }

        private async Task<List<Feed>> SeedTestFeedsAsync(int count = SEED_COUNT)
        {
            var feeds = new List<Feed>();

            for (int i = 1; i <= count; i++)
            {
                var feed = CreateTestFeed(
                    id: i,
                    title: $"Test Feed {i}",
                    url: $"https://example.com/feed{i}.xml",
                    categoryId: i % 2 == 0 ? DEFAULT_CATEGORY_ID : null,
                    isActive: i % 3 != 0,
                    failureCount: i % 4,
                    healthStatus: i % 4 == 0 ? FeedHealthStatus.Error :
                                 i % 3 == 0 ? FeedHealthStatus.Invalid :
                                 FeedHealthStatus.Healthy,
                    retentionDays: i % 5 == 0 ? 30 : null,
                    lastUpdated: i % 3 == 0 ? DateTime.UtcNow.AddDays(-i) : null
                );

                _dbContext.Feeds.Add(feed);
                feeds.Add(feed);
            }

            await _dbContext.SaveChangesAsync();
            return feeds;
        }

        private async Task SeedArticlesForFeedAsync(int feedId, int count = 10)
        {
            var feed = await _dbContext.Feeds.FindAsync(feedId);
            if (feed == null) return;

            for (int i = 1; i <= count; i++)
            {
                _dbContext.Articles.Add(new Article
                {
                    FeedId = feedId,
                    Title = $"Article {i} for Feed {feedId}",
                    Content = $"Content {i}",
                    PublishedDate = DateTime.UtcNow.AddDays(-i),
                    Guid = Guid.NewGuid().ToString(),
                    ContentHash = Guid.NewGuid().ToString(),
                    Status = i % 3 == 0 ? ArticleStatus.Read : ArticleStatus.Unread,
                    AddedDate = DateTime.UtcNow.AddDays(-i)
                });
            }

            await _dbContext.SaveChangesAsync();

            // Update feed counts
            feed.TotalArticleCount = count;
            feed.UnreadCount = await _dbContext.Articles
                .CountAsync(a => a.FeedId == feedId && a.Status == ArticleStatus.Unread);
            await _dbContext.SaveChangesAsync();
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDbContext_ShouldInitializeRepository()
        {
            // Act
            var repository = new FeedRepository(_dbContext, _mockLogger.Object);

            // Assert
            repository.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => new FeedRepository(null!, _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("context");
        }

        #endregion

        #region GetWhereAsync Tests

        [Fact]
        public async Task GetWhereAsync_WithValidPredicate_ShouldReturnMatchingFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetWhereAsync(f => f.IsActive && f.CategoryId.HasValue);

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.IsActive && f.CategoryId.HasValue);
        }

        [Fact]
        public async Task GetWhereAsync_WithNoMatches_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetWhereAsync(f => f.Id == 999);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region CountWhereAsync Tests

        [Fact]
        public async Task CountWhereAsync_WithValidPredicate_ShouldReturnCorrectCount()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedCount = (await _dbContext.Feeds.ToListAsync()).Count(f => f.IsActive);

            // Act
            var result = await _repository.CountWhereAsync(f => f.IsActive);

            // Assert
            result.Should().Be(expectedCount);
        }

        [Fact]
        public async Task CountWhereAsync_WithNoMatches_ShouldReturnZero()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.CountWhereAsync(f => f.Id == 999);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region GetByCategoryAsync Tests

        [Fact]
        public async Task GetByCategoryAsync_WithValidCategoryId_ShouldReturnCategoryFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByCategoryAsync(DEFAULT_CATEGORY_ID);

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.CategoryId == DEFAULT_CATEGORY_ID);
        }

        [Fact]
        public async Task GetByCategoryAsync_WithInvalidCategoryId_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByCategoryAsync(999);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region GetUncategorizedAsync Tests

        [Fact]
        public async Task GetUncategorizedAsync_WhenCalled_ShouldReturnFeedsWithoutCategory()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUncategorizedAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.CategoryId == null);
        }

        #endregion

        #region GetActiveAsync Tests

        [Fact]
        public async Task GetActiveAsync_WhenCalled_ShouldReturnActiveFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetActiveAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.IsActive);
        }

        #endregion

        #region GetInactiveAsync Tests

        [Fact]
        public async Task GetInactiveAsync_WhenCalled_ShouldReturnInactiveFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetInactiveAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => !f.IsActive);
        }

        #endregion

        #region GetByUrlAsync Tests

        [Fact]
        public async Task GetByUrlAsync_WithExistingUrl_ShouldReturnFeed()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            var expectedUrl = $"https://example.com/feed1.xml";
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByUrlAsync(expectedUrl);

            // Assert
            result.Should().NotBeNull();
            result?.Url.Should().Be(expectedUrl);
        }

        [Fact]
        public async Task GetByUrlAsync_WithNonExistentUrl_ShouldReturnNull()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByUrlAsync("https://nonexistent.com/feed.xml");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByUrlAsync_WithNullUrl_ShouldReturnNull()
        {
            // Act
            var result = await _repository.GetByUrlAsync(null!);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region ExistsByUrlAsync Tests

        [Fact]
        public async Task ExistsByUrlAsync_WithExistingUrl_ShouldReturnTrue()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            var existingUrl = $"https://example.com/feed1.xml";
            ClearEntityTracking();

            // Act
            var result = await _repository.ExistsByUrlAsync(existingUrl);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsByUrlAsync_WithNonExistentUrl_ShouldReturnFalse()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.ExistsByUrlAsync("https://nonexistent.com/feed.xml");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetFeedsToUpdateAsync Tests

        [Fact]
        public async Task GetFeedsToUpdateAsync_WithNeverUpdatedFeeds_ShouldIncludeAllActiveFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var activeFeedsWithoutUpdate = await _dbContext.Feeds
                .Where(f => f.IsActive && f.LastUpdated == null)
                .CountAsync();

            // Act
            var result = await _repository.GetFeedsToUpdateAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.IsActive);
            result.Count.Should().BeGreaterThanOrEqualTo(activeFeedsWithoutUpdate);
        }

        [Fact]
        public async Task GetFeedsToUpdateAsync_WithRecentlyUpdatedFeeds_ShouldExcludeFeedsNotDue()
        {
            // Arrange
            await SeedTestFeedsAsync(10);

            // Set some feeds as recently updated
            var recentFeeds = await _dbContext.Feeds
                .Where(f => f.IsActive)
                .Take(3)
                .ToListAsync();

            foreach (var feed in recentFeeds)
            {
                feed.LastUpdated = DateTime.UtcNow;
                feed.UpdateFrequency = FeedUpdateFrequency.EveryHour;
            }
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.GetFeedsToUpdateAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.IsActive);

            foreach (var feed in recentFeeds)
            {
                result.Should().NotContain(f => f.Id == feed.Id);
            }
        }

        #endregion

        #region UpdateLastUpdatedAsync Tests

        [Fact]
        public async Task UpdateLastUpdatedAsync_WithExistingFeed_ShouldUpdateTimestamp()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;
            ClearEntityTracking();

            var beforeUpdate = DateTime.UtcNow.AddMinutes(-1);

            // Act
            var result = await _repository.UpdateLastUpdatedAsync(feedId);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed.Should().NotBeNull();
            updatedFeed?.LastUpdated.Should().BeAfter(beforeUpdate);
        }

        [Fact]
        public async Task UpdateLastUpdatedAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.UpdateLastUpdatedAsync(999);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region UpdateNextUpdateScheduleAsync Tests

        [Fact]
        public async Task UpdateNextUpdateScheduleAsync_WithValidParameters_ShouldUpdateSchedule()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;
            var nextUpdate = DateTime.UtcNow.AddHours(2);
            ClearEntityTracking();

            // Act
            var result = await _repository.UpdateNextUpdateScheduleAsync(feedId, nextUpdate);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed?.NextUpdateSchedule.Should().BeCloseTo(nextUpdate, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task UpdateNextUpdateScheduleAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.UpdateNextUpdateScheduleAsync(999, DateTime.UtcNow);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region GetCountByCategoryAsync Tests

        [Fact]
        public async Task GetCountByCategoryAsync_WithFeedsInCategories_ShouldReturnGroupedCounts()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedCounts = await _dbContext.Feeds
                .Where(f => f.CategoryId.HasValue)
                .GroupBy(f => f.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            // Act
            var result = await _repository.GetCountByCategoryAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedCounts);
        }

        [Fact]
        public async Task GetCountByCategoryAsync_WithNoFeedsInCategories_ShouldReturnEmptyDictionary()
        {
            // Arrange
            await ClearTestData();
            await SeedBaseData();

            // Act
            var result = await _repository.GetCountByCategoryAsync();

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region GetFailedFeedsAsync Tests

        [Fact]
        public async Task GetFailedFeedsAsync_WithDefaultThreshold_ShouldReturnFeedsAbove3Failures()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedCount = await _dbContext.Feeds
                .CountAsync(f => f.FailureCount > 3);

            // Act
            var result = await _repository.GetFailedFeedsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.FailureCount > 3);
            result.Count.Should().Be(expectedCount);
        }

        [Fact]
        public async Task GetFailedFeedsAsync_WithCustomThreshold_ShouldReturnFeedsAboveThreshold()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var threshold = 2;
            var expectedCount = await _dbContext.Feeds
                .CountAsync(f => f.FailureCount > threshold);

            // Act
            var result = await _repository.GetFailedFeedsAsync(threshold);

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.FailureCount > threshold);
            result.Count.Should().Be(expectedCount);
        }

        #endregion

        #region GetHealthyFeedsAsync Tests

        [Fact]
        public async Task GetHealthyFeedsAsync_WhenCalled_ShouldReturnFeedsWithZeroFailures()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedCount = await _dbContext.Feeds
                .CountAsync(f => f.FailureCount == 0);

            // Act
            var result = await _repository.GetHealthyFeedsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.FailureCount == 0);
            result.Count.Should().Be(expectedCount);
        }

        #endregion

        #region IncrementFailureCountAsync Tests

        [Fact]
        public async Task IncrementFailureCountAsync_WithValidFeed_ShouldIncrementAndSetError()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;
            var errorMessage = "Connection timeout";
            ClearEntityTracking();

            var feedBefore = await _repository.GetByIdAsync(feedId);
            var previousCount = feedBefore!.FailureCount;

            // Act
            var result = await _repository.IncrementFailureCountAsync(feedId, errorMessage);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed?.FailureCount.Should().Be(previousCount + 1);
            updatedFeed?.LastError.Should().Be(errorMessage);
            updatedFeed?.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task IncrementFailureCountAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.IncrementFailureCountAsync(999, "Error");

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region ResetFailureCountAsync Tests

        [Fact]
        public async Task ResetFailureCountAsync_WithValidFeed_ShouldResetCountAndClearError()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;

            // First increment to ensure non-zero
            await _repository.IncrementFailureCountAsync(feedId, "Test error");
            ClearEntityTracking();

            // Act
            var result = await _repository.ResetFailureCountAsync(feedId);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed?.FailureCount.Should().Be(0);
            updatedFeed?.LastError.Should().BeNull();
        }

        [Fact]
        public async Task ResetFailureCountAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.ResetFailureCountAsync(999);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region UpdateHealthStatusAsync Tests

        [Fact]
        public async Task UpdateHealthStatusAsync_WithValidParameters_ShouldUpdateAllFields()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;
            var lastUpdated = DateTime.UtcNow;
            var failureCount = 2;
            var lastError = "New error message";
            ClearEntityTracking();

            // Act
            var result = await _repository.UpdateHealthStatusAsync(feedId, lastUpdated, failureCount, lastError);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed?.LastUpdated.Should().BeCloseTo(lastUpdated, TimeSpan.FromSeconds(1));
            updatedFeed?.FailureCount.Should().Be(failureCount);
            updatedFeed?.LastError.Should().Be(lastError);
        }

        [Fact]
        public async Task UpdateHealthStatusAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.UpdateHealthStatusAsync(999, DateTime.UtcNow, 0, null);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region GetFeedsWithRetentionAsync Tests

        [Fact]
        public async Task GetFeedsWithRetentionAsync_WhenCalled_ShouldReturnFeedsWithRetentionSettings()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedCount = await _dbContext.Feeds
                .CountAsync(f => f.ArticleRetentionDays.HasValue && f.ArticleRetentionDays > 0);

            // Act
            var result = await _repository.GetFeedsWithRetentionAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(f => f.ArticleRetentionDays.HasValue && f.ArticleRetentionDays > 0);
            result.Count.Should().Be(expectedCount);
        }

        #endregion

        #region UpdateArticleCountsAsync Tests

        [Fact]
        public async Task UpdateArticleCountsAsync_WithValidFeed_ShouldUpdateCounts()
        {
            // Arrange - Crear un feed primero
            var feed = new Feed
            {
                Title = "Test Feed",
                Url = $"https://test{Guid.NewGuid():N}.com/rss",
                WebsiteUrl = "https://test.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            _dbContext.Feeds.Add(feed);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.UpdateArticleCountsAsync(feed.Id, 10, 5);

            // Assert
            result.Should().Be(1);
            ClearEntityTracking();

            var updatedFeed = await _repository.GetByIdAsync(feed.Id);
            updatedFeed?.TotalArticleCount.Should().Be(10);
        }

        [Fact]
        public async Task UpdateArticleCountsAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.UpdateArticleCountsAsync(999, 10, 5);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region SetActiveStatusAsync Tests

        [Fact]
        public async Task SetActiveStatusAsync_WithValidFeed_ShouldUpdateStatus()
        {
            // Arrange
            await SeedTestFeedsAsync(1);
            var feedId = DEFAULT_FEED_ID;
            ClearEntityTracking();

            // Act
            var result = await _repository.SetActiveStatusAsync(feedId, false);
            ClearEntityTracking();

            // Assert
            result.Should().Be(1);
            var updatedFeed = await _repository.GetByIdAsync(feedId);
            updatedFeed?.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task SetActiveStatusAsync_WithNonExistentFeed_ShouldReturnZero()
        {
            // Act
            var result = await _repository.SetActiveStatusAsync(999, false);

            // Assert
            result.Should().Be(0);
        }

        #endregion

        #region SearchAsync Tests

        [Fact]
        public async Task SearchAsync_WithValidSearchText_ShouldReturnMatchingFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);

            // Add a feed with specific title to search
            _dbContext.Feeds.Add(CreateTestFeed(
                id: 100,
                title: "Special Technology Feed",
                url: "https://example.com/special.xml"
            ));
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.SearchAsync("Special");

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain(f => f.Title.Contains("Special"));
        }

        [Fact]
        public async Task SearchAsync_WithEmptySearchText_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.SearchAsync("");

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task SearchAsync_WithWhitespace_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.SearchAsync("   ");

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task SearchAsync_WithNull_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.SearchAsync(null!);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region GetFeedsGroupedByCategoryAsync Tests

        [Fact]
        public async Task GetFeedsGroupedByCategoryAsync_WithFeedsInCategories_ShouldReturnGroupedDictionary()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedGroups = await _dbContext.Feeds
                .Where(f => f.CategoryId.HasValue)
                .GroupBy(f => f.CategoryId!.Value)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToListAsync();

            // Act
            var result = await _repository.GetFeedsGroupedByCategoryAsync();

            // Assert
            result.Should().NotBeNull();
            result.Keys.Should().BeEquivalentTo(expectedGroups.Select(g => g.CategoryId));

            foreach (var group in expectedGroups)
            {
                result[group.CategoryId].Should().HaveCount(group.Count);
            }
        }

        [Fact]
        public async Task GetFeedsGroupedByCategoryAsync_WithNoCategorizedFeeds_ShouldReturnEmptyDictionary()
        {
            // Arrange
            await ClearTestData();
            await SeedBaseData();

            // Create feeds without categories
            for (int i = 1; i <= 5; i++)
            {
                _dbContext.Feeds.Add(CreateTestFeed(id: i, categoryId: null));
            }
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.GetFeedsGroupedByCategoryAsync();

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region GetFeedsByCategoryAsync Tests

        [Fact]
        public async Task GetFeedsByCategoryAsync_WithValidCategoryId_ShouldReturnOrderedFeeds()
        {
            // Arrange
            await SeedTestFeedsAsync(10);
            ClearEntityTracking();

            var expectedFeeds = await _dbContext.Feeds
                .Where(f => f.CategoryId == DEFAULT_CATEGORY_ID)
                .OrderBy(f => f.Title)
                .Select(f => f.Title)
                .ToListAsync();

            // Act
            var result = await _repository.GetFeedsByCategoryAsync(DEFAULT_CATEGORY_ID);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeInAscendingOrder(f => f.Title);
            result.Select(f => f.Title).Should().Equal(expectedFeeds);
        }

        [Fact]
        public async Task GetFeedsByCategoryAsync_WithInvalidCategoryId_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetFeedsByCategoryAsync(999);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Integration and Edge Cases Tests


        [Fact]
        public async Task ConcurrentOperations_ShouldNotThrowDeadlockExceptions()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            ClearEntityTracking();

            // Act
            var task1 = _repository.GetActiveAsync();
            var task2 = _repository.GetCountByCategoryAsync();
            var task3 = _repository.GetFeedsToUpdateAsync();
            var task4 = _repository.SearchAsync("Test");

            await Task.WhenAll(task1, task2, task3, task4);

            // ✅ CORRECTO - USA LOS RESULTADOS DE LAS TAREAS YA COMPLETADAS
            var result1 = await task1;  // 👈 ASÍ SÍ
            var result2 = await task2;  // 👈 ASÍ SÍ
            var result3 = await task3;  // 👈 ASÍ SÍ
            var result4 = await task4;  // 👈 ASÍ SÍ

            // Assert
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result3.Should().NotBeNull();
            result4.Should().NotBeNull();
        }

        [Fact]
        public async Task WithExtremelyLargeSearchText_ShouldHandleGracefully()
        {
            // Arrange
            await SeedTestFeedsAsync(5);
            var hugeText = new string('a', 10000);
            ClearEntityTracking();

            // Act
            Func<Task> act = async () => await _repository.SearchAsync(hugeText);

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Performance and Stress Tests
        //(Skip = "Performance test - ejecutar manualmente cuando sea necesario")
        [Fact(Skip = "Performance test - ejecutar manualmente cuando sea necesario")]
        public async Task GetFeedsToUpdateAsync_WithLargeDataset_ShouldCompleteInReasonableTime()
        {
            // Arrange
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < 1000; i++)
            {
                _dbContext.Feeds.Add(CreateTestFeed(
                    id: i + 100,
                    title: $"Performance Feed {i}",
                    url: $"https://example.com/feed{i}.xml",
                    isActive: i % 2 == 0,
                    lastUpdated: i % 3 == 0 ? DateTime.UtcNow.AddHours(-i) : null
                ));
            }
            await _dbContext.SaveChangesAsync();
            stopwatch.Stop();
            var setupTime = stopwatch.Elapsed;
            ClearEntityTracking();

            // Act
            stopwatch.Restart();
            var result = await _repository.GetFeedsToUpdateAsync();
            stopwatch.Stop();

            // Assert
            setupTime.Should().BeLessThan(TimeSpan.FromSeconds(5));
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
            result.Should().NotBeNull();
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