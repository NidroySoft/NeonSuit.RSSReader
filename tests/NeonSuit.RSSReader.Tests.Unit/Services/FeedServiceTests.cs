using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.FeedParser;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;
using System.Net;
using System.Net.Sockets;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for the <see cref="FeedService"/> class.
    /// Validates feed management, synchronization, and maintenance operations.
    /// </summary>
    public class FeedServiceTests
    {
        private readonly Mock<IFeedRepository> _mockFeedRepository;
        private readonly Mock<IArticleRepository> _mockArticleRepository;
        private readonly Mock<IFeedParser> _mockFeedParser;
        private readonly Mock<ILogger> _mockLogger;
        private readonly IFeedService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedServiceTests"/> class.
        /// </summary>
        public FeedServiceTests()
        {
            _mockFeedRepository = new Mock<IFeedRepository>();
            _mockArticleRepository = new Mock<IArticleRepository>();
            _mockFeedParser = new Mock<IFeedParser>();
            _mockLogger = new Mock<ILogger>();

            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
              .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                       .Returns(_mockLogger.Object);

            _service = new FeedService(
                _mockFeedRepository.Object,
                _mockArticleRepository.Object,
                _mockFeedParser.Object,
                _mockLogger.Object);
        }

        private Feed CreateTestFeed(
                int id = 1,
                string name = "Tech Feed",
                string url = "https://example.com/feed.xml",
                int? categoryId = 1,
                bool isActive = true,
                int failureCount = 0)
        {
            return new Feed
            {
                Id = id,
                Title = name,
                Url = url,
                WebsiteUrl = "https://example.com",
                CategoryId = categoryId,
                IsActive = isActive,
                FailureCount = failureCount,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastUpdated = DateTime.UtcNow.AddHours(-1),
                UpdateFrequency = FeedUpdateFrequency.EveryHour
            };
        }

        private Article CreateTestArticle(
                int id = 1,
                int feedId = 1,
                string title = "Test Article",
                string content = "Test content",
                string guid = "test-guid-123",
                ArticleStatus status = ArticleStatus.Unread)
        {
            return new Article
            {
                Id = id,
                FeedId = feedId,
                Title = title,
                Content = content,
                Summary = "Test summary",
                Guid = guid,
                Link = "https://example.com/article",
                PublishedDate = DateTime.UtcNow.AddDays(-1),
                AddedDate = DateTime.UtcNow,
                Status = status,
                IsStarred = false,
                IsFavorite = false,
                IsNotified = false,
                ProcessedByRules = false
            };
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFeedRepository_ShouldThrowArgumentNullException()
        {
            Action act = () => new FeedService(null!, _mockArticleRepository.Object, _mockFeedParser.Object, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("feedRepository");
        }

        [Fact]
        public void Constructor_WithNullArticleRepository_ShouldThrowArgumentNullException()
        {
            Action act = () => new FeedService(_mockFeedRepository.Object, null!, _mockFeedParser.Object, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("articleRepository");
        }

        [Fact]
        public void Constructor_WithNullFeedParser_ShouldThrowArgumentNullException()
        {
            Action act = () => new FeedService(_mockFeedRepository.Object, _mockArticleRepository.Object, null!, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("feedParser");
        }

        #endregion

        #region GetAllFeedsAsync Tests

        [Fact]
        public async Task GetAllFeedsAsync_ShouldReturnAllFeeds()
        {
            var expectedFeeds = new List<Feed>
            {
                new Feed { Id = 1, Title = "Feed 1" },
                new Feed { Id = 2, Title = "Feed 2" }
            };

            _mockFeedRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(expectedFeeds);

            var result = await _service.GetAllFeedsAsync();

            result.Should().BeEquivalentTo(expectedFeeds);
        }

        [Fact]
        public async Task GetAllFeedsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            _mockFeedRepository.Setup(x => x.GetAllAsync()).ThrowsAsync(new Exception("DB Error"));

            Func<Task> act = async () => await _service.GetAllFeedsAsync();

            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
        }

        #endregion

        #region GetFeedByIdAsync Tests

        [Fact]
        public async Task GetFeedByIdAsync_WithExistingId_ShouldReturnFeed()
        {
            var expectedFeed = new Feed { Id = 1, Title = "Test Feed" };
            _mockFeedRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(expectedFeed);

            var result = await _service.GetFeedByIdAsync(1);

            result.Should().BeEquivalentTo(expectedFeed);
        }

        [Fact]
        public async Task GetFeedByIdAsync_WithNonExistingId_ShouldReturnNull()
        {
            _mockFeedRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Feed?)null);

            var result = await _service.GetFeedByIdAsync(999);

            result.Should().BeNull();
        }

        #endregion

        #region AddFeedAsync Tests

        [Fact]
        public async Task AddFeedAsync_WithExistingUrl_ShouldThrowInvalidOperationException()
        {
            var url = "https://example.com/feed.xml";
            _mockFeedRepository.Setup(x => x.ExistsByUrlAsync(url)).ReturnsAsync(true);

            Func<Task> act = async () => await _service.AddFeedAsync(url);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already in your list*");
        }

        [Fact]
        public async Task AddFeedAsync_WithInvalidUrl_ShouldThrowInvalidOperationException()
        {
            var url = "https://invalid.com/feed.xml";
            _mockFeedRepository.Setup(x => x.ExistsByUrlAsync(url)).ReturnsAsync(false);
            _mockFeedParser.Setup(x => x.ParseFeedAsync(url)).ReturnsAsync(((Feed?) null, (List<Article>?)null));

            Func<Task> act = async () => await _service.AddFeedAsync(url);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Could not read the feed*");
        }

        [Fact]
        public async Task AddFeedAsync_WithValidUrl_ShouldCreateFeedAndArticles()
        {
            var url = "https://example.com/feed.xml";
            var feed = new Feed { Title = "Test Feed", Url = url };
            var articles = new List<Article> { new Article { Title = "Article 1" } };

            _mockFeedRepository.Setup(x => x.ExistsByUrlAsync(url)).ReturnsAsync(false);
            _mockFeedParser.Setup(x => x.ParseFeedAsync(url)).ReturnsAsync((feed, articles));

            // Usar Callback para establecer el ID en el feed
            _mockFeedRepository.Setup(x => x.InsertAsync(It.IsAny<Feed>()))
                .ReturnsAsync(1)
                .Callback<Feed>(f => f.Id = 1); // ← Esto establece el ID

            _mockArticleRepository.Setup(x => x.InsertAllAsync(It.IsAny<List<Article>>())).ReturnsAsync(1);

            var result = await _service.AddFeedAsync(url);

            result.Should().NotBeNull();
            result.Title.Should().Be("Test Feed");
            result.IsActive.Should().BeTrue();

            // Verificar que se llamó con artículos que tienen FeedId = 1
            _mockArticleRepository.Verify(
                x => x.InsertAllAsync(It.Is<List<Article>>(a => a.All(ar => ar.FeedId == 1))),
                Times.Once);
        }

        [Fact]
        public async Task AddFeedAsync_WithCategoryId_ShouldAssignCategory()
        {
            var url = "https://example.com/feed.xml";
            var feed = new Feed { Title = "Test Feed", Url = url };

            _mockFeedRepository.Setup(x => x.ExistsByUrlAsync(url)).ReturnsAsync(false);
            _mockFeedParser.Setup(x => x.ParseFeedAsync(url)).ReturnsAsync((feed, new List<Article>()));
            _mockFeedRepository.Setup(x => x.InsertAsync(It.IsAny<Feed>())).ReturnsAsync(1);

            var result = await _service.AddFeedAsync(url, categoryId: 5);

            result.CategoryId.Should().Be(5);
        }

        #endregion

        #region RefreshFeedAsync Tests

        [Fact]
        public async Task RefreshFeedAsync_WithNonExistingFeed_ShouldReturnFalse()
        {
            _mockFeedRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Feed?)null);

            var result = await _service.RefreshFeedAsync(999);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task RefreshFeedAsync_WithNewArticles_ShouldAddArticlesAndUpdateCounts()
        {
            var feed = new Feed { Id = 1, Url = "https://example.com/feed.xml" };
            var newArticles = new List<Article>
            {
                new Article { Guid = "guid1", Title = "New Article 1" },
                new Article { Guid = "guid2", Title = "New Article 2" }
            };

            _mockFeedRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(feed);
            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, 1)).ReturnsAsync(newArticles);
            _mockArticleRepository.Setup(x => x.ExistsByGuidAsync("guid1")).ReturnsAsync(false);
            _mockArticleRepository.Setup(x => x.ExistsByGuidAsync("guid2")).ReturnsAsync(false);

            var result = await _service.RefreshFeedAsync(1);

            result.Should().BeTrue();
            _mockArticleRepository.Verify(x => x.InsertAsync(It.IsAny<Article>()), Times.Exactly(2));
            _mockFeedRepository.Verify(x => x.UpdateLastUpdatedAsync(1), Times.Once);
            _mockFeedRepository.Verify(x => x.ResetFailureCountAsync(1), Times.Once);
        }

        [Fact]
        public async Task RefreshFeedAsync_WithExistingArticles_ShouldSkipDuplicates()
        {
            var feed = new Feed { Id = 1, Url = "https://example.com/feed.xml" };
            var articles = new List<Article> { new Article { Guid = "existing", Title = "Existing" } };

            _mockFeedRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(feed);
            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, 1)).ReturnsAsync(articles);
            _mockArticleRepository.Setup(x => x.ExistsByGuidAsync("existing")).ReturnsAsync(true);

            var result = await _service.RefreshFeedAsync(1);

            result.Should().BeTrue();
            _mockArticleRepository.Verify(x => x.InsertAsync(It.IsAny<Article>()), Times.Never);
        }

        [Fact]
        public async Task RefreshFeedAsync_WhenParserThrows_ShouldReturnFalseAndIncrementFailure()
        {
            // Arrange
            var feedId = 1;
            var feed = CreateTestFeed(id: feedId);
            var exceptionMessage = "Parse error";

            _mockFeedRepository.Setup(x => x.GetByIdAsync(feedId))
                .ReturnsAsync(feed);

            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, feedId))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _service.RefreshFeedAsync(feedId);

            // Assert
            result.Should().BeFalse();

            _mockFeedRepository.Verify(x =>
                x.IncrementFailureCountAsync(1, $"Parse error: {exceptionMessage}"),
                Times.Once);
        }

        #endregion

        #region RefreshAllFeedsAsync Tests

        [Fact]
        public async Task RefreshAllFeedsAsync_ShouldRefreshDueFeeds()
        {
            var feeds = new List<Feed>
            {
                new Feed { Id = 1, Url = "https://feed1.com" },
                new Feed { Id = 2, Url = "https://feed2.com" }
            };

            _mockFeedRepository.Setup(x => x.GetFeedsToUpdateAsync()).ReturnsAsync(feeds);
            _mockFeedRepository.Setup(x => x.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => feeds.First(f => f.Id == id));
            _mockFeedParser.Setup(x => x.ParseArticlesAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(new List<Article>());

            var result = await _service.RefreshAllFeedsAsync();

            result.Should().Be(2);
        }

        [Fact]
        public async Task RefreshAllFeedsAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            _mockFeedRepository.Setup(x => x.GetFeedsToUpdateAsync()).ThrowsAsync(new Exception("DB Error"));

            Func<Task> act = async () => await _service.RefreshAllFeedsAsync();

            await act.Should().ThrowAsync<Exception>().WithMessage("DB Error");
        }

        #endregion

        #region UpdateFeedAsync Tests

        [Fact]
        public async Task UpdateFeedAsync_WithValidFeed_ShouldReturnTrue()
        {
            var feed = new Feed { Id = 1, Title = "Updated Title" };
            _mockFeedRepository.Setup(x => x.UpdateAsync(feed)).ReturnsAsync(1);

            var result = await _service.UpdateFeedAsync(feed);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateFeedAsync_WithNonExistingFeed_ShouldReturnFalse()
        {
            var feed = new Feed { Id = 999, Title = "Updated Title" };
            _mockFeedRepository.Setup(x => x.UpdateAsync(feed)).ReturnsAsync(0);

            var result = await _service.UpdateFeedAsync(feed);

            result.Should().BeFalse();
        }

        #endregion

        #region DeleteFeedAsync Tests

        [Fact]
        public async Task DeleteFeedAsync_ShouldDeleteArticlesThenFeed()
        {
            _mockFeedRepository.Setup(x => x.DeleteAsync(It.IsAny<Feed>())).ReturnsAsync(1);

            var result = await _service.DeleteFeedAsync(1);

            result.Should().BeTrue();
            _mockArticleRepository.Verify(x => x.DeleteByFeedAsync(1), Times.Once);
            _mockFeedRepository.Verify(x => x.DeleteAsync(It.Is<Feed>(f => f.Id == 1)), Times.Once);
        }

        [Fact]
        public async Task DeleteFeedAsync_WithNonExistingFeed_ShouldReturnFalse()
        {
            _mockFeedRepository.Setup(x => x.DeleteAsync(It.IsAny<Feed>())).ReturnsAsync(0);

            var result = await _service.DeleteFeedAsync(999);

            result.Should().BeFalse();
        }

        #endregion

        #region FeedExistsAsync Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task FeedExistsAsync_ShouldReturnRepositoryResult(bool exists)
        {
            var url = "https://example.com/feed.xml";
            _mockFeedRepository.Setup(x => x.ExistsByUrlAsync(url)).ReturnsAsync(exists);

            var result = await _service.FeedExistsAsync(url);

            result.Should().Be(exists);
        }

        #endregion

        #region GetFailedFeedsAsync Tests

        [Fact]
        public async Task GetFailedFeedsAsync_ShouldReturnFeedsAboveThreshold()
        {
            var failedFeeds = new List<Feed>
            {
                new Feed { Id = 1, Title = "Failed Feed", FailureCount = 5 }
            };

            _mockFeedRepository.Setup(x => x.GetFailedFeedsAsync(3)).ReturnsAsync(failedFeeds);

            var result = await _service.GetFailedFeedsAsync(3);

            result.Should().BeEquivalentTo(failedFeeds);
        }

        #endregion

        #region GetFeedHealthStatsAsync Tests

        [Fact]
        public async Task GetFeedHealthStatsAsync_ShouldCalculateCorrectStats()
        {
            var feeds = new List<Feed>
                {
                    new Feed { Id = 1, FailureCount = 0 },      // Healthy
                    new Feed { Id = 2, FailureCount = 0 },      // Healthy
                    new Feed { Id = 3, FailureCount = 2 },      // Warning (1-3)
                    new Feed { Id = 4, FailureCount = 5 }       // Error (>3)
                };

            _mockFeedRepository.Setup(x => x.GetAllAsync()).ReturnsAsync(feeds);

            var result = await _service.GetFeedHealthStatsAsync();

            result[FeedHealthStatus.Healthy].Should().Be(2);
            result[FeedHealthStatus.Warning].Should().Be(1);
            result[FeedHealthStatus.Error].Should().Be(1);
        }

        #endregion

        #region SearchFeedsAsync Tests

        [Fact]
        public async Task SearchFeedsAsync_WithValidSearchText_ShouldReturnMatches()
        {
            var searchText = "tech";
            var expectedFeeds = new List<Feed> { new Feed { Id = 1, Title = "Tech News" } };

            _mockFeedRepository.Setup(x => x.SearchAsync(searchText)).ReturnsAsync(expectedFeeds);

            var result = await _service.SearchFeedsAsync(searchText);

            result.Should().BeEquivalentTo(expectedFeeds);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SearchFeedsAsync_WithEmptySearchText_ShouldReturnEmptyList(string? searchText)
        {
            var result = await _service.SearchFeedsAsync(searchText!);

            result.Should().BeEmpty();
        }

        #endregion

        #region UpdateFeedCategoryAsync Tests

        [Fact]
        public async Task UpdateFeedCategoryAsync_WithExistingFeed_ShouldUpdateCategory()
        {
            var feed = new Feed { Id = 1, Title = "Test Feed", CategoryId = 1 };
            _mockFeedRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(feed);
            _mockFeedRepository.Setup(x => x.UpdateAsync(It.IsAny<Feed>())).ReturnsAsync(1);

            var result = await _service.UpdateFeedCategoryAsync(1, 5);

            result.Should().BeTrue();
            feed.CategoryId.Should().Be(5);
        }

        [Fact]
        public async Task UpdateFeedCategoryAsync_WithNonExistingFeed_ShouldReturnFalse()
        {
            _mockFeedRepository.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Feed?)null);

            var result = await _service.UpdateFeedCategoryAsync(999, 5);

            result.Should().BeFalse();
        }

        #endregion

        #region SetFeedActiveStatusAsync Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SetFeedActiveStatusAsync_ShouldUpdateStatus(bool isActive)
        {
            _mockFeedRepository.Setup(x => x.SetActiveStatusAsync(1, isActive)).ReturnsAsync(1);

            var result = await _service.SetFeedActiveStatusAsync(1, isActive);

            result.Should().BeTrue();
        }

        #endregion

        #region CleanupOldArticlesAsync Tests

        [Fact]
        public async Task CleanupOldArticlesAsync_ShouldDeleteArticlesBasedOnRetention()
        {
            var feeds = new List<Feed>
            {
                new Feed { Id = 1, ArticleRetentionDays = 30 },
                new Feed { Id = 2, ArticleRetentionDays = null }, // No retention
                new Feed { Id = 3, ArticleRetentionDays = 7 }
            };

            _mockFeedRepository.Setup(x => x.GetFeedsWithRetentionAsync()).ReturnsAsync(feeds);
            _mockArticleRepository.Setup(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>())).ReturnsAsync(5);

            var result = await _service.CleanupOldArticlesAsync();

            result.Should().Be(10); // 5 from feed 1 + 5 from feed 3
            _mockArticleRepository.Verify(x => x.DeleteOlderThanAsync(It.IsAny<DateTime>()), Times.Exactly(2));
        }

        #endregion

        #region CreateFeedAsync Tests

        [Fact]
        public async Task CreateFeedAsync_WithNullFeed_ShouldThrowArgumentNullException()
        {
            Func<Task> act = async () => await _service.CreateFeedAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("feed");
        }

        [Fact]
        public async Task CreateFeedAsync_WithEmptyUrl_ShouldThrowArgumentException()
        {
            var feed = new Feed { Title = "Test", Url = "" };

            Func<Task> act = async () => await _service.CreateFeedAsync(feed);

            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*URL cannot be empty*");
        }

        [Fact]
        public async Task CreateFeedAsync_WithExistingUrl_ShouldThrowInvalidOperationException()
        {
            var feed = new Feed { Title = "Test", Url = "https://existing.com/feed.xml" };
            _mockFeedRepository.Setup(x => x.GetByUrlAsync(feed.Url)).ReturnsAsync(new Feed { Id = 1 });

            Func<Task> act = async () => await _service.CreateFeedAsync(feed);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
        }

        [Fact]
        public async Task CreateFeedAsync_WithValidFeed_ShouldInsertAndReturnId()
        {
            var feed = new Feed { Title = "New Feed", Url = "https://new.com/feed.xml" };
            _mockFeedRepository.Setup(x => x.GetByUrlAsync(feed.Url)).ReturnsAsync((Feed?)null);
            _mockFeedRepository.Setup(x => x.InsertAsync(feed)).ReturnsAsync(42);

            var result = await _service.CreateFeedAsync(feed);

            result.Should().Be(42);
            feed.CreatedAt.Should().NotBe(default);
            feed.LastUpdated.Should().NotBeNull();
        }

        #endregion

        #region GetFeedByUrlAsync Tests

        [Fact]
        public async Task GetFeedByUrlAsync_ShouldReturnFeedFromRepository()
        {
            var url = "https://example.com/feed.xml";
            var expectedFeed = new Feed { Id = 1, Url = url };

            _mockFeedRepository.Setup(x => x.GetByUrlAsync(url)).ReturnsAsync(expectedFeed);

            var result = await _service.GetFeedByUrlAsync(url);

            result.Should().BeEquivalentTo(expectedFeed);
        }

        #endregion

        #region Network Exception Tests
        // FeedServiceTests.cs
        [Fact]
        public async Task RefreshFeedAsync_WhenHttpRequestException_ShouldIncrementFailureCountAndReturnFalse()
        {
            // Arrange
            var feedId = 1;
            var feed = CreateTestFeed(id: feedId, url: "https://example.com/feed");

            _mockFeedRepository.Setup(x => x.GetByIdAsync(feedId))
                .ReturnsAsync(feed);

            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, feedId))
                .ThrowsAsync(new HttpRequestException("404 Not Found", null, HttpStatusCode.NotFound));

            // Act
            var result = await _service.RefreshFeedAsync(feedId);

            // Assert
            result.Should().BeFalse();
            _mockFeedRepository.Verify(x =>
                x.IncrementFailureCountAsync(feedId, It.Is<string>(s => s.Contains("404"))),
                Times.Once);
            _mockFeedRepository.Verify(x => x.ResetFailureCountAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task RefreshFeedAsync_WhenTaskCanceledException_ShouldIncrementFailureCountAndReturnFalse()
        {
            // Arrange
            var feedId = 1;
            var feed = CreateTestFeed(id: feedId);

            _mockFeedRepository.Setup(x => x.GetByIdAsync(feedId))
                .ReturnsAsync(feed);

            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, feedId))
                .ThrowsAsync(new TaskCanceledException());

            // Act
            var result = await _service.RefreshFeedAsync(feedId);

            // Assert
            result.Should().BeFalse();
            _mockFeedRepository.Verify(x =>
                x.IncrementFailureCountAsync(feedId, It.Is<string>(s => s.Contains("Timeout"))),
                Times.Once);
        }

        [Fact]
        public async Task RefreshFeedAsync_WhenSocketException_ShouldIncrementFailureCountAndReturnFalse()
        {
            // Arrange
            var feedId = 1;
            var feed = CreateTestFeed(id: feedId);

            _mockFeedRepository.Setup(x => x.GetByIdAsync(feedId))
                .ReturnsAsync(feed);

            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, feedId))
                .ThrowsAsync(new SocketException(10060)); // Connection timed out

            // Act
            var result = await _service.RefreshFeedAsync(feedId);

            // Assert
            result.Should().BeFalse();
            _mockFeedRepository.Verify(x =>
                x.IncrementFailureCountAsync(feedId, It.Is<string>(s => s.Contains("Network error"))),
                Times.Once);
        }

        [Fact]
        public async Task RefreshFeedAsync_WhenSuccessful_ShouldResetFailureCount()
        {
            // Arrange
            var feedId = 1;
            var feed = CreateTestFeed(id: feedId);
            var articles = new List<Article> { CreateTestArticle(feedId: feedId) };

            _mockFeedRepository.Setup(x => x.GetByIdAsync(feedId))
                .ReturnsAsync(feed);
            _mockFeedParser.Setup(x => x.ParseArticlesAsync(feed.Url, feedId))
                .ReturnsAsync(articles);
            _mockArticleRepository.Setup(x => x.ExistsByGuidAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.RefreshFeedAsync(feedId);

            // Assert
            result.Should().BeTrue();
            _mockFeedRepository.Verify(x => x.ResetFailureCountAsync(feedId), Times.Once);
        }
        #endregion Network Exception Tests
    }
}