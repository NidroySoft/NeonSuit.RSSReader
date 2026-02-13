using FluentAssertions;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    [Collection("Integration Tests")]
    public class FeedIntegrationTests : IAsyncLifetime
    {
        private DatabaseFixture _dbFixture;
        private ServiceFactory _factory = null!;
        private IFeedService _feedService = null!;
        private IArticleService _articleService = null!;

        public FeedIntegrationTests(DatabaseFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task InitializeAsync()
        {
            _factory = new ServiceFactory(_dbFixture);
            _feedService = _factory.CreateFeedService();
            _articleService = _factory.CreateArticleService();

            await Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private const string ArsTechnicaFeed = "http://feeds.arstechnica.com/arstechnica/index";

        private const string W3cFeed = "https://www.cibercuba.com/rss.xml";

        [Fact]
        public async Task AddFeedAsync_WithW3cFeed_ShouldDownloadParseAndPersistFeed()
        {
            // Act
            var feed = await _feedService.AddFeedAsync(W3cFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Assert
            feed.Should().NotBeNull();
            feed.Id.Should().BeGreaterThan(0);
            feed.Title.Should().NotBeNullOrEmpty();
            articles.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AddFeedAsync_WithDuplicateUrl_ShouldThrowInvalidOperationException()
        {
            // Arrange
            await _feedService.AddFeedAsync(ArsTechnicaFeed);

            // Act
            Func<Task> act = () => _feedService.AddFeedAsync(ArsTechnicaFeed);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already in your list*");
        }

        [Fact]
        public async Task RefreshFeedAsync_WithArsTechnicaFeed_ShouldDownloadNewArticles()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var initialArticles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Act
            var result = await _feedService.RefreshFeedAsync(feed.Id);
            var updatedArticles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Assert
            result.Should().BeTrue();
            updatedArticles.Count.Should().BeGreaterThanOrEqualTo(initialArticles.Count);
        }

        [Fact]
        public async Task GetUnreadCountsAsync_AfterAddingFeed_ShouldReturnCorrectCounts()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Act
            var unreadCounts = await _feedService.GetUnreadCountsAsync();
            var feedUnread = await _feedService.GetUnreadCountByFeedAsync(feed.Id);

            // Assert
            unreadCounts.Should().ContainKey(feed.Id);
            unreadCounts[feed.Id].Should().Be(articles.Count);
            feedUnread.Should().Be(articles.Count);
        }

        [Fact]
        public async Task MarkAsReadAsync_WithRealArticle_ShouldUpdateStatus()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);
            var articleId = articles.First().Id;

            // Act
            var result = await _articleService.MarkAsReadAsync(articleId, true);
            var unreadCount = await _feedService.GetUnreadCountByFeedAsync(feed.Id);
            var updatedArticle = (await _articleService.GetArticlesByFeedAsync(feed.Id))
                .First(a => a.Id == articleId);

            // Assert
            result.Should().BeTrue();
            unreadCount.Should().Be(articles.Count - 1);
            updatedArticle.Status.Should().Be(ArticleStatus.Read);
        }

        [Fact]
        public async Task SearchFeedsAsync_WithExistingFeed_ShouldReturnMatchingResults()
        {
            // Arrange
            await _feedService.AddFeedAsync(ArsTechnicaFeed);

            // Act
            var results = await _feedService.SearchFeedsAsync("Ars Technica");

            // Assert
            results.Should().NotBeEmpty();
            results.First().Title.Should().Contain("Ars Technica");
        }

        [Fact]
        public async Task UpdateFeedPropertiesAsync_WithValidData_ShouldUpdateFeed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var newTitle = "Updated Ars Technica Feed";
            var newDescription = "This is an updated description";

            // Act
            var result = await _feedService.UpdateFeedPropertiesAsync(feed.Id, newTitle, newDescription);
            var updatedFeed = await _feedService.GetFeedByIdAsync(feed.Id);

            // Assert
            result.Should().BeTrue();
            updatedFeed!.Title.Should().Be(newTitle);
            updatedFeed.Description.Should().Be(newDescription);
        }

        [Fact]
        public async Task UpdateFeedCategoryAsync_ShouldMoveFeedToDifferentCategory()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var categoryService = _factory.CreateCategoryService();
            var newCategory = await categoryService.CreateCategoryAsync("Integration Test Category");

            // Act
            var result = await _feedService.UpdateFeedCategoryAsync(feed.Id, newCategory.Id);
            var updatedFeed = await _feedService.GetFeedByIdAsync(feed.Id);
            var feedsInCategory = await _feedService.GetFeedsByCategoryAsync(newCategory.Id);

            // Assert
            result.Should().BeTrue();
            updatedFeed!.CategoryId.Should().Be(newCategory.Id);
            feedsInCategory.Should().Contain(f => f.Id == feed.Id);
        }

        [Fact]
        public async Task DeleteFeedAsync_WithExistingFeed_ShouldRemoveFeedAndArticles()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);
            var articles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Act
            var result = await _feedService.DeleteFeedAsync(feed.Id);
            var deletedFeed = await _feedService.GetFeedByIdAsync(feed.Id);
            var deletedArticles = await _articleService.GetArticlesByFeedAsync(feed.Id);

            // Assert
            result.Should().BeTrue();
            deletedFeed.Should().BeNull();
            deletedArticles.Should().BeEmpty();
        }

        [Fact]
        public async Task GetFeedByUrlAsync_WithExistingUrl_ShouldReturnFeed()
        {
            // Arrange
            var feed = await _feedService.AddFeedAsync(ArsTechnicaFeed);

            // Act
            var retrievedFeed = await _feedService.GetFeedByUrlAsync(ArsTechnicaFeed);

            // Assert
            retrievedFeed.Should().NotBeNull();
            retrievedFeed!.Id.Should().Be(feed.Id);
            retrievedFeed.Title.Should().Be(feed.Title);
        }

        [Fact]
        public async Task GetUncategorizedFeedsAsync_ShouldReturnFeedsWithoutCategory()
        {
            // Arrange
            await _feedService.AddFeedAsync(ArsTechnicaFeed, categoryId: null);

            // Act
            var uncategorizedFeeds = await _feedService.GetUncategorizedFeedsAsync();

            // Assert
            uncategorizedFeeds.Should().NotBeEmpty();
            uncategorizedFeeds.All(f => f.CategoryId == null).Should().BeTrue();
        }
    }
}