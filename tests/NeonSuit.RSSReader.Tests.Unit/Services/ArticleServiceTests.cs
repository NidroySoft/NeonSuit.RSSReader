using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    public class ArticleServiceTests
    {
        private readonly Mock<IArticleRepository> _mockArticleRepository;
        private readonly Mock<IFeedRepository> _mockFeedRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ArticleService _articleService;

        public ArticleServiceTests()
        {
            _mockArticleRepository = new Mock<IArticleRepository>();
            _mockFeedRepository = new Mock<IFeedRepository>();
            _mockLogger = new Mock<ILogger>();

            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _articleService = new ArticleService(
                _mockArticleRepository.Object,
                _mockFeedRepository.Object,
                _mockLogger.Object);
        }

        #region Test Data Setup

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

            return new Article
            {
                Id = id,
                FeedId = feedId,
                Guid = guid,
                Title = $"Test Article {id}",
                Link = $"https://example.com/article/{id}",
                Content = $"<p>Test content for article {id}</p>",
                Summary = $"Test summary for article {id}",
                Author = $"Author {id % 3 + 1}",
                PublishedDate = DateTime.UtcNow.AddDays(-id),
                AddedDate = DateTime.UtcNow.AddHours(-id),
                ImageUrl = id % 2 == 0 ? $"https://example.com/images/article{id}.jpg" : string.Empty,
                Status = isRead ? ArticleStatus.Read : ArticleStatus.Unread,
                IsStarred = isStarred,
                Categories = id % 2 == 0 ? "Technology,Programming" : "News,Business",
                ContentHash = $"hash-{guid}",
                IsNotified = isNotified,
                ProcessedByRules = processedByRules,
                IsFavorite = isFavorite,
                Feed = null,
                Tags = new List<Tag>(),
                NotificationLogs = new List<NotificationLog>()
            };
        }

        private List<Article> CreateTestArticles(int count, int feedId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestArticle(i, feedId))
                .ToList();
        }

        private List<Feed> CreateTestFeeds(int count, int categoryId = 1)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateTestFeed(i, categoryId))
                .ToList();
        }

        #endregion

        #region GetAllArticlesAsync Tests

        [Fact]
        public async Task GetAllArticlesAsync_WhenCalled_ReturnsAllArticles()
        {
            var expectedArticles = CreateTestArticles(5);

            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetAllArticlesAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            _mockArticleRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllArticlesAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            var expectedException = new InvalidOperationException("Connection failed");

            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ThrowsAsync(expectedException);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _articleService.GetAllArticlesAsync());

            _mockArticleRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
        }

        #endregion

        #region GetArticlesByFeedAsync Tests

        [Fact]
        public async Task GetArticlesByFeedAsync_WithValidFeedId_ReturnsArticlesWithAttachedFeed()
        {
            var feedId = 1;
            var expectedFeed = CreateTestFeed(feedId);
            var expectedArticles = CreateTestArticles(3, feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(expectedArticles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedFeed);

            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.Equal(expectedFeed, article.Feed));

            _mockArticleRepository.Verify(repo => repo.GetByFeedAsync(feedId, 100), Times.Once);
            _mockFeedRepository.Verify(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Once);
        }

        [Fact]
        public async Task GetArticlesByFeedAsync_WhenFeedNotFound_ReturnsArticlesWithoutFeed()
        {
            var feedId = 99;
            var expectedArticles = CreateTestArticles(2, feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(expectedArticles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.Null(article.Feed));
        }

        [Fact]
        public async Task GetArticlesByFeedAsync_WhenNoArticlesExist_ReturnsEmptyList()
        {
            var feedId = 1;
            var expectedFeed = CreateTestFeed(feedId);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedAsync(feedId, 100))
                .ReturnsAsync(new List<Article>());

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(expectedFeed);

            var result = await _articleService.GetArticlesByFeedAsync(feedId);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region GetArticlesByCategoryAsync Tests

        [Fact]
        public async Task GetArticlesByCategoryAsync_WithValidCategory_ReturnsArticlesFromAllFeeds()
        {
            var categoryId = 1;
            var feeds = CreateTestFeeds(3, categoryId);
            var feedIds = feeds.Select(f => f.Id).ToList();
            var expectedArticles = CreateTestArticles(5, feedIds.First());

            _mockFeedRepository
                .Setup(repo => repo.GetByCategoryAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            _mockArticleRepository
                .Setup(repo => repo.GetByFeedsAsync(It.IsAny<List<int>>(), 100))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetArticlesByCategoryAsync(categoryId);

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);

            _mockFeedRepository.Verify(repo => repo.GetByCategoryAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Once);
            _mockArticleRepository.Verify(repo => repo.GetByFeedsAsync(It.IsAny<List<int>>(), 100), Times.Once);
        }

        [Fact]
        public async Task GetArticlesByCategoryAsync_WhenNoFeedsInCategory_ReturnsEmptyList()
        {
            var categoryId = 99;

            _mockFeedRepository
                .Setup(repo => repo.GetByCategoryAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(new List<Feed>());

            var result = await _articleService.GetArticlesByCategoryAsync(categoryId);

            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.GetByFeedsAsync(It.IsAny<List<int>>(), 100), Times.Never);
        }

        #endregion

        #region GetUnreadArticlesAsync Tests

        [Fact]
        public async Task GetUnreadArticlesAsync_WhenCalled_ReturnsOnlyUnreadArticles()
        {
            var expectedUnreadArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isRead: false),
                CreateTestArticle(3, 1, isRead: false)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnreadAsync(100))
                .ReturnsAsync(expectedUnreadArticles);

            var result = await _articleService.GetUnreadArticlesAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, article => Assert.Equal(ArticleStatus.Unread, article.Status));
            _mockArticleRepository.Verify(repo => repo.GetUnreadAsync(100), Times.Once);
        }

        #endregion

        #region GetStarredArticlesAsync Tests

        [Fact]
        public async Task GetStarredArticlesAsync_WhenCalled_ReturnsOnlyStarredArticles()
        {
            var expectedStarredArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isStarred: true),
                CreateTestArticle(2, 1, isStarred: true)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetStarredAsync())
                .ReturnsAsync(expectedStarredArticles);

            var result = await _articleService.GetStarredArticlesAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.All(result, article => Assert.True(article.IsStarred));
            _mockArticleRepository.Verify(repo => repo.GetStarredAsync(), Times.Once);
        }

        #endregion

        #region GetFavoriteArticlesAsync Tests

        [Fact]
        public async Task GetFavoriteArticlesAsync_WhenCalled_ReturnsOnlyFavoriteArticles()
        {
            var expectedFavoriteArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isFavorite: true),
                CreateTestArticle(2, 1, isFavorite: true)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetFavoritesAsync())
                .ReturnsAsync(expectedFavoriteArticles);

            var result = await _articleService.GetFavoriteArticlesAsync();

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
        public async Task MarkAsReadAsync_WithDifferentParameters_ReturnsExpectedResult(
            bool isRead, int repositoryResult, bool expectedResult)
        {
            var articleId = 1;
            var expectedStatus = isRead ? ArticleStatus.Read : ArticleStatus.Unread;

            _mockArticleRepository
                .Setup(repo => repo.MarkAsAsync(articleId, expectedStatus))
                .ReturnsAsync(repositoryResult);

            var result = await _articleService.MarkAsReadAsync(articleId, isRead);

            Assert.Equal(expectedResult, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsAsync(articleId, expectedStatus), Times.Once);
        }

        #endregion

        #region ToggleStarredAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public async Task ToggleStarredAsync_WithDifferentRepositoryResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.ToggleStarAsync(articleId))
                .ReturnsAsync(repositoryResult);

            var result = await _articleService.ToggleStarredAsync(articleId);

            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.ToggleStarAsync(articleId), Times.Once);
        }

        #endregion

        #region ToggleFavoriteAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public async Task ToggleFavoriteAsync_WithDifferentRepositoryResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.ToggleFavoriteAsync(articleId))
                .ReturnsAsync(repositoryResult);

            var result = await _articleService.ToggleFavoriteAsync(articleId);

            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.ToggleFavoriteAsync(articleId), Times.Once);
        }

        #endregion

        #region SearchArticlesAsync Tests

        [Fact]
        public async Task SearchArticlesAsync_WithValidSearchText_ReturnsMatchingArticlesWithFeeds()
        {
            var searchText = "important";
            var articles = CreateTestArticles(3);
            var feed = CreateTestFeed(1);

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(feed);

            var result = await _articleService.SearchArticlesAsync(searchText);

            Assert.NotNull(result);
            Assert.Equal(articles.Count, result.Count);
            Assert.All(result, article => Assert.NotNull(article.Feed));

            _mockArticleRepository.Verify(repo => repo.SearchAsync(searchText), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SearchArticlesAsync_WithNullOrWhiteSpaceSearchText_ReturnsEmptyList(string? searchText)
        {
            var result = await _articleService.SearchArticlesAsync(searchText ?? string.Empty);

            Assert.NotNull(result);
            Assert.Empty(result);
            _mockArticleRepository.Verify(repo => repo.SearchAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SearchArticlesAsync_WhenFeedNotFound_ReturnsArticlesWithoutFeed()
        {
            var searchText = "test";
            var articles = CreateTestArticles(2);

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            var result = await _articleService.SearchArticlesAsync(searchText);

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
        public async Task DeleteOldArticlesAsync_WithDifferentAgeParameters_ReturnsDeletedCount(
            int daysOld, int expectedCount)
        {
            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expectedCount);

            var result = await _articleService.DeleteOldArticlesAsync(daysOld);

            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_WithDefaultParameter_UsesDefaultDays()
        {
            var expectedCount = 10;

            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(expectedCount);

            var result = await _articleService.DeleteOldArticlesAsync();

            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()), Times.Once);
        }

        #endregion

        #region GetUnreadCountAsync Tests

        [Theory]
        [InlineData(5)]
        [InlineData(0)]
        [InlineData(100)]
        public async Task GetUnreadCountAsync_WhenCalled_ReturnsCorrectCount(int expectedCount)
        {
            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountAsync())
                .ReturnsAsync(expectedCount);

            var result = await _articleService.GetUnreadCountAsync();

            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.GetUnreadCountAsync(), Times.Once);
        }

        #endregion

        #region GetUnreadCountByFeedAsync Tests

        [Theory]
        [InlineData(1, 5)]
        [InlineData(2, 0)]
        [InlineData(3, 25)]
        public async Task GetUnreadCountByFeedAsync_WithValidFeedId_ReturnsCorrectCount(
            int feedId, int expectedCount)
        {
            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountByFeedAsync(feedId))
                .ReturnsAsync(expectedCount);

            var result = await _articleService.GetUnreadCountByFeedAsync(feedId);

            Assert.Equal(expectedCount, result);
            _mockArticleRepository.Verify(repo => repo.GetUnreadCountByFeedAsync(feedId), Times.Once);
        }

        #endregion

        #region MarkAllAsReadAsync Tests

        [Theory]
        [InlineData(10, true)]
        [InlineData(0, false)]
        public async Task MarkAllAsReadAsync_WithDifferentAffectedCounts_ReturnsExpectedResult(
            int affectedCount, bool expectedResult)
        {
            _mockArticleRepository
                .Setup(repo => repo.MarkAllAsReadAsync())
                .ReturnsAsync(affectedCount);

            var result = await _articleService.MarkAllAsReadAsync();

            Assert.Equal(expectedResult, result);
            _mockArticleRepository.Verify(repo => repo.MarkAllAsReadAsync(), Times.Once);
        }

        #endregion

        #region MarkAsNotifiedAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public async Task MarkAsNotifiedAsync_WithDifferentResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.MarkAsNotifiedAsync(articleId))
                .ReturnsAsync(repositoryResult);

            var result = await _articleService.MarkAsNotifiedAsync(articleId);

            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsNotifiedAsync(articleId), Times.Once);
        }

        #endregion

        #region MarkAsProcessedAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public async Task MarkAsProcessedAsync_WithDifferentResults_ReturnsExpectedValue(
            int repositoryResult, bool expected)
        {
            var articleId = 1;

            _mockArticleRepository
                .Setup(repo => repo.MarkAsProcessedAsync(articleId))
                .ReturnsAsync(repositoryResult);

            var result = await _articleService.MarkAsProcessedAsync(articleId);

            Assert.Equal(expected, result);
            _mockArticleRepository.Verify(repo => repo.MarkAsProcessedAsync(articleId), Times.Once);
        }

        #endregion

        #region GetUnprocessedArticlesAsync Tests

        [Fact]
        public async Task GetUnprocessedArticlesAsync_WhenCalled_ReturnsUnprocessedArticles()
        {
            var expectedArticles = new List<Article>
            {
                CreateTestArticle(1, 1, processedByRules: false),
                CreateTestArticle(2, 1, processedByRules: false)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnprocessedArticlesAsync(100))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetUnprocessedArticlesAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.False(article.ProcessedByRules));
            _mockArticleRepository.Verify(repo => repo.GetUnprocessedArticlesAsync(100), Times.Once);
        }

        #endregion

        #region GetUnnotifiedArticlesAsync Tests

        [Fact]
        public async Task GetUnnotifiedArticlesAsync_WhenCalled_ReturnsUnnotifiedArticles()
        {
            var expectedArticles = new List<Article>
            {
                CreateTestArticle(1, 1, isNotified: false),
                CreateTestArticle(2, 1, isNotified: false)
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnnotifiedArticlesAsync(100))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetUnnotifiedArticlesAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            Assert.All(result, article => Assert.False(article.IsNotified));
            _mockArticleRepository.Verify(repo => repo.GetUnnotifiedArticlesAsync(100), Times.Once);
        }

        #endregion

        #region GetUnreadCountsByFeedAsync Tests

        [Fact]
        public async Task GetUnreadCountsByFeedAsync_WhenCalled_ReturnsDictionaryOfCounts()
        {
            var expectedCounts = new Dictionary<int, int>
            {
                { 1, 5 },
                { 2, 3 },
                { 3, 0 }
            };

            _mockArticleRepository
                .Setup(repo => repo.GetUnreadCountsByFeedAsync())
                .ReturnsAsync(expectedCounts);

            var result = await _articleService.GetUnreadCountsByFeedAsync();

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
        public async Task GetArticlesByCategoriesAsync_WithValidCategories_ReturnsFilteredArticles()
        {
            var categories = "Technology,Programming";
            var expectedArticles = CreateTestArticles(3);

            _mockArticleRepository
                .Setup(repo => repo.GetByCategoriesAsync(categories, 100))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetArticlesByCategoriesAsync(categories);

            Assert.NotNull(result);
            Assert.Equal(expectedArticles.Count, result.Count);
            _mockArticleRepository.Verify(repo => repo.GetByCategoriesAsync(categories, 100), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetArticlesByCategoriesAsync_WithNullOrWhiteSpaceCategories_PropagatesToRepository(string? categories)
        {
            var expectedArticles = new List<Article>();

            _mockArticleRepository
                .Setup(repo => repo.GetByCategoriesAsync(categories ?? string.Empty, 100))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetArticlesByCategoriesAsync(categories ?? string.Empty);

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
        public async Task GetPagedArticlesAsync_WithDifferentParameters_ReturnsPagedResults(
            int page, int pageSize, bool? unreadOnly, int expectedCount)
        {
            var expectedArticles = CreateTestArticles(expectedCount);

            _mockArticleRepository
                .Setup(repo => repo.GetPagedAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<ArticleStatus?>()))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetPagedArticlesAsync(page, pageSize, unreadOnly);

            Assert.NotNull(result);
            Assert.Equal(expectedCount, result.Count);
            _mockArticleRepository.Verify(repo => repo.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ArticleStatus?>()), Times.Once);
        }

        [Fact]
        public async Task GetPagedArticlesAsync_WithInvalidPageNumber_HandlesGracefully()
        {
            var page = 0;
            var pageSize = 10;
            var expectedArticles = new List<Article>();

            _mockArticleRepository
                .Setup(repo => repo.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ArticleStatus?>()))
                .ReturnsAsync(expectedArticles);

            var result = await _articleService.GetPagedArticlesAsync(page, pageSize);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region Exception Propagation Tests

        [Fact]
        public async Task AnyServiceMethod_WhenRepositoryThrowsException_PropagatesException()
        {
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockArticleRepository
                .Setup(repo => repo.GetAllAsync())
                .ThrowsAsync(expectedException);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _articleService.GetAllArticlesAsync());

            Assert.Equal(expectedException.Message, exception.Message);
        }

        #endregion

        #region Integration-Style Tests

        [Fact]
        public async Task CompleteWorkflow_FromUnreadToReadAndStarred_ExecutesSuccessfully()
        {
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

            var initialCount = await _articleService.GetUnreadCountAsync();
            var markResult = await _articleService.MarkAsReadAsync(articleId, true);
            var starResult = await _articleService.ToggleStarredAsync(articleId);
            var finalCount = await _articleService.GetUnreadCountAsync();

            Assert.Equal(initialUnreadCount, initialCount);
            Assert.True(markResult);
            Assert.True(starResult);
            Assert.Equal(finalUnreadCount, finalCount);
        }

        [Fact]
        public async Task ArticleLifecycle_FromCreationToDeletion_ExecutesAllOperations()
        {
            var feedId = 1;
            var searchText = "important";
            var articles = CreateTestArticles(3, feedId);
            var feed = CreateTestFeed(feedId);

            _mockArticleRepository
                .Setup(repo => repo.SearchAsync(searchText))
                .ReturnsAsync(articles);

            _mockFeedRepository
                .Setup(repo => repo.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(feed);

            _mockArticleRepository
                .Setup(repo => repo.MarkAsAsync(It.IsAny<int>(), ArticleStatus.Read))
                .ReturnsAsync(1);

            _mockArticleRepository
                .Setup(repo => repo.ToggleFavoriteAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            _mockArticleRepository
                .Setup(repo => repo.DeleteOlderThanAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(1);

            var searchResults = await _articleService.SearchArticlesAsync(searchText);
            var firstArticle = searchResults.First();
            var markReadResult = await _articleService.MarkAsReadAsync(firstArticle.Id, true);
            var favoriteResult = await _articleService.ToggleFavoriteAsync(firstArticle.Id);
            var deleteResult = await _articleService.DeleteOldArticlesAsync(7);

            Assert.NotNull(searchResults);
            Assert.Equal(3, searchResults.Count);
            Assert.True(markReadResult);
            Assert.True(favoriteResult);
            Assert.Equal(1, deleteResult);
        }

        #endregion
    }
}