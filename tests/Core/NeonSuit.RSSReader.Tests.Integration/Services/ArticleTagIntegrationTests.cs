using FluentAssertions;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services;

[Collection("Integration Tests")]
public class ArticleTagIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _dbFixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private readonly ServiceFactory _factory;

    private IArticleService _articleService = null!;
    private ITagService _tagService = null!;
    private IArticleTagService _articleTagService = null!;

    private Article _testArticle1 = null!;
    private Article _testArticle2 = null!;
    private Tag _testTag1 = null!;
    private Tag _testTag2 = null!;
    private Tag _testTag3 = null!;

    public ArticleTagIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
    {
        _dbFixture = dbFixture;
        _output = output;
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<ArticleTagIntegrationTests>();

        _factory = new ServiceFactory(_dbFixture);
    }

    public async Task InitializeAsync()
    {
        // ✅ USAR UN SOLO DbContext para todo el setup para evitar problemas de tracking
        var dbContext = _dbFixture.CreateNewDbContext();

        var articleRepo = new ArticleRepository(dbContext, _logger);
        var feedRepo = new FeedRepository(dbContext, _logger);
        var categoryRepo = new CategoryRepository(dbContext, _logger);
        var articleTagRepo = new ArticleTagRepository(dbContext, _logger);
        var tagRepo = new TagRepository(dbContext, _logger);

        _articleService = new ArticleService(articleRepo, feedRepo, _logger);
        _tagService = new TagService(tagRepo, _logger);

        // ✅ USAR EL CONSTRUCTOR CON VALIDACIÓN DE ARTÍCULOS
        _articleTagService = new ArticleTagService(articleTagRepo, _tagService, articleRepo, _logger);

        await SetupTestDataAsync(dbContext);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Test Data Setup

    private async Task SetupTestDataAsync(RssReaderDbContext dbContext)
    {
        // Crear repositorios que compartan el mismo contexto
        var feedRepo = new FeedRepository(dbContext, _logger);
        var articleRepo = new ArticleRepository(dbContext, _logger);
        var tagRepo = new TagRepository(dbContext, _logger);

        // 1. Crear feed primero (necesario para artículos)
        var feed = new Feed
        {
            Title = "Test Feed",
            Url = $"https://test-{Guid.NewGuid():N}.com/rss",
            WebsiteUrl = "https://test.com",
            IsActive = true
        };

        await feedRepo.InsertAsync(feed);
        // ✅ NO asignar feed.Id manualmente - ya fue poblado por EF

        // 2. Crear artículos
        _testArticle1 = new Article
        {
            Title = $"Test Article 1 {Guid.NewGuid():N}",
            Content = "Content for article 1",
            FeedId = feed.Id,
            Guid = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddDays(-1),
            AddedDate = DateTime.UtcNow,
            Status = ArticleStatus.Unread
        };
        await articleRepo.InsertAsync(_testArticle1);
        // ✅ NO asignar _testArticle1.Id manualmente

        _testArticle2 = new Article
        {
            Title = $"Test Article 2 {Guid.NewGuid():N}",
            Content = "Content for article 2",
            FeedId = feed.Id,
            Guid = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow.AddDays(-2),
            AddedDate = DateTime.UtcNow,
            Status = ArticleStatus.Unread
        };
        await articleRepo.InsertAsync(_testArticle2);
        // ✅ NO asignar _testArticle2.Id manualmente

        // 3. Crear tags
        _testTag1 = new Tag
        {
            Name = $"Technology_{Guid.NewGuid():N}",
            Color = "#FF5733",
            Description = "Technology related articles"
        };
        await tagRepo.InsertAsync(_testTag1);
        // ✅ NO asignar _testTag1.Id manualmente

        _testTag2 = new Tag
        {
            Name = $"Science_{Guid.NewGuid():N}",
            Color = "#33FF57",
            Description = "Science related articles"
        };
        await tagRepo.InsertAsync(_testTag2);
        // ✅ NO asignar _testTag2.Id manualmente

        _testTag3 = new Tag
        {
            Name = $"Programming_{Guid.NewGuid():N}",
            Color = "#3357FF",
            Description = "Programming related articles"
        };
        await tagRepo.InsertAsync(_testTag3);
        // ✅ NO asignar _testTag3.Id manualmente

        // 4. Verificar que todo se creó correctamente usando el MISMO contexto
        var verifyArticle1 = await articleRepo.GetByIdAsync(_testArticle1.Id);
        if (verifyArticle1 == null)
            throw new InvalidOperationException($"Failed to create article 1 with ID {_testArticle1.Id}");

        var verifyArticle2 = await articleRepo.GetByIdAsync(_testArticle2.Id);
        if (verifyArticle2 == null)
            throw new InvalidOperationException($"Failed to create article 2 with ID {_testArticle2.Id}");

        var verifyTag1 = await tagRepo.GetByIdAsync(_testTag1.Id);
        if (verifyTag1 == null)
            throw new InvalidOperationException($"Failed to create tag 1 with ID {_testTag1.Id}");

        var verifyTag2 = await tagRepo.GetByIdAsync(_testTag2.Id);
        if (verifyTag2 == null)
            throw new InvalidOperationException($"Failed to create tag 2 with ID {_testTag2.Id}");

        var verifyTag3 = await tagRepo.GetByIdAsync(_testTag3.Id);
        if (verifyTag3 == null)
            throw new InvalidOperationException($"Failed to create tag 3 with ID {_testTag3.Id}");

        _logger.Information("Test data setup completed successfully. " +
            "Articles: {Article1Id}, {Article2Id}. Tags: {Tag1Id}, {Tag2Id}, {Tag3Id}",
            _testArticle1.Id, _testArticle2.Id, _testTag1.Id, _testTag2.Id, _testTag3.Id);
    }

    #endregion

    #region Basic Tagging Operations

    [Fact]
    public async Task TagArticleAsync_WithValidIds_ShouldCreateAssociation()
    {
        // Act
        var result = await _articleTagService.TagArticleAsync(
            _testArticle1.Id,
            _testTag1.Id,
            appliedBy: "user");

        // Assert
        result.Should().BeTrue();

        var isTagged = await _articleTagService.IsArticleTaggedAsync(
            _testArticle1.Id,
            _testTag1.Id);
        isTagged.Should().BeTrue();

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().Contain(t => t.Id == _testTag1.Id);
    }

    [Fact]
    public async Task TagArticleAsync_WithDuplicateTag_ShouldReturnFalse()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Act
        var result = await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UntagArticleAsync_WithExistingAssociation_ShouldRemoveAssociation()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Act
        var result = await _articleTagService.UntagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Assert
        result.Should().BeTrue();

        var isTagged = await _articleTagService.IsArticleTaggedAsync(
            _testArticle1.Id,
            _testTag1.Id);
        isTagged.Should().BeFalse();
    }

    [Fact]
    public async Task UntagArticleAsync_WithNonExistingAssociation_ShouldReturnFalse()
    {
        // Act
        var result = await _articleTagService.UntagArticleAsync(_testArticle1.Id, 99999);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsArticleTaggedAsync_WithExistingAssociation_ShouldReturnTrue()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Act
        var result = await _articleTagService.IsArticleTaggedAsync(
            _testArticle1.Id,
            _testTag1.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsArticleTaggedAsync_WithNonExistingAssociation_ShouldReturnFalse()
    {
        // Act
        var result = await _articleTagService.IsArticleTaggedAsync(_testArticle1.Id, 99999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Batch Operations

    [Fact]
    public async Task TagArticleWithMultipleAsync_WithValidTags_ShouldAssociateAll()
    {
        // Arrange
        var tagIds = new[] { _testTag1.Id, _testTag2.Id, _testTag3.Id };

        // Act
        var result = await _articleTagService.TagArticleWithMultipleAsync(
            _testArticle1.Id,
            tagIds,
            appliedBy: "user");

        // Assert
        result.Should().Be(3);

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(3);
        tags.Select(t => t.Id).Should().Contain(tagIds);
    }

    [Fact]
    public async Task TagArticleWithMultipleAsync_WithDuplicateTags_ShouldAssociateOnlyOnce()
    {
        // Arrange
        var tagIds = new[] { _testTag1.Id, _testTag1.Id, _testTag2.Id };

        // Act
        var result = await _articleTagService.TagArticleWithMultipleAsync(
            _testArticle1.Id,
            tagIds,
            appliedBy: "user");

        // Assert
        result.Should().Be(2); // Only two unique tags

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task UntagArticleMultipleAsync_WithExistingTags_ShouldRemoveAll()
    {
        // Arrange
        var tagIds = new[] { _testTag1.Id, _testTag2.Id, _testTag3.Id };
        await _articleTagService.TagArticleWithMultipleAsync(_testArticle1.Id, tagIds);

        // Act
        var result = await _articleTagService.UntagArticleMultipleAsync(
            _testArticle1.Id,
            new[] { _testTag1.Id, _testTag2.Id });

        // Assert
        result.Should().Be(2);

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(1);
        tags.Should().Contain(t => t.Id == _testTag3.Id);
    }

    [Fact]
    public async Task ReplaceArticleTagsAsync_WithNewTags_ShouldReplaceCompletely()
    {
        // Arrange
        var initialTags = new[] { _testTag1.Id, _testTag2.Id };
        await _articleTagService.TagArticleWithMultipleAsync(_testArticle1.Id, initialTags);

        // Act
        var newTags = new[] { _testTag2.Id, _testTag3.Id };
        var result = await _articleTagService.ReplaceArticleTagsAsync(
            _testArticle1.Id,
            newTags,
            appliedBy: "user");

        // Assert
        result.Should().Be(1); // Only _testTag3 is new

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(2);
        tags.Should().Contain(t => t.Id == _testTag2.Id);
        tags.Should().Contain(t => t.Id == _testTag3.Id);
        tags.Should().NotContain(t => t.Id == _testTag1.Id);
    }

    #endregion

    #region Retrieval Operations

    [Fact]
    public async Task GetTagsForArticleAsync_WithTaggedArticle_ShouldReturnAllTags()
    {
        // Arrange
        var tagIds = new[] { _testTag1.Id, _testTag2.Id };
        await _articleTagService.TagArticleWithMultipleAsync(_testArticle1.Id, tagIds);

        // Act
        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);

        // Assert
        tags.Should().HaveCount(2);
        tags.Should().Contain(t => t.Id == _testTag1.Id);
        tags.Should().Contain(t => t.Id == _testTag2.Id);
        tags.All(t => !string.IsNullOrEmpty(t.Name)).Should().BeTrue();
    }

    [Fact]
    public async Task GetTagsForArticleAsync_WithUntaggedArticle_ShouldReturnEmptyList()
    {
        // Act
        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);

        // Assert
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task GetArticleTagAssociationsAsync_WithTaggedArticle_ShouldReturnAssociations()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id, "rule", ruleId: 42, confidence: 0.95);

        // Act
        var associations = await _articleTagService.GetArticleTagAssociationsAsync(_testArticle1.Id);

        // Assert
        associations.Should().HaveCount(1);
        var association = associations.First();
        association.ArticleId.Should().Be(_testArticle1.Id);
        association.TagId.Should().Be(_testTag1.Id);
        association.AppliedBy.Should().Be("rule");
        association.RuleId.Should().Be(42);
        association.Confidence.Should().Be(0.95);
        association.AppliedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Statistics and Analysis

    [Fact]
    public async Task GetTagUsageCountsAsync_WithMultipleArticles_ShouldReturnCorrectCounts()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag2.Id);
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag1.Id);
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag2.Id);
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag3.Id);

        // Act
        var usageCounts = await _articleTagService.GetTagUsageCountsAsync();

        // Assert
        usageCounts.Should().ContainKey(_testTag1.Id).WhoseValue.Should().Be(2);
        usageCounts.Should().ContainKey(_testTag2.Id).WhoseValue.Should().Be(2);
        usageCounts.Should().ContainKey(_testTag3.Id).WhoseValue.Should().Be(1);
    }

    [Fact]
    public async Task GetMostUsedTagsAsync_WithMultipleTags_ShouldReturnOrderedByUsage()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag2.Id);
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag1.Id);
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag3.Id);

        // Act
        var mostUsed = await _articleTagService.GetMostUsedTagsAsync(2);

        // Assert
        mostUsed.Should().HaveCount(2);
        mostUsed[0].Id.Should().Be(_testTag1.Id); // Used twice
        mostUsed[1].Id.Should().BeOneOf(_testTag2.Id, _testTag3.Id); // Used once
    }

    [Fact]
    public async Task GetTaggingStatisticsAsync_WithDateRange_ShouldReturnFilteredStats()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow.AddDays(1);

        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id, "user");
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag2.Id, "rule", ruleId: 1);
        await Task.Delay(100); // Ensure different timestamps
        await _articleTagService.TagArticleAsync(_testArticle2.Id, _testTag1.Id, "system");

        // Act
        var stats = await _articleTagService.GetTaggingStatisticsAsync(startDate, endDate);

        // Assert
        stats["total_associations"].Should().Be(3);
        stats["user_applied"].Should().Be(1);
        stats["rule_applied"].Should().Be(1);
        stats["system_applied"].Should().Be(1);
    }

    #endregion

    #region Rule-based Tagging

    [Fact]
    public async Task ApplyRuleTaggingAsync_WithMultipleArticlesAndTags_ShouldAssociateAll()
    {
        // Arrange
        var articleIds = new[] { _testArticle1.Id, _testArticle2.Id };
        var tagIds = new[] { _testTag1.Id, _testTag2.Id };

        // Act
        var result = await _articleTagService.ApplyRuleTaggingAsync(
            ruleId: 42,
            articleIds: articleIds,
            tagIds: tagIds,
            confidence: 0.85);

        // Assert
        result.Should().Be(4); // 2 articles × 2 tags

        var article1Tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        var article2Tags = await _articleTagService.GetTagsForArticleAsync(_testArticle2.Id);

        article1Tags.Should().HaveCount(2);
        article2Tags.Should().HaveCount(2);

        // Verify metadata
        var associations = await _articleTagService.GetArticleTagAssociationsAsync(_testArticle1.Id);
        associations.First().RuleId.Should().Be(42);
        associations.First().AppliedBy.Should().Be("rule");
        associations.First().Confidence.Should().Be(0.85);
    }

    [Fact]
    public async Task RemoveRuleTagsAsync_WithRuleAppliedTags_ShouldRemoveOnlyThoseTags()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id, "user"); // User tag
        await _articleTagService.ApplyRuleTaggingAsync(
            ruleId: 42,
            articleIds: new[] { _testArticle1.Id },
            tagIds: new[] { _testTag2.Id, _testTag3.Id });

        // Act
        var removed = await _articleTagService.RemoveRuleTagsAsync(42);

        // Assert
        removed.Should().Be(2); // Only rule tags removed

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(1);
        tags.Should().Contain(t => t.Id == _testTag1.Id); // User tag remains
        tags.Should().NotContain(t => t.Id == _testTag2.Id);
        tags.Should().NotContain(t => t.Id == _testTag3.Id);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task TagArticleAsync_WhenSuccessful_ShouldRaiseArticleTaggedEvent()
    {
        // Arrange
        using var eventRaised = new ManualResetEventSlim(false);
        ArticleTaggedEventArgs? capturedArgs = null;

        _articleTagService.OnArticleTagged += (sender, args) =>
        {
            capturedArgs = args;
            eventRaised.Set();
        };

        // Act
        await _articleTagService.TagArticleAsync(
            _testArticle1.Id,
            _testTag1.Id,
            appliedBy: "rule",
            ruleId: 42);

        // Assert
        var eventFired = eventRaised.Wait(TimeSpan.FromSeconds(5));
        eventFired.Should().BeTrue("ArticleTagged event should be raised");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.ArticleId.Should().Be(_testArticle1.Id);
        capturedArgs.TagId.Should().Be(_testTag1.Id);
        capturedArgs.TagName.Should().Be(_testTag1.Name);
        capturedArgs.AppliedBy.Should().Be("rule");
        capturedArgs.RuleId.Should().Be(42);
    }

    [Fact]
    public async Task UntagArticleAsync_WhenSuccessful_ShouldRaiseArticleUntaggedEvent()
    {
        // Arrange
        await _articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id);

        using var eventRaised = new ManualResetEventSlim(false);
        ArticleUntaggedEventArgs? capturedArgs = null;

        _articleTagService.OnArticleUntagged += (sender, args) =>
        {
            capturedArgs = args;
            eventRaised.Set();
        };

        // Act
        await _articleTagService.UntagArticleAsync(_testArticle1.Id, _testTag1.Id);

        // Assert
        var eventFired = eventRaised.Wait(TimeSpan.FromSeconds(5));
        eventFired.Should().BeTrue("ArticleUntagged event should be raised");

        capturedArgs.Should().NotBeNull();
        capturedArgs!.ArticleId.Should().Be(_testArticle1.Id);
        capturedArgs.TagId.Should().Be(_testTag1.Id);
        capturedArgs.TagName.Should().Be(_testTag1.Name);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task TagArticleAsync_WithNonExistentArticle_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _articleTagService.TagArticleAsync(99999, _testTag1.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Article with ID 99999 does not exist*");
    }

    [Fact]
    public async Task TagArticleAsync_WithNonExistentTag_ShouldThrowException()
    {
        // Act
        Func<Task> act = async () => await _articleTagService.TagArticleAsync(_testArticle1.Id, 99999);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetTagsForArticleAsync_WithNonExistentArticle_ShouldReturnEmptyList()
    {
        // Act
        var tags = await _articleTagService.GetTagsForArticleAsync(99999);

        // Assert
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceArticleTagsAsync_WithEmptyList_ShouldRemoveAllTags()
    {
        // Arrange
        await _articleTagService.TagArticleWithMultipleAsync(
            _testArticle1.Id,
            new[] { _testTag1.Id, _testTag2.Id });

        // Act
        var result = await _articleTagService.ReplaceArticleTagsAsync(
            _testArticle1.Id,
            Enumerable.Empty<int>());

        // Assert
        result.Should().Be(0); // No new tags added

        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupOrphanedAssociationsAsync_ShouldHandleGracefully()
    {
        // Act
        var result = await _articleTagService.CleanupOrphanedAssociationsAsync();

        // Assert - No exception means success
        result.Should().Be(0);
    }

    [Fact]
    public async Task RecalculateTagUsageCountsAsync_ShouldHandleGracefully()
    {
        // Act
        var result = await _articleTagService.RecalculateTagUsageCountsAsync();

        // Assert - No exception means success
        result.Should().Be(0);
    }

    #endregion

    #region Concurrent Operations

    [Fact]
    public async Task MultipleConcurrentTaggingOperations_ShouldMaintainConsistency()
    {
        // Arrange
        var tasks = new List<Task<bool>>();

        // Act - Tag the same article with multiple tags concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_articleTagService.TagArticleAsync(_testArticle1.Id, _testTag1.Id));
            tasks.Add(_articleTagService.TagArticleAsync(_testArticle1.Id, _testTag2.Id));
        }

        await Task.WhenAll(tasks);

        // Assert
        var tags = await _articleTagService.GetTagsForArticleAsync(_testArticle1.Id);
        tags.Should().HaveCount(2); // Should only have two unique tags
    }

    #endregion
}