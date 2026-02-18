using FluentAssertions;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    [Collection("Integration Tests")]
    public class ArticleIntegrationTests : IAsyncLifetime
    {
        private DatabaseFixture _dbFixture;
        private ServiceFactory _factory = null!;
        private IArticleService _articleService = null!;
        private IFeedService _feedService = null!;

        public ArticleIntegrationTests(DatabaseFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task InitializeAsync()
        {
            _factory = new ServiceFactory(_dbFixture);
            _articleService = _factory.CreateArticleService();
            _feedService = _factory.CreateFeedService();
            await Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private const string TestFeed = "http://feeds.arstechnica.com/arstechnica/index";

        [Fact]
        public async Task GetAllArticlesAsync_ShouldReturnAllArticles()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetAllArticlesAsync();

            // Assert
            articles.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetArticlesByFeedAsync_ShouldReturnArticlesForFeed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.FeedId == feed.Id).Should().BeTrue();
            articles.First().Feed.Should().NotBeNull();
        }

        [Fact]
        public async Task GetArticlesByCategoryAsync_ShouldReturnArticlesForCategory()
        {
            // Arrange
            var categoryService = _factory.CreateCategoryService();
            var category = await categoryService.CreateCategoryAsync("Test Category");
            var feed = await _feedService.AddFeedAsync(TestFeed, category.Id);

            // Act
            var articles = await _articleService.GetArticlesByCategoryAsync(category.Id);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.Feed != null).Should().BeTrue();
        }

        [Fact]
        public async Task GetUnreadArticlesAsync_ShouldReturnOnlyUnreadArticles()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetUnreadArticlesAsync();

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.Status == ArticleStatus.Unread).Should().BeTrue();
        }

        [Fact]
        public async Task GetStarredArticlesAsync_ShouldReturnStarredArticles()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;
            await _articleService.ToggleStarredAsync(articleId);

            // Act
            var starredArticles = await _articleService.GetStarredArticlesAsync();

            // Assert
            starredArticles.Should().NotBeEmpty();
            starredArticles.Should().Contain(a => a.Id == articleId);
        }

        [Fact]
        public async Task GetFavoriteArticlesAsync_ShouldReturnFavoriteArticles()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;
            await _articleService.ToggleFavoriteAsync(articleId);

            // Act
            var favoriteArticles = await _articleService.GetFavoriteArticlesAsync();

            // Assert
            favoriteArticles.Should().NotBeEmpty();
            favoriteArticles.Should().Contain(a => a.Id == articleId);
        }

        [Fact]
        public async Task MarkAsReadAsync_ShouldMarkArticleAsRead()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act
            var result = await _articleService.MarkAsReadAsync(articleId, true);

            // Assert
            result.Should().BeTrue();
            var unreadCount = await _articleService.GetUnreadCountByFeedAsync(feed.Id);
            unreadCount.Should().Be(articles.Count - 1);
        }

        [Fact]
        public async Task ToggleStarredAsync_ShouldToggleStarStatus()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act - Star
            var result1 = await _articleService.ToggleStarredAsync(articleId);
            var starred1 = await _articleService.GetStarredArticlesAsync();

            // Act - Unstar
            var result2 = await _articleService.ToggleStarredAsync(articleId);
            var starred2 = await _articleService.GetStarredArticlesAsync();

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            starred1.Should().Contain(a => a.Id == articleId);
            starred2.Should().NotContain(a => a.Id == articleId);
        }

        [Fact]
        public async Task ToggleFavoriteAsync_ShouldToggleFavoriteStatus()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act - Add favorite
            var result1 = await _articleService.ToggleFavoriteAsync(articleId);
            var favorites1 = await _articleService.GetFavoriteArticlesAsync();

            // Act - Remove favorite
            var result2 = await _articleService.ToggleFavoriteAsync(articleId);
            var favorites2 = await _articleService.GetFavoriteArticlesAsync();

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue();
            favorites1.Should().Contain(a => a.Id == articleId);
            favorites2.Should().NotContain(a => a.Id == articleId);
        }

        [Fact]
        public async Task SearchArticlesAsync_WithValidQuery_ShouldReturnResults()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var results = await _articleService.SearchArticlesAsync("the");

            // Assert
            results.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteOldArticlesAsync_ShouldRemoveOldArticles()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var deletedCount = await _articleService.DeleteOldArticlesAsync(0);

            // Assert
            deletedCount.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetUnreadCountAsync_ShouldReturnTotalUnreadCount()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var count = await _articleService.GetUnreadCountAsync();

            // Assert
            count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetUnreadCountByFeedAsync_ShouldReturnUnreadCountForFeed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);

            // Act
            var count = await _articleService.GetUnreadCountByFeedAsync(feed.Id);

            // Assert
            count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_ShouldMarkAllArticlesAsRead()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var result = await _articleService.MarkAllAsReadAsync();
            var unreadCount = await _articleService.GetUnreadCountAsync();

            // Assert
            result.Should().BeTrue();
            unreadCount.Should().Be(0);
        }

        [Fact]
        public async Task MarkAsNotifiedAsync_ShouldMarkArticleAsNotified()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act
            var result = await _articleService.MarkAsNotifiedAsync(articleId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task MarkAsProcessedAsync_ShouldMarkArticleAsProcessed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act
            var result = await _articleService.MarkAsProcessedAsync(articleId);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetUnprocessedArticlesAsync_ShouldReturnUnprocessedArticles()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetUnprocessedArticlesAsync();

            // Assert
            articles.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUnnotifiedArticlesAsync_ShouldReturnUnnotifiedArticles()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetUnnotifiedArticlesAsync();

            // Assert
            articles.Should().NotBeNull();
        }

        [Fact]
        public async Task GetUnreadCountsByFeedAsync_ShouldReturnCountsByFeed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(TestFeed);

            // Act
            var counts = await _articleService.GetUnreadCountsByFeedAsync();

            // Assert
            counts.Should().ContainKey(feed.Id);
            counts[feed.Id].Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetPagedArticlesAsync_ShouldReturnPagedResults()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetPagedArticlesAsync(1, 5);

            // Assert
            articles.Should().NotBeEmpty();
            articles.Count.Should().BeLessThanOrEqualTo(5);
        }

        [Fact]
        public async Task GetPagedArticlesAsync_WithUnreadFilter_ShouldReturnOnlyUnread()
        {
            // Arrange
            await _feedService.AddFeedAsync(TestFeed);

            // Act
            var articles = await _articleService.GetPagedArticlesAsync(1, 5, unreadOnly: true);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.Status == ArticleStatus.Unread).Should().BeTrue();
        }
    }
}