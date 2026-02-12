using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Professional test suite for the ArticleService class.
    /// Tests all public methods with various scenarios including edge cases.
    /// Utilizes the actual Article model properties for accurate testing.
    /// </summary>
    public class ArticleServiceTests
    {
        private readonly Mock<IArticleRepository> _mockArticleRepository;
        private readonly Mock<IFeedRepository> _mockFeedRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ArticleService _articleService;

        /// <summary>
        /// Initializes test dependencies before each test.
        /// Creates mock repositories and instantiates the ArticleService.
        /// </summary>
        public ArticleServiceTests()
        {
            _mockArticleRepository = new Mock<IArticleRepository>();
            _mockFeedRepository = new Mock<IFeedRepository>();
            _mockLogger = new Mock<ILogger>();
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
               .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                       .Returns(_mockLogger.Object);

            _articleService = new ArticleService(
                _mockArticleRepository.Object,
                _mockFeedRepository.Object,
                _mockLogger.Object);
        }

        #region Test Data Setup

        /// <summary>
        /// Creates a test Feed instance with realistic data.
        /// </summary>
        /// <param name="id">The feed identifier.</param>
        /// <param name="categoryId">The category identifier.</param>
        /// <returns>A configured Feed instance for testing.</returns>
        private Feed CreateTestFeed(int id = 1, int categoryId = 1)
        {
            return new Feed
            {
                Id = id,
                Title = $"Test Feed {id}",
                Url = $"https://example.com/feed{id}.rss",
                CategoryId = categoryId
            };
        }

        /// <summary>
        /// Creates a test Article instance with realistic data for unit testing.
        /// Mirrors the actual Article model properties accurately.
        /// </summary>
        /// <param name="id">The article identifier.</param>
        /// <param name="feedId">The feed identifier.</param>
        /// <param name="isRead">Whether the article is marked as read.</param>
        /// <param name="isStarred">Whether the article is starred.</param>
        /// <param name="isFavorite">Whether the article is marked as favorite.</param>
        /// <param name="isNotified">Whether the article has been notified to user.</param>
        /// <param name="processedByRules">Whether rules have processed this article.</param>
        /// <returns>A configured Article instance for testing.</returns>
        private Article CreateTestArticle(
            int id = 1,
            int feedId = 1,
            bool isRead = false,
            bool isStarred = false,
            bool isFavorite = false,
            bool isNotified = false,
            bool processedByRules = false)
        {
            var guid = $"test-guid-{id}-{feedId}";
            var publishedDate = DateTime.UtcNow.AddDays(-id);
            var addedDate = DateTime.UtcNow.AddHours(-id);

            return new Article
            {
                Id = id,
                FeedId = feedId,
                Guid = guid,
                Title = $"Test Article {id}",
                Link = $"https://example.com/article/{id}",
                Content = $"<p>Test HTML content for article {id}. This is a <strong>sample</strong> article content.</p>",
                Summary = $"This is a test summary for article {id} with some sample text.",
                Author = $"Author {id % 3 + 1}",
                PublishedDate = publishedDate,
                AddedDate = addedDate,
                ImageUrl = id % 2 == 0 ? $"https://example.com/images/article{id}.jpg" : string.Empty,
                Status = isRead ? ArticleStatus.Read : ArticleStatus.Unread,
                IsStarred = isStarred,
                Categories = id % 2 == 0 ? "Technology,Programming" : "News,Business",
                ContentHash = $"hash-{guid}",
                IsNotified = isNotified,
                ProcessedByRules = processedByRules,
                IsFavorite = isFavorite,
                // Navigation properties (will be null by default)
                Feed = null,
                Tags = new List<Tag>(),
                NotificationLogs = new List<NotificationLog>()
            };
        }

        /// <summary>
        /// Creates a list of test articles with default configurations.
        /// </summary>
        /// <param name="count">Number of articles to create.</param>
        /// <param name="feedId">The feed identifier for all articles.</param>
        /// <returns>A list of test articles.</returns>
        private List<Article> CreateTestArticles(int count, int feedId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestArticle(i, feedId))
                .ToList();
        }

        /// <summary>
        /// Creates a list of test feeds.
        /// </summary>
        /// <param name="count">Number of feeds to create.</param>
        /// <param name="categoryId">The category identifier for all feeds.</param>
        /// <returns>A list of test feeds.</returns>
        private List<Feed> CreateTestFeeds(int count, int categoryId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestFeed(i, categoryId))
                .ToList();
        }

        #endregion

        #region GetAllArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetAllArticlesAsync_WhenCalled_ReturnsAllArticles()
        {
            // Arrange
            var expectedArticles = CreateTestArticles(5);
            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetAllArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            _mockArticleRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task GetAllArticlesAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var expectedException = new DatabaseException("Connection failed");
            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<DatabaseException>(() =>
                _articleService.GetAllArticlesAsync());

            _mockArticleRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        #endregion

        #region GetArticlesByFeedAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FeedOperations")]
        [Trait("Type", "Unit")]
        public async Task GetArticlesByFeedAsync_WithValidFeedId_ReturnsArticlesWithAttachedFeed()
        {
            // Arrange
            var feedId = 1;
            var expectedFeed = CreateTestFeed(feedId);
            var expectedArticles = CreateTestArticles(3, feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(expectedArticles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(feedId))
                .ReturnsAsync(expectedFeed);

            // Act
            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article =>
            {
                Assert.Equal(expectedFeed, article.Feed);
                Assert.Equal(feedId, article.FeedId);
            });

            _mockArticleRepository.Verify(repo => repo.GetByFeedAsync(feedId, 100), Times.Once);
            _mockFeedRepository.Verify(repo => repo.GetByIdAsync(feedId), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FeedOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetArticlesByFeedAsync_WhenFeedNotFound_ReturnsArticlesWithoutFeed()
        {
            // Arrange
            var feedId = 99;
            var expectedArticles = CreateTestArticles(2, feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(expectedArticles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(feedId))
                .ReturnsAsync((Feed?)null!);

            // Act
            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.Null(article.Feed));
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FeedOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetArticlesByFeedAsync_WhenNoArticlesExist_ReturnsEmptyList()
        {
            // Arrange
            var feedId = 1;
            var expectedFeed = CreateTestFeed(feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(new List<Article>());

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(feedId))
                .ReturnsAsync(expectedFeed);

            // Act
            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region GetArticlesByCategoryAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "CategoryOperations")]
        [Trait("Type", "Unit")]
        public async Task GetArticlesByCategoryAsync_WithValidCategory_ReturnsArticlesFromAllFeeds()
        {
            // Arrange
            var categoryId = 1;
            var feeds = CreateTestFeeds(3, categoryId);
            var feedIds = feeds.Select(f => f.Id).ToList();
            var expectedArticles = CreateTestArticles(5, feedIds.First());

            _mockFeedRepository
                .Setup(repo => repo.GetByCategoryAsync(categoryId))
                .ReturnsAsync(feeds);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedsAsync(feedIds, 100))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetArticlesByCategoryAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);

            // Verify articles have feeds attached
            var feedMap = feeds.ToDictionary(f => f.Id);
            Assert.All(result, article =>
            {
                if (feedMap.ContainsKey(article.FeedId))
                {
                    Assert.Equal(feedMap[article.FeedId], article.Feed);
                }
            });

            _mockFeedRepository.Verify(repo => repo.GetByCategoryAsync(categoryId), Times.Once);
            _mockArticleRepository.Verify(repo => repo.GetByFeedsAsync(It.Is<List<int>>(ids =>
                ids.Count == feedIds.Count && ids.All(feedIds.Contains)), 100), Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "CategoryOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetArticlesByCategoryAsync_WhenNoFeedsInCategory_ReturnsEmptyList()
        {
            // Arrange
            var categoryId = 99;

            _mockFeedRepository
                .Setup(repo => repo.GetByCategoryAsync(categoryId))
                .ReturnsAsync(new List<Feed>());

            // Act
            var result = await _articleService.GetArticlesByCategoryAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.GetByFeedsAsync(It.IsAny<List<int>>(), 100), Times.Never);
        }

        #endregion

        #region GetUnreadArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task GetUnreadArticlesAsync_WhenCalled_ReturnsOnlyUnreadArticles()
        {
            // Arrange
            var mixedArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isRead: false), // Unread
                CreateTestArticle(2, 1, isRead: true),  // Read
                CreateTestArticle(3, 1, isRead: false), // Unread
                CreateTestArticle(4, 1, isRead: true)   // Read
            };

            var expectedUnreadArticles = mixedArticles.Where(a => a.Status == ArticleStatus.Unread).ToList();

            _mockArticleRepository
                .Setup(repo => repo.GetUnreadAsync(100))
                .ReturnsAsync(expectedUnreadArticles);

            // Act
            var result = await _articleService.GetUnreadArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // Should have 2 unread articles
            Assert.All(result, article => Assert.Equal(ArticleStatus.Unread, article.Status));
            _mockArticleRepository.Verify(repo => repo.GetUnreadAsync(100), Times.Once);
        }

        #endregion

        #region GetStarredArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FlagOperations")]
        [Trait("Type", "Unit")]
        public async Task GetStarredArticlesAsync_WhenCalled_ReturnsOnlyStarredArticles()
        {
            // Arrange
            var expectedStarredArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isStarred: true),
                CreateTestArticle(2, 1, isStarred: true)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetStarredAsync())
                .ReturnsAsync(expectedStarredArticles);

            // Act
            var result = await _articleService.GetStarredArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, article => Assert.True(article.IsStarred));
            _mockArticleRepository.Verify(repo => repo.GetStarredAsync(), Times.Once);
        }

        #endregion

        #region GetFavoriteArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FlagOperations")]
        [Trait("Type", "Unit")]
        public async Task GetFavoriteArticlesAsync_WhenCalled_ReturnsOnlyFavoriteArticles()
        {
            // Arrange
            var expectedFavoriteArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isFavorite: true),
                CreateTestArticle(2, 1, isFavorite: true)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetFavoritesAsync())
                .ReturnsAsync(expectedFavoriteArticles);

            // Act
            var result = await _articleService.GetFavoriteArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, article => Assert.True(article.IsFavorite));
            _mockArticleRepository.Verify(repo => repo.GetFavoritesAsync(), Times.Once);
        }

        #endregion

        #region MarkAsReadAsync Tests

        [Theory]
        [InlineData(true, 1, true)]
        [InlineData(false, 1, true)]
        [InlineData(true, 0, false)]
        [InlineData(false, 0, false)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task MarkAsReadAsync_WithDifferentParameters_ReturnsExpectedResult(
            bool isRead, int repositoryResult, bool expectedResult)
        {
            // Arrange
            var articleId = 1;
            var expectedStatus = isRead ? ArticleStatus.Read : ArticleStatus.Unread;

            _mockArticleRepository
                .Setup(repo => repo.MarkAsAsync(articleId, expectedStatus))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _articleService.MarkAsReadAsync(articleId, isRead);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsAsync(articleId, expectedStatus), Times.Once);
        }

        #endregion

        #region ToggleStarredAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FlagOperations")]
        [Trait("Type", "Unit")]
        public async Task ToggleStarredAsync_WithDifferentRepositoryResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            // Arrange
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.ToggleStarAsync(articleId))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _articleService.ToggleStarredAsync(articleId);

            // Assert
            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.ToggleStarAsync(articleId), Times.Once);
        }

        #endregion

        #region ToggleFavoriteAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "FlagOperations")]
        [Trait("Type", "Unit")]
        public async Task ToggleFavoriteAsync_WithDifferentRepositoryResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            // Arrange
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.ToggleFavoriteAsync(articleId))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _articleService.ToggleFavoriteAsync(articleId);

            // Assert
            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.ToggleFavoriteAsync(articleId), Times.Once);
        }

        #endregion

        #region SearchArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "SearchOperations")]
        [Trait("Type", "Unit")]
        public async Task SearchArticlesAsync_WithValidSearchText_ReturnsMatchingArticlesWithFeeds()
        {
            // Arrange
            var searchText = "important";
            var articles = CreateTestArticles(3);
            var feedIds = articles.Select(a => a.FeedId).Distinct().ToList();
            var feeds = feedIds.ToDictionary(
                id => id,
                id => CreateTestFeed(id));

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            foreach (var feedId in feedIds)
            {
                _mockFeedRepository
                    .Setup(repo => repo.GetByIdAsync(feedId))
                    .ReturnsAsync(feeds[feedId]);
            }

            // Act
            var result = await _articleService.SearchArticlesAsync(searchText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(articles.Count, result.Count);
            Assert.All(result, article => Assert.NotNull(article.Feed));

            _mockArticleRepository.Verify(repo => repo.SearchAsync(searchText), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "SearchOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task SearchArticlesAsync_WithNullOrWhiteSpaceSearchText_ReturnsEmptyList(string? searchText)
        {
            // Act
            var result = await _articleService.SearchArticlesAsync(searchText ?? string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.SearchAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "SearchOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task SearchArticlesAsync_WhenFeedNotFound_ReturnsArticlesWithoutFeed()
        {
            // Arrange
            var searchText = "test";
            var articles = CreateTestArticles(2);

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Feed)null!);

            // Act
            var result = await _articleService.SearchArticlesAsync(searchText);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(articles.Count, result.Count);
            Assert.All(result, article => Assert.Null(article.Feed));
        }

        #endregion

        #region DeleteOldArticlesAsync Tests

        [Theory]
        [InlineData(30, 5)]
        [InlineData(7, 10)]
        [InlineData(365, 0)]
        [InlineData(1, 15)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "MaintenanceOperations")]
        [Trait("Type", "Unit")]
        public async Task DeleteOldArticlesAsync_WithDifferentAgeParameters_ReturnsDeletedCount(
            int daysOld, int expectedCount)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _articleService.DeleteOldArticlesAsync(daysOld);

            // Assert
            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo =>
                repo.DeleteOlderThanAsync(It.Is<DateTime>(d =>
                    d <= DateTime.UtcNow.AddDays(-daysOld).AddSeconds(1) &&
                    d >= DateTime.UtcNow.AddDays(-daysOld).AddSeconds(-1))),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "MaintenanceOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task DeleteOldArticlesAsync_WithDefaultParameter_UsesDefaultDays()
        {
            // Arrange
            var expectedCount = 10;
            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _articleService.DeleteOldArticlesAsync(); // Using default parameter

            // Assert
            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo =>
                repo.DeleteOlderThanAsync(It.Is<DateTime>(d =>
                    d <= DateTime.UtcNow.AddDays(-30).AddSeconds(1) &&
                    d >= DateTime.UtcNow.AddDays(-30).AddSeconds(-1))),
                Times.Once);
        }

        #endregion

        #region GetUnreadCountAsync Tests

        [Theory]
        [InlineData(5)]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(999)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task GetUnreadCountAsync_WhenCalled_ReturnsCorrectCount(int expectedCount)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountAsync())
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _articleService.GetUnreadCountAsync();

            // Assert
            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.GetUnreadCountAsync(), Times.Once);
        }

        #endregion

        #region GetUnreadCountByFeedAsync Tests

        [Theory]
        [InlineData(1, 5)]
        [InlineData(2, 0)]
        [InlineData(3, 25)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task GetUnreadCountByFeedAsync_WithValidFeedId_ReturnsCorrectCount(
            int feedId, int expectedCount)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountByFeedAsync(feedId))
                .ReturnsAsync(expectedCount);

            // Act
            var result = await _articleService.GetUnreadCountByFeedAsync(feedId);

            // Assert
            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.GetUnreadCountByFeedAsync(feedId), Times.Once);
        }

        #endregion

        #region MarkAllAsReadAsync Tests

        [Theory]
        [InlineData(10, true)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task MarkAllAsReadAsync_WithDifferentAffectedCounts_ReturnsExpectedResult(
            int affectedCount, bool expectedResult)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.MarkAllAsReadAsync())
                .ReturnsAsync(affectedCount);

            // Act
            var result = await _articleService.MarkAllAsReadAsync();

            // Assert
            Assert.Equal(expectedResult, result);
            _mockArticleRepository.Verify(repo => repo.MarkAllAsReadAsync(), Times.Once);
        }

        #endregion

        #region MarkAsNotifiedAsync Tests

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(2, 0, false)]
        [InlineData(3, -1, false)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "NotificationOperations")]
        [Trait("Type", "Unit")]
        public async Task MarkAsNotifiedAsync_WithDifferentResults_ReturnsExpectedValue(
            int articleId, int repositoryResult, bool expected)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.MarkAsNotifiedAsync(articleId))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _articleService.MarkAsNotifiedAsync(articleId);

            // Assert
            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsNotifiedAsync(articleId), Times.Once);
        }

        #endregion

        #region MarkAsProcessedAsync Tests

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(2, 0, false)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "RuleProcessing")]
        [Trait("Type", "Unit")]
        public async Task MarkAsProcessedAsync_WithDifferentResults_ReturnsExpectedValue(
            int articleId, int repositoryResult, bool expected)
        {
            // Arrange
            _mockArticleRepository
                .Setup(repo => repo.MarkAsProcessedAsync(articleId))
                .ReturnsAsync(repositoryResult);

            // Act
            var result = await _articleService.MarkAsProcessedAsync(articleId);

            // Assert
            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsProcessedAsync(articleId), Times.Once);
        }

        #endregion

        #region GetUnprocessedArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "RuleProcessing")]
        [Trait("Type", "Unit")]
        public async Task GetUnprocessedArticlesAsync_WhenCalled_ReturnsUnprocessedArticles()
        {
            // Arrange
            var expectedArticles = new List<Article>
            {
                CreateTestArticle(1, 1, processedByRules: false),
                CreateTestArticle(2, 1, processedByRules: false)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnprocessedArticlesAsync(100))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetUnprocessedArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.False(article.ProcessedByRules));
            _mockArticleRepository.Verify(repo => repo.GetUnprocessedArticlesAsync(100), Times.Once);
        }

        #endregion

        #region GetUnnotifiedArticlesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "NotificationOperations")]
        [Trait("Type", "Unit")]
        public async Task GetUnnotifiedArticlesAsync_WhenCalled_ReturnsUnnotifiedArticles()
        {
            // Arrange
            var expectedArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isNotified: false),
                CreateTestArticle(2, 1, isNotified: false)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnnotifiedArticlesAsync(100))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetUnnotifiedArticlesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.False(article.IsNotified));
            _mockArticleRepository.Verify(repo => repo.GetUnnotifiedArticlesAsync(100), Times.Once);
        }

        #endregion

        #region GetUnreadCountsByFeedAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "StatusOperations")]
        [Trait("Type", "Unit")]
        public async Task GetUnreadCountsByFeedAsync_WhenCalled_ReturnsDictionaryOfCounts()
        {
            // Arrange
            var expectedCounts = new Dictionary<int, int>
            {
                { 1, 5 },
                { 2, 3 },
                { 3, 0 }
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountsByFeedAsync())
                .ReturnsAsync(expectedCounts);

            // Act
            var result = await _articleService.GetUnreadCountsByFeedAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCounts.Count, result.Count);
            Assert.Equal(expectedCounts[1], result[1]);
            Assert.Equal(expectedCounts[2], result[2]);
            Assert.Equal(expectedCounts[3], result[3]);
            _mockArticleRepository.Verify(repo => repo.GetUnreadCountsByFeedAsync(), Times.Once);
        }

        #endregion

        #region GetArticlesByCategoriesAsync Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "CategoryOperations")]
        [Trait("Type", "Unit")]
        public async Task GetArticlesByCategoriesAsync_WithValidCategories_ReturnsFilteredArticles()
        {
            // Arrange
            var categories = "Technology,Programming";
            var expectedArticles = CreateTestArticles(3);

            _mockArticleRepository
                .Setup(repo => repo.GetByCategoriesAsync(categories, 100))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetArticlesByCategoriesAsync(categories);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            _mockArticleRepository.Verify(repo => repo.GetByCategoriesAsync(categories, 100), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "CategoryOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetArticlesByCategoriesAsync_WithNullOrWhiteSpaceCategories_PropagatesToRepository(string? categories)
        {
            // Arrange
            var expectedArticles = new List<Article>();
            _mockArticleRepository
                .Setup(repo => repo.GetByCategoriesAsync(categories ?? string.Empty, 100))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetArticlesByCategoriesAsync(categories ?? string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.GetByCategoriesAsync(categories ?? string.Empty, 100), Times.Once);
        }

        #endregion

        #region GetPagedArticlesAsync Tests

        [Theory]
        [InlineData(1, 10, null, 10)]
        [InlineData(2, 20, true, 20)]
        [InlineData(3, 5, false, 5)]
        [InlineData(1, 100, null, 100)]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "Pagination")]
        [Trait("Type", "Unit")]
        public async Task GetPagedArticlesAsync_WithDifferentParameters_ReturnsPagedResults(
            int page, int pageSize, bool? unreadOnly, int expectedCount)
        {
            // Arrange
            var expectedArticles = CreateTestArticles(expectedCount);

            _mockArticleRepository
                .Setup(repo => repo.GetPagedAsync(
                    page,
                    pageSize,
                    It.IsAny<ArticleStatus?>()))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetPagedArticlesAsync(page, pageSize, unreadOnly);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCount, result.Count);

            _mockArticleRepository.Verify(repo =>
                repo.GetPagedAsync(
                    page,
                    pageSize,
                    It.Is<ArticleStatus?>(s =>
                        unreadOnly.HasValue && unreadOnly.Value ?
                        s == ArticleStatus.Unread :
                        s == null)),
                Times.Once);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "Pagination")]
        [Trait("Type", "EdgeCase")]
        public async Task GetPagedArticlesAsync_WithInvalidPageNumber_HandlesGracefully()
        {
            // Arrange
            var page = 0; // Invalid page number
            var pageSize = 10;
            var expectedArticles = new List<Article>();

            _mockArticleRepository
                .Setup(repo => repo.GetPagedAsync(page, pageSize, null))
                .ReturnsAsync(expectedArticles);

            // Act
            var result = await _articleService.GetPagedArticlesAsync(page, pageSize);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.GetPagedAsync(page, pageSize, null), Times.Once);
        }

        #endregion

        #region Exception Propagation Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task AnyServiceMethod_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _articleService.GetAllArticlesAsync());

            Assert.Equal(expectedException.Message, exception.Message);
        }

        #endregion

        #region Integration-Style Tests

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "Integration")]
        [Trait("Type", "Integration")]
        public async Task CompleteWorkflow_FromUnreadToReadAndStarred_ExecutesSuccessfully()
        {
            // Arrange
            var articleId = 1;
            var initialUnreadCount = 5;
            var finalUnreadCount = 4;

            _mockArticleRepository
                .SetupSequence(repo => repo.GetUnreadCountAsync())
                .ReturnsAsync(initialUnreadCount)
                .ReturnsAsync(finalUnreadCount);

            _mockArticleRepository
                .Setup(repo => repo.MarkAsAsync(articleId, ArticleStatus.Read))
                .ReturnsAsync(1);

            _mockArticleRepository
                .Setup(repo => repo.ToggleStarAsync(articleId))
                .ReturnsAsync(1);

            // Act - Simulate user workflow
            var initialCount = await _articleService.GetUnreadCountAsync();
            var markResult = await _articleService.MarkAsReadAsync(articleId, true);
            var starResult = await _articleService.ToggleStarredAsync(articleId);
            var finalCount = await _articleService.GetUnreadCountAsync();

            // Assert
            Assert.Equal(initialUnreadCount, initialCount);
            Assert.True(markResult);
            Assert.True(starResult);
            Assert.Equal(finalUnreadCount, finalCount);
            Assert.True(initialCount > finalCount);
        }

        [Fact]
        [Trait("Category", "ArticleService")]
        [Trait("Scope", "Integration")]
        [Trait("Type", "Integration")]
        public async Task ArticleLifecycle_FromCreationToDeletion_ExecutesAllOperations()
        {
            // Arrange
            var feedId = 1;
            var searchText = "important";

            // Setup article creation/search
            var articles = CreateTestArticles(3, feedId);
            var feed = CreateTestFeed(feedId);

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(feedId))
                .ReturnsAsync(feed);

            // Setup mark as read
            _mockArticleRepository
                .Setup(repo => repo.MarkAsAsync(It.IsAny<int>(), ArticleStatus.Read))
                .ReturnsAsync(1);

            // Setup toggle favorite
            _mockArticleRepository
                .Setup(repo => repo.ToggleFavoriteAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            // Setup delete old articles
            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            // Act - Simulate complete lifecycle
            var searchResults = await _articleService.SearchArticlesAsync(searchText);
            var firstArticle = searchResults.First();
            var markReadResult = await _articleService.MarkAsReadAsync(firstArticle.Id, true);
            var favoriteResult = await _articleService.ToggleFavoriteAsync(firstArticle.Id);
            var deleteResult = await _articleService.DeleteOldArticlesAsync(7);

            // Assert
            Assert.NotNull(searchResults);
            Assert.Equal(3, searchResults.Count);
            Assert.True(markReadResult);
            Assert.True(favoriteResult);
            Assert.Equal(1, deleteResult);
        }

        #endregion

        /// <summary>
        /// Custom exception for database errors in tests.
        /// </summary>
        public class DatabaseException : Exception
        {
            public DatabaseException(string message) : base(message) { }

            public DatabaseException() : base()
            {
            }

            public DatabaseException(string? message, Exception? innerException) : base(message, innerException)
            {
            }
        }
    }
}