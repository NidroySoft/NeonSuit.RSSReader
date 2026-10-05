using FluentAssertions;
using NeonSuit.RSSReader.Services.RssFeedParser;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    [Collection("Integration Tests")]
    public class FeedParserIntegrationTests
    {
        private readonly ILogger _logger;

        public FeedParserIntegrationTests(DatabaseFixture dbFixture)
        {
            _logger = dbFixture.Logger;
        }

        [Theory]
        [InlineData("http://feeds.arstechnica.com/arstechnica/index")]
        [InlineData("https://www.cibercuba.com/rss.xml")]
        public async Task ParseFeedAsync_WithValidFeed_ShouldReturnFeedAndArticles(string url)
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync(url);

            // Assert
            feed.Should().NotBeNull();
            feed.Title.Should().NotBeNullOrEmpty();
            feed.Url.Should().Be(url);
            articles.Should().NotBeEmpty();
            articles.All(a => !string.IsNullOrEmpty(a.Title)).Should().BeTrue();
            articles.All(a => !string.IsNullOrEmpty(a.Link)).Should().BeTrue();
        }

        [Fact]
        public async Task ParseFeedAsync_WithCiberCubaFeed_ShouldHandleMalformedXml()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);
            var ciberCubaUrl = "https://www.cibercuba.com/rss.xml";

            // Act
            var (feed, articles) = await parser.ParseFeedAsync(ciberCubaUrl);

            // Assert
            feed.Should().NotBeNull();
            feed.Title.Should().NotBeNullOrEmpty();
            articles.Should().NotBeEmpty();
            articles.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task ParseArticlesAsync_WithValidFeed_ShouldReturnArticlesWithFeedId()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);
            var feedId = 42;

            // Act
            var articles = await parser.ParseArticlesAsync("http://feeds.arstechnica.com/arstechnica/index", feedId);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.FeedId == feedId).Should().BeTrue();
            articles.All(a => !string.IsNullOrEmpty(a.Guid)).Should().BeTrue();
        }

        [Fact]
        public async Task ParseFeedAsync_WithInvalidUrl_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);
            var invalidUrl = "https://invalid-url-that-does-not-exist-12345.com/feed.xml";

            // Act
            Func<Task> act = async () => await parser.ParseFeedAsync(invalidUrl);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Network error*");
        }

        [Fact]
        public async Task ParseFeedAsync_WithNonFeedUrl_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);
            var nonFeedUrl = "https://www.google.com";

            // Act
            Func<Task> act = async () => await parser.ParseFeedAsync(nonFeedUrl);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Parse error*");
        }

        [Fact]
        public async Task ParseFeedAsync_WithUnreachableHost_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);
            var unreachableUrl = "http://10.255.255.1/feed.xml";

            // Act
            Func<Task> act = async () => await parser.ParseFeedAsync(unreachableUrl);

            // Assert - Solo verificamos que lanza la excepción esperada
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Theory]
        [InlineData("http://feeds.bbci.co.uk/news/rss.xml")]
        [InlineData("https://feeds.feedburner.com/TechCrunch")]
        public async Task ParseFeedAsync_WithVariousFeeds_ShouldParseSuccessfully(string url)
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync(url);

            // Assert
            feed.Should().NotBeNull();
            articles.Should().NotBeNull();
        }

        [Fact]
        public async Task ParseFeedAsync_ShouldExtractImageUrls()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync("https://www.cibercuba.com/rss.xml");

            // Assert
            articles.Should().NotBeEmpty();
            // Al menos algunos artículos deberían tener imagen
            articles.Where(a => !string.IsNullOrEmpty(a.ImageUrl)).Should().NotBeEmpty();
        }

        [Fact]
        public async Task ParseFeedAsync_ShouldHandleHtmlEntitiesInTitles()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync("https://www.cibercuba.com/rss.xml");

            // Assert
            articles.Should().NotBeEmpty();
            // Verificar que las entidades HTML fueron decodificadas
            articles.Where(a => a.Title.Contains("&") && !a.Title.Contains("&amp;")).Should().BeEmpty();
        }

        [Fact]
        public async Task ParseFeedAsync_ShouldSetDefaultValuesForEmptyFields()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync("http://feeds.arstechnica.com/arstechnica/index");

            // Assert
            feed.Should().NotBeNull();
            // Si no hay título, debería tener valor por defecto
            feed.Title.Should().NotBeNullOrEmpty();
            // Si no hay descripción, debería ser string vacío
            feed.Description.Should().NotBeNull();
        }

        [Fact]
        public async Task ParseFeedAsync_ShouldLimitContentLength()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var (feed, articles) = await parser.ParseFeedAsync("http://feeds.arstechnica.com/arstechnica/index");

            // Assert
            articles.Should().NotBeEmpty();
            // Verificar que el contenido no excede 1MB
            articles.All(a => a.Content?.Length <= 1000003).Should().BeTrue(); // 1MB + "..."
        }

        [Fact]
        public async Task ParseArticlesAsync_ShouldSetCorrectPublishedDate()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var articles = await parser.ParseArticlesAsync("http://feeds.arstechnica.com/arstechnica/index", 1);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => a.PublishedDate != default).Should().BeTrue();
        }

        [Fact]
        public async Task ParseArticlesAsync_ShouldGenerateGuidWhenMissing()
        {
            // Arrange
            var parser = new RssFeedParser(_logger);

            // Act
            var articles = await parser.ParseArticlesAsync("http://feeds.arstechnica.com/arstechnica/index", 1);

            // Assert
            articles.Should().NotBeEmpty();
            articles.All(a => !string.IsNullOrEmpty(a.Guid)).Should().BeTrue();
        }
    }
}