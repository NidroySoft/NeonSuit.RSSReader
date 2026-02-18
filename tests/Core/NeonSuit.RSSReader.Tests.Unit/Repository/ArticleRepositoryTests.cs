using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Tests.Unit.DbContextFactory;
using Serilog;
using System.Threading;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{
    [CollectionDefinition("Database_Articles")]
    public class DatabaseCollectionArticles : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_Articles")]
    public class ArticleRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly ArticleRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private bool _disposed;

        // Test Constants
        private const int DEFAULT_FEED_ID = 1;
        private const string DEFAULT_TITLE = "Test Article";
        private const int SEED_COUNT = 5;

        public ArticleRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();

            _dbContext = fixture.Context;
            ClearArticles();

            _repository = new ArticleRepository(_dbContext, _mockLogger.Object);
        }
        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext<ArticleRepository>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        #region Test Data Helpers

        private void ClearArticles()
        {
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM ArticleTags");
            _dbContext.Database.ExecuteSqlRaw("DELETE FROM Articles");
            _dbContext.ChangeTracker.Clear();
        }

        private Article CreateTestArticle(
            int feedId = DEFAULT_FEED_ID,
            string title = DEFAULT_TITLE,
            ArticleStatus status = ArticleStatus.Unread,
            bool isStarred = false,
            bool isFavorite = false,
            bool isNotified = false,
            bool processedByRules = false,
            DateTime? publishedDate = null)
        {
            return new Article
            {
                FeedId = feedId,
                Title = title,
                Content = $"Content for {title}",
                Summary = $"Summary for {title}",
                Author = "Test Author",
                PublishedDate = publishedDate ?? DateTime.UtcNow.AddDays(-1),
                Guid = Guid.NewGuid().ToString(),
                ContentHash = Guid.NewGuid().ToString(),
                Status = status,
                IsStarred = isStarred,
                IsFavorite = isFavorite,
                IsNotified = isNotified,
                ProcessedByRules = processedByRules,
                Categories = "Technology,Programming",
                Link = $"https://example.com/{title.ToLower().Replace(" ", "-")}",
                AddedDate = DateTime.UtcNow
            };
        }

        private async Task<List<Article>> SeedTestArticlesAsync(
            int count = SEED_COUNT,
            int? feedId = null,
            ArticleStatus? defaultStatus = null)
        {
            var articles = new List<Article>();
            var targetFeedId = feedId ?? DEFAULT_FEED_ID;

            for (int i = 1; i <= count; i++)
            {
                var status = defaultStatus ?? (i % 2 == 0 ? ArticleStatus.Read : ArticleStatus.Unread);
                var article = CreateTestArticle(
                    feedId: targetFeedId,
                    title: $"Test Article {i}",
                    status: status,
                    isStarred: i % 3 == 0,
                    isFavorite: i % 4 == 0,
                    isNotified: i % 5 == 0,
                    processedByRules: i % 6 == 0,
                    publishedDate: DateTime.UtcNow.AddDays(-i)
                );

                await _repository.InsertAsync(article);
                articles.Add(article);
            }

            return articles;
        }

        private async Task EnsureFeedExists(int feedId)
        {
            if (!await _dbContext.Feeds.AnyAsync(f => f.Id == feedId))
            {
                _dbContext.Feeds.Add(new Feed
                {
                    Id = feedId,
                    Title = $"Test Feed {feedId}",
                    Url = $"https://example.com/feed{feedId}",
                    CreatedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
            }
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region Basic CRUD Tests

        [Fact]
        public async Task InsertAsync_WithValidArticle_ShouldAddToDatabase()
        {
            // Arrange
            var article = CreateTestArticle();

            // Act
            var result = await _repository.InsertAsync(article);

            // Assert
            result.Should().Be(1);
            article.Id.Should().BeGreaterThan(0);

            ClearEntityTracking();
            var retrieved = await _repository.GetByIdAsync(article.Id);
            retrieved.Should().NotBeNull();
            retrieved?.Title.Should().Be(DEFAULT_TITLE);
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingArticle_ShouldReturnArticle()
        {
            // Arrange
            var article = CreateTestArticle();
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByIdAsync(article.Id);

            // Assert
            result.Should().NotBeNull();
            result?.Id.Should().Be(article.Id);
            result?.Title.Should().Be(DEFAULT_TITLE);
        }

        [Fact]
        public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
        {
            // Act & Assert
            var result = await _repository.GetByIdAsync(999);
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_WithValidArticle_ShouldUpdateInDatabase()
        {
            // Arrange
            var article = CreateTestArticle();
            await _repository.InsertAsync(article);

            const string updatedTitle = "Updated Article Title";
            article.Title = updatedTitle;

            // Act
            var result = await _repository.UpdateAsync(article);

            // Assert
            result.Should().Be(1);

            ClearEntityTracking();
            var retrieved = await _repository.GetByIdAsync(article.Id);
            retrieved?.Title.Should().Be(updatedTitle);
        }

        [Fact]
        public async Task DeleteAsync_WithExistingArticle_ShouldRemoveFromDatabase()
        {
            // Arrange
            var article = CreateTestArticle();
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act
            var result = await _repository.DeleteAsync(article);

            // Assert
            result.Should().Be(1);
            var retrieved = await _repository.GetByIdAsync(article.Id);
            retrieved.Should().BeNull();
        }

        [Fact]
        public async Task GetAllAsync_WhenCalled_ShouldReturnAllArticles()
        {
            // Arrange
            await SeedTestArticlesAsync(3);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetAllAsync();

            // Assert
            result.Should().NotBeNull().And.HaveCount(3);
        }

        #endregion

        #region Feed-Specific Tests

        [Fact]
        public async Task GetByFeedAsync_WithValidFeedId_ShouldReturnFeedArticles()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            await EnsureFeedExists(2);

            await _repository.InsertAsync(CreateTestArticle(feedId, "Feed 1 Article 1"));
            await _repository.InsertAsync(CreateTestArticle(feedId, "Feed 1 Article 2"));
            await _repository.InsertAsync(CreateTestArticle(2, "Feed 2 Article 1"));
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByFeedAsync(feedId);

            // Assert
            result.Should().NotBeNull().And.HaveCount(2);
            result.Should().OnlyContain(a => a.FeedId == feedId);
        }

        [Fact]
        public async Task GetByFeedAsync_WithLimit_ShouldReturnLimitedResults()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            const int limit = 2;

            for (int i = 1; i <= 4; i++)
            {
                await _repository.InsertAsync(CreateTestArticle(feedId, $"Article {i}"));
            }
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByFeedAsync(feedId, limit);

            // Assert
            result.Should().HaveCount(limit);
        }

        [Fact]
        public async Task GetByFeedAsync_ShouldOrderByPublishedDateDescending()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;

            var oldest = CreateTestArticle(feedId, "Oldest");
            oldest.PublishedDate = DateTime.UtcNow.AddDays(-3);

            var newest = CreateTestArticle(feedId, "Newest");
            newest.PublishedDate = DateTime.UtcNow.AddDays(-1);

            var middle = CreateTestArticle(feedId, "Middle");
            middle.PublishedDate = DateTime.UtcNow.AddDays(-2);

            await _repository.InsertAsync(oldest);
            await _repository.InsertAsync(newest);
            await _repository.InsertAsync(middle);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByFeedAsync(feedId);

            // Assert
            result.Should().HaveCount(3);
            result[0].Title.Should().Be("Newest");
            result[1].Title.Should().Be("Middle");
            result[2].Title.Should().Be("Oldest");
        }

        [Fact]
        public async Task GetByFeedsAsync_WithMultipleFeedIds_ShouldReturnArticlesFromAllFeeds()
        {
            // Arrange
            var feedIds = new List<int> { 1, 2 };
            await EnsureFeedExists(2);
            await EnsureFeedExists(3);

            await _repository.InsertAsync(CreateTestArticle(1, "Feed 1 Article"));
            await _repository.InsertAsync(CreateTestArticle(2, "Feed 2 Article"));
            await _repository.InsertAsync(CreateTestArticle(3, "Feed 3 Article"));
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByFeedsAsync(feedIds);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(a => feedIds.Contains(a.FeedId));
        }

        #endregion

        #region Status-Based Tests

        [Fact]
        public async Task GetUnreadAsync_WhenCalled_ShouldReturnOnlyUnreadArticles()
        {
            // Arrange
            await SeedTestArticlesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUnreadAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().OnlyContain(a => a.Status == ArticleStatus.Unread);
        }

        [Fact]
        public async Task GetUnreadByFeedAsync_WithValidFeedId_ShouldReturnUnreadArticles()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            await _repository.InsertAsync(CreateTestArticle(feedId, "Unread", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(feedId, "Read", ArticleStatus.Read));
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUnreadByFeedAsync(feedId);

            // Assert
            result.Should().HaveCount(1);
            result[0].Status.Should().Be(ArticleStatus.Unread);
            result[0].Title.Should().Be("Unread");
        }

        [Fact]
        public async Task MarkAsAsync_WithValidStatus_ShouldUpdateArticleStatus()
        {
            // Arrange
            var article = CreateTestArticle(status: ArticleStatus.Unread);
            await _repository.InsertAsync(article);

            // Act
            var result = await _repository.MarkAsAsync(article.Id, ArticleStatus.Read);

            // Assert
            result.Should().Be(1);
            ClearEntityTracking();

            var updated = await _repository.GetByIdAsync(article.Id);
            updated?.Status.Should().Be(ArticleStatus.Read);
        }

        [Fact]
        public async Task MarkAsAsync_WithNonExistentArticle_ShouldReturnZero()
        {
            // Act & Assert
            var result = await _repository.MarkAsAsync(999, ArticleStatus.Read);
            result.Should().Be(0);
        }

        [Fact]
        public async Task MarkAllAsReadByFeedAsync_WithValidFeedId_ShouldMarkAllUnreadAsRead()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            for (int i = 1; i <= 3; i++)
            {
                await _repository.InsertAsync(CreateTestArticle(feedId, $"Unread {i}", ArticleStatus.Unread));
            }
            ClearEntityTracking();

            // Act
            var result = await _repository.MarkAllAsReadByFeedAsync(feedId);

            // Assert
            result.Should().Be(3);
            ClearEntityTracking();

            var articles = await _repository.GetByFeedAsync(feedId);
            articles.Should().OnlyContain(a => a.Status == ArticleStatus.Read);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_WhenCalled_ShouldMarkAllUnreadArticlesAsRead()
        {
            // Arrange
            await SeedTestArticlesAsync();
            ClearEntityTracking();

            // Act
            var result = await _repository.MarkAllAsReadAsync();

            // Assert
            result.Should().BeGreaterThan(0);
            ClearEntityTracking();

            var unreadArticles = await _repository.GetUnreadAsync();
            unreadArticles.Should().BeEmpty();
        }

        #endregion

        #region Star and Favorite Tests

        [Fact]
        public async Task GetStarredAsync_WhenCalled_ShouldReturnStarredArticles()
        {
            // Arrange
            await SeedTestArticlesAsync(6);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetStarredAsync();

            // Assert
            result.Should().OnlyContain(a => a.IsStarred);
        }

        [Fact]
        public async Task GetFavoritesAsync_WhenCalled_ShouldReturnFavoriteArticles()
        {
            // Arrange
            await SeedTestArticlesAsync(8);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetFavoritesAsync();

            // Assert
            result.Should().OnlyContain(a => a.IsFavorite);
        }

        [Fact]
        public async Task ToggleStarAsync_WhenCalled_ShouldToggleStarStatus()
        {
            // Arrange
            var article = CreateTestArticle();
            article.IsStarred = false;
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act & Assert - Toggle from false to true
            var result1 = await _repository.ToggleStarAsync(article.Id);
            result1.Should().Be(1);
            ClearEntityTracking();

            var afterFirstToggle = await _repository.GetByIdAsync(article.Id);
            afterFirstToggle?.IsStarred.Should().BeTrue();

            // Act & Assert - Toggle from true to false
            var result2 = await _repository.ToggleStarAsync(article.Id);
            result2.Should().Be(1);
            ClearEntityTracking();

            var afterSecondToggle = await _repository.GetByIdAsync(article.Id);
            afterSecondToggle?.IsStarred.Should().BeFalse();
        }

        [Fact]
        public async Task ToggleFavoriteAsync_WhenCalled_ShouldToggleFavoriteStatus()
        {
            // Arrange
            var article = CreateTestArticle();
            article.IsFavorite = false;
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act & Assert - Toggle from false to true
            var result1 = await _repository.ToggleFavoriteAsync(article.Id);
            result1.Should().Be(1);
            ClearEntityTracking();

            var afterFirstToggle = await _repository.GetByIdAsync(article.Id);
            afterFirstToggle?.IsFavorite.Should().BeTrue();

            // Act & Assert - Toggle from true to false
            var result2 = await _repository.ToggleFavoriteAsync(article.Id);
            result2.Should().Be(1);
            ClearEntityTracking();

            var afterSecondToggle = await _repository.GetByIdAsync(article.Id);
            afterSecondToggle?.IsFavorite.Should().BeFalse();
        }

        #endregion

        #region GUID and Hash Tests

        [Fact]
        public async Task GetByGuidAsync_WithExistingGuid_ShouldReturnArticle()
        {
            // Arrange
            var testGuid = Guid.NewGuid().ToString();
            var article = CreateTestArticle();
            article.Guid = testGuid;
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByGuidAsync(testGuid);

            // Assert
            result.Should().NotBeNull();
            result?.Guid.Should().Be(testGuid);
            result?.Title.Should().Be(DEFAULT_TITLE);
        }

        [Fact]
        public async Task GetByGuidAsync_WithNonExistentGuid_ShouldReturnNull()
        {
            // Act & Assert
            var result = await _repository.GetByGuidAsync("non-existent-guid");
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByContentHashAsync_WithExistingHash_ShouldReturnArticle()
        {
            // Arrange
            var testHash = Guid.NewGuid().ToString();
            var article = CreateTestArticle();
            article.ContentHash = testHash;
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetByContentHashAsync(testHash);

            // Assert
            result.Should().NotBeNull();
            result?.ContentHash.Should().Be(testHash);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExistsByGuidAsync_ShouldReturnCorrectExistence(bool shouldExist)
        {
            // Arrange
            var guid = Guid.NewGuid().ToString();
            if (shouldExist)
            {
                var article = CreateTestArticle();
                article.Guid = guid;
                await _repository.InsertAsync(article);
            }
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.ExistsByGuidAsync(guid);
            result.Should().Be(shouldExist);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ExistsByContentHashAsync_ShouldReturnCorrectExistence(bool shouldExist)
        {
            // Arrange
            var hash = Guid.NewGuid().ToString();
            if (shouldExist)
            {
                var article = CreateTestArticle();
                article.ContentHash = hash;
                await _repository.InsertAsync(article);
            }
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.ExistsByContentHashAsync(hash);
            result.Should().Be(shouldExist);
        }

        #endregion

        #region Count Tests

        [Fact]
        public async Task GetUnreadCountAsync_WhenCalled_ShouldReturnCorrectCount()
        {
            // Arrange
            await SeedTestArticlesAsync();
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetUnreadCountAsync();
            result.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetUnreadCountByFeedAsync_WithValidFeedId_ShouldReturnCorrectCount()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            await EnsureFeedExists(2);

            await _repository.InsertAsync(CreateTestArticle(feedId, "Unread 1", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(feedId, "Unread 2", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(feedId, "Read", ArticleStatus.Read));
            await _repository.InsertAsync(CreateTestArticle(2, "Other Feed", ArticleStatus.Unread));
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetUnreadCountByFeedAsync(feedId);
            result.Should().Be(2);
        }

        [Fact]
        public async Task GetUnreadCountsByFeedAsync_WhenCalled_ShouldReturnGroupedCounts()
        {
            // Arrange
            await EnsureFeedExists(2);
            await EnsureFeedExists(3);

            // Feed 1: 2 unread
            await _repository.InsertAsync(CreateTestArticle(1, "Feed 1 Unread 1", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(1, "Feed 1 Unread 2", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(1, "Feed 1 Read", ArticleStatus.Read));

            // Feed 2: 1 unread
            await _repository.InsertAsync(CreateTestArticle(2, "Feed 2 Unread", ArticleStatus.Unread));
            await _repository.InsertAsync(CreateTestArticle(2, "Feed 2 Read", ArticleStatus.Read));

            // Feed 3: 0 unread
            await _repository.InsertAsync(CreateTestArticle(3, "Feed 3 Read", ArticleStatus.Read));
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUnreadCountsByFeedAsync();

            // Assert
            result.Should().NotBeNull().And.HaveCount(2);
            result.Should().ContainKey(1).WhoseValue.Should().Be(2);
            result.Should().ContainKey(2).WhoseValue.Should().Be(1);
            result.Should().NotContainKey(3);
        }

        #endregion

        #region Search Tests

        [Fact]
        public async Task SearchAsync_WithMatchingText_ShouldReturnArticles()
        {
            // Arrange
            const string searchText = "special";

            await _repository.InsertAsync(CreateTestArticle(1, "Special Article Title"));
            await _repository.InsertAsync(CreateTestArticle(1, "Normal Article"));

            var articleWithContent = CreateTestArticle(1, "Another Article");
            articleWithContent.Content = "This has special content inside";
            await _repository.InsertAsync(articleWithContent);
            ClearEntityTracking();

            // Act
            var result = await _repository.SearchAsync(searchText);

            // Assert
            result.Should().Contain(a =>
                a.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                a.Content.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task SearchAsync_WithEmptyText_ShouldReturnEmptyList()
        {
            // Arrange
            await SeedTestArticlesAsync(3);
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.SearchAsync("");
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Theory]
        [InlineData("Test Author", "Author")]
        [InlineData("Technology", "Categories")]
        [InlineData("Summary for", "Summary")]
        public async Task SearchAsync_ShouldSearchInMultipleFields(string searchText, string expectedField)
        {
            // Arrange
            var article = CreateTestArticle();
            await _repository.InsertAsync(article);
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.SearchAsync(searchText);
            result.Should().HaveCount(1);
        }

        #endregion

        #region Cleanup and Processing Tests

        [Fact]
        public async Task DeleteOlderThanAsync_WithOldArticles_ShouldDeleteNonStarredNonFavorite()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow;

            // Old article (should be deleted)
            var oldArticle = CreateTestArticle();
            oldArticle.PublishedDate = cutoffDate.AddDays(-10);
            await _repository.InsertAsync(oldArticle);

            // Old but starred (should NOT be deleted)
            var oldStarred = CreateTestArticle();
            oldStarred.PublishedDate = cutoffDate.AddDays(-10);
            oldStarred.IsStarred = true;
            await _repository.InsertAsync(oldStarred);

            // Old but favorite (should NOT be deleted)
            var oldFavorite = CreateTestArticle();
            oldFavorite.PublishedDate = cutoffDate.AddDays(-10);
            oldFavorite.IsFavorite = true;
            await _repository.InsertAsync(oldFavorite);

            // New article (should NOT be deleted)
            var newArticle = CreateTestArticle();
            newArticle.PublishedDate = cutoffDate.AddDays(1);
            await _repository.InsertAsync(newArticle);
            ClearEntityTracking();

            // Act
            var result = await _repository.DeleteOlderThanAsync(cutoffDate);

            // Assert
            result.Should().Be(1);
            ClearEntityTracking();

            var allArticles = await _repository.GetAllAsync();
            allArticles.Should().HaveCount(3);
        }

        [Fact]
        public async Task DeleteByFeedAsync_WithValidFeedId_ShouldDeleteAllFeedArticles()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            await EnsureFeedExists(2);

            await _repository.InsertAsync(CreateTestArticle(feedId, "Feed 1 Article 1"));
            await _repository.InsertAsync(CreateTestArticle(feedId, "Feed 1 Article 2"));
            await _repository.InsertAsync(CreateTestArticle(2, "Feed 2 Article"));
            ClearEntityTracking();

            // Act
            var result = await _repository.DeleteByFeedAsync(feedId);

            // Assert
            result.Should().Be(2);
            ClearEntityTracking();

            var remaining = await _repository.GetAllAsync();
            remaining.Should().HaveCount(1);
            remaining[0].FeedId.Should().Be(2);
        }

        #endregion

        #region Processing and Notification Tests

        [Fact]
        public async Task GetUnprocessedArticlesAsync_WhenCalled_ShouldReturnUnprocessedArticles()
        {
            // Arrange
            var unprocessed = CreateTestArticle();
            unprocessed.ProcessedByRules = false;
            await _repository.InsertAsync(unprocessed);

            var processed = CreateTestArticle();
            processed.ProcessedByRules = true;
            await _repository.InsertAsync(processed);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUnprocessedArticlesAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].ProcessedByRules.Should().BeFalse();
        }

        [Fact]
        public async Task GetUnnotifiedArticlesAsync_WhenCalled_ShouldReturnUnnotifiedArticles()
        {
            // Arrange
            var unnotified = CreateTestArticle();
            unnotified.IsNotified = false;
            await _repository.InsertAsync(unnotified);

            var notified = CreateTestArticle();
            notified.IsNotified = true;
            await _repository.InsertAsync(notified);
            ClearEntityTracking();

            // Act
            var result = await _repository.GetUnnotifiedArticlesAsync();

            // Assert
            result.Should().HaveCount(1);
            result[0].IsNotified.Should().BeFalse();
        }

        [Fact]
        public async Task MarkAsProcessedAsync_WithValidArticle_ShouldMarkAsProcessed()
        {
            // Arrange
            var article = CreateTestArticle();
            article.ProcessedByRules = false;
            await _repository.InsertAsync(article);

            // Act
            var result = await _repository.MarkAsProcessedAsync(article.Id);

            // Assert
            result.Should().Be(1);
            ClearEntityTracking();

            var updated = await _repository.GetByIdAsync(article.Id);
            updated?.ProcessedByRules.Should().BeTrue();
        }

        [Fact]
        public async Task MarkAsNotifiedAsync_WithValidArticle_ShouldMarkAsNotified()
        {
            // Arrange
            var article = CreateTestArticle();
            article.IsNotified = false;
            await _repository.InsertAsync(article);

            // Act
            var result = await _repository.MarkAsNotifiedAsync(article.Id);

            // Assert
            result.Should().Be(1);
            ClearEntityTracking();

            var updated = await _repository.GetByIdAsync(article.Id);
            updated?.IsNotified.Should().BeTrue();
        }

        [Fact]
        public async Task BulkMarkAsProcessedAsync_WithMultipleArticles_ShouldMarkAllAsProcessed()
        {
            // Arrange
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var article = CreateTestArticle();
                article.ProcessedByRules = false;
                await _repository.InsertAsync(article);
                articles.Add(article);
            }

            var articleIds = articles.Select(a => a.Id).ToList();
            ClearEntityTracking();

            // Act
            var result = await _repository.BulkMarkAsProcessedAsync(articleIds);

            // Assert
            result.Should().Be(3);
            ClearEntityTracking();

            var allArticles = await _repository.GetAllAsync();
            allArticles.Should().OnlyContain(a => a.ProcessedByRules);
        }

        [Fact]
        public async Task BulkMarkAsNotifiedAsync_WithMultipleArticles_ShouldMarkAllAsNotified()
        {
            // Arrange
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var article = CreateTestArticle();
                article.IsNotified = false;
                await _repository.InsertAsync(article);
                articles.Add(article);
            }

            var articleIds = articles.Select(a => a.Id).ToList();
            ClearEntityTracking();

            // Act
            var result = await _repository.BulkMarkAsNotifiedAsync(articleIds);

            // Assert
            result.Should().Be(3);
            ClearEntityTracking();

            var allArticles = await _repository.GetAllAsync();
            allArticles.Should().OnlyContain(a => a.IsNotified);
        }

        #endregion

        #region Paging Tests

        [Fact]
        public async Task GetPagedAsync_WithValidParameters_ShouldReturnPagedResults()
        {
            // Arrange
            for (int i = 1; i <= 10; i++)
            {
                var article = CreateTestArticle(1, $"Article {i}");
                article.PublishedDate = DateTime.UtcNow.AddDays(-i);
                await _repository.InsertAsync(article);
            }
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetPagedAsync(page: 2, pageSize: 3);
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task GetPagedAsync_WithStatusFilter_ShouldReturnFilteredPagedResults()
        {
            // Arrange
            for (int i = 1; i <= 5; i++)
            {
                var article = CreateTestArticle(1, $"Article {i}");
                article.Status = i % 2 == 0 ? ArticleStatus.Read : ArticleStatus.Unread;
                await _repository.InsertAsync(article);
            }
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetPagedAsync(page: 1, pageSize: 10, status: ArticleStatus.Unread);
            result.Should().OnlyContain(a => a.Status == ArticleStatus.Unread);
        }

        [Fact]
        public async Task GetPagedByFeedAsync_WithValidFeedId_ShouldReturnPagedFeedResults()
        {
            // Arrange
            const int feedId = DEFAULT_FEED_ID;
            await EnsureFeedExists(2);

            for (int i = 1; i <= 5; i++)
            {
                await _repository.InsertAsync(CreateTestArticle(feedId, $"Feed 1 Article {i}"));
            }

            for (int i = 1; i <= 3; i++)
            {
                await _repository.InsertAsync(CreateTestArticle(2, $"Feed 2 Article {i}"));
            }
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetPagedByFeedAsync(feedId, page: 2, pageSize: 2);
            result.Should().HaveCount(2);
            result.Should().OnlyContain(a => a.FeedId == feedId);
        }

        #endregion

        #region Bulk Operations Tests

        [Fact]
        public async Task InsertAllAsync_WithMultipleArticles_ShouldInsertAll()
        {
            // Arrange
            var articles = new List<Article>();
            for (int i = 1; i <= 5; i++)
            {
                articles.Add(CreateTestArticle(1, $"Bulk Article {i}"));
            }

            // Act
            var result = await _repository.InsertAllAsync(articles);

            // Assert
            result.Should().Be(5);
            ClearEntityTracking();

            var allArticles = await _repository.GetAllAsync();
            allArticles.Should().HaveCount(5);
        }

        [Fact]
        public async Task UpdateAllAsync_WithMultipleArticles_ShouldUpdateAll()
        {
            // Arrange
            var articles = new List<Article>();
            for (int i = 1; i <= 3; i++)
            {
                var article = CreateTestArticle(1, $"Original {i}");
                await _repository.InsertAsync(article);
                articles.Add(article);
            }

            foreach (var article in articles)
            {
                article.Title = $"Updated {article.Title}";
            }

            // Act
            var result = await _repository.UpdateAllAsync(articles);

            // Assert
            result.Should().Be(3);
            ClearEntityTracking();

            var allArticles = await _repository.GetAllAsync();
            allArticles.Should().OnlyContain(a => a.Title.StartsWith("Updated"));
        }

        #endregion

        #region Helper Method Tests

        [Fact]
        public async Task GetWhereAsync_WithPredicate_ShouldReturnFilteredArticles()
        {
            // Arrange
            await SeedTestArticlesAsync();
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetWhereAsync(a => a.Title.Contains("3"));
            result.Should().HaveCount(1);
            result[0].Title.Should().Be("Test Article 3");
        }

        [Fact]
        public async Task CountWhereAsync_WithPredicate_ShouldReturnCorrectCount()
        {
            // Arrange
            await SeedTestArticlesAsync();
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.CountWhereAsync(a => a.Status == ArticleStatus.Unread);
            result.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetByCategoriesAsync_WithMatchingCategories_ShouldReturnArticles()
        {
            // Arrange
            var techArticle = CreateTestArticle();
            techArticle.Categories = "Technology,Programming";
            await _repository.InsertAsync(techArticle);

            var newsArticle = CreateTestArticle();
            newsArticle.Categories = "News,Politics";
            await _repository.InsertAsync(newsArticle);
            ClearEntityTracking();

            // Act & Assert
            var result = await _repository.GetByCategoriesAsync("Technology");
            result.Should().HaveCount(1);
            result[0].Categories.Should().Contain("Technology");
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

    public class DatabaseFixture : IDisposable
    {
        public TestDbContextFactory Factory { get; }
        public RssReaderDbContext Context { get; }

        public DatabaseFixture()
        {
            Factory = new TestDbContextFactory();
            Context = Factory.CreateContext();

            // Eliminar el lock y el static _initialized
            // Cada fixture debe inicializar SU PROPIA base de datos
            SeedInitialData();
        }

        private void SeedInitialData()
        {
            if (!Context.Feeds.Any())
            {
                Context.Feeds.Add(new Feed
                {
                    Id = 1,
                    Title = "Test Feed",
                    Url = "https://example.com/feed",
                    CreatedAt = DateTime.UtcNow
                });
                Context.SaveChanges();
            }
        }

        public void Dispose()
        {
            Context?.Dispose();
            Factory?.Dispose();
        }
    }
}