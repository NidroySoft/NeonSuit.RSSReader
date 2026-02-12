using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services;

public class RuleServiceTests
{
    private readonly Mock<IRuleRepository> _mockRuleRepository;
    private readonly Mock<IArticleRepository> _mockArticleRepository;
    private readonly Mock<IFeedRepository> _mockFeedRepository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly RuleService _ruleService;

    public RuleServiceTests()
    {
        _mockRuleRepository = new Mock<IRuleRepository>();
        _mockArticleRepository = new Mock<IArticleRepository>();
        _mockFeedRepository = new Mock<IFeedRepository>();
        _mockLogger = new Mock<ILogger>();

        _mockLogger.Setup(x => x.ForContext<RuleService>())
                  .Returns(_mockLogger.Object);

        _ruleService = new RuleService(
            _mockRuleRepository.Object,
            _mockArticleRepository.Object,
            _mockFeedRepository.Object,
            _mockLogger.Object);
    }

    #region Test Data Factories

    private Rule CreateTestRule(
        int id = 1,
        string name = "Test Rule",
        RuleFieldTarget target = RuleFieldTarget.Title,
        RuleOperator @operator = RuleOperator.Contains,
        string value = "AI",
        bool isEnabled = true,
        int priority = 100,
        RuleActionType actionType = RuleActionType.MarkAsRead,
        RuleScope scope = RuleScope.AllFeeds,
        bool stopOnMatch = false,
        bool usesAdvancedConditions = false)
    {
        return new Rule
        {
            Id = id,
            Name = name,
            Target = target,
            Operator = @operator,
            Value = value,
            IsEnabled = isEnabled,
            Priority = priority,
            ActionType = actionType,
            Scope = scope,
            StopOnMatch = stopOnMatch,
            UsesAdvancedConditions = usesAdvancedConditions,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastModified = DateTime.UtcNow,
            MatchCount = 0
        };
    }

    private Article CreateTestArticle(
        int id = 1,
        int feedId = 1,
        string title = "The Future of AI in Software Development",
        string content = "Artificial intelligence is revolutionizing how we write and test code.",
        string author = "Jane Developer",
        string categories = "Technology,Programming,AI")
    {
        return new Article
        {
            Id = id,
            FeedId = feedId,
            Title = title,
            Content = content,
            Summary = "AI tools are becoming essential for developers.",
            Author = author,
            Categories = categories,
            PublishedDate = DateTime.Now.AddDays(-1),
            Status = ArticleStatus.Unread,
            IsStarred = false
        };
    }

    private Feed CreateTestFeed(
        int id = 1,
        string name = "Tech Feed",
        int? categoryId = 1)
    {
        return new Feed
        {
            Id = id,
            Title = name,
            CategoryId = categoryId,
            Url = "https://example.com/feed",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region GetRuleByIdAsync Tests

    [Fact]
    public async Task GetRuleByIdAsync_WithValidId_ShouldReturnRule()
    {
        // Arrange
        var ruleId = 1;
        var expectedRule = CreateTestRule(id: ruleId);
        _mockRuleRepository.Setup(x => x.GetByIdAsync(ruleId))
            .ReturnsAsync(expectedRule);

        // Act
        var result = await _ruleService.GetRuleByIdAsync(ruleId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedRule);

    }

    [Fact]
    public async Task GetRuleByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var ruleId = 999;
        _mockRuleRepository.Setup(x => x.GetByIdAsync(ruleId))
            .ReturnsAsync((Rule?)null);

        // Act
        var result = await _ruleService.GetRuleByIdAsync(ruleId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRuleByIdAsync_WhenRepositoryThrows_ShouldThrow()
    {
        // Arrange
        var ruleId = 1;
        var expectedException = new Exception("Database error");
        _mockRuleRepository.Setup(x => x.GetByIdAsync(ruleId))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _ruleService.GetRuleByIdAsync(ruleId));
    }

    #endregion

    #region GetAllRulesAsync Tests

    [Fact]
    public async Task GetAllRulesAsync_ShouldReturnAllRules()
    {
        // Arrange
        var expectedRules = new List<Rule>
        {
            CreateTestRule(id: 1, name: "Rule 1"),
            CreateTestRule(id: 2, name: "Rule 2"),
            CreateTestRule(id: 3, name: "Rule 3")
        };
        _mockRuleRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _ruleService.GetAllRulesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedRules);
    }

    [Fact]
    public async Task GetAllRulesAsync_WhenNoRules_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRuleRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(new List<Rule>());

        // Act
        var result = await _ruleService.GetAllRulesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetActiveRulesAsync Tests

    [Fact]
    public async Task GetActiveRulesAsync_ShouldReturnOnlyEnabledRules()
    {
        // Arrange
        var expectedRules = new List<Rule>
        {
            CreateTestRule(id: 1, name: "Active Rule 1", isEnabled: true),
            CreateTestRule(id: 2, name: "Active Rule 2", isEnabled: true)
        };
        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _ruleService.GetActiveRulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.All(r => r.IsEnabled).Should().BeTrue();
    }

    #endregion

    #region CreateRuleAsync - JSON Validation Tests

    [Fact]
    public async Task CreateRuleAsync_WithValidRule_ShouldCreateAndReturnRule()
    {
        // Arrange
        var newRule = CreateTestRule(id: 0, name: "New Rule");

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .Callback<Rule>(r => r.Id = 1)
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.IsEnabled.Should().BeTrue();
        result.Priority.Should().Be(100);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.MatchCount.Should().Be(0);

        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Once);
    }

    [Fact]
    public async Task CreateRuleAsync_WithNullRule_ShouldThrowArgumentNullException()
    {
        // Act
        Func<Task> act = () => _ruleService.CreateRuleAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateRuleAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var ruleName = "Existing Rule";
        var newRule = CreateTestRule(name: ruleName);

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(ruleName))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithZeroPriority_ShouldSetDefaultPriority()
    {
        // Arrange
        var newRule = CreateTestRule(priority: 0);

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Priority.Should().Be(100);
    }

    [Fact]
    public async Task CreateRuleAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(name: "");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithNameTooLong_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(name: new string('A', 201)); // Max 200

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));
    }

    // ✅ TESTS DE VALIDACIÓN DE JSON - NUEVOS
    [Fact]
    public async Task CreateRuleAsync_WithSpecificFeedsAndValidFeedIds_ShouldCreateRule()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Feed Specific Rule",
            scope: RuleScope.SpecificFeeds);
        newRule.FeedIds = "[1, 2, 3]"; // ✅ JSON válido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Once);
    }

    // Línea 347-362 - Test de FeedIds malformado
    [Fact]
    public async Task CreateRuleAsync_WithSpecificFeedsAndMalformedFeedIds_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Malformed Feed Rule",
            scope: RuleScope.SpecificFeeds);
        newRule.FeedIds = "invalid json";

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        // ✅ CORREGIDO
        exception.Message.Should().Contain("FeedIds contiene JSON inválido");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    // Línea 409-424 - Test de CategoryIds malformado
    [Fact]
    public async Task CreateRuleAsync_WithSpecificCategoriesAndMalformedCategoryIds_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Malformed Category Rule",
            scope: RuleScope.SpecificCategories);
        newRule.CategoryIds = "[invalid";

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        // ✅ CORREGIDO
        exception.Message.Should().Contain("CategoryIds contiene JSON inválido");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    // Línea 461-476 - Test de TagIds malformado
    [Fact]
    public async Task CreateRuleAsync_WithApplyTagsActionAndMalformedTagIds_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Malformed Tags Rule",
            actionType: RuleActionType.ApplyTags);
        newRule.TagIds = "not json";

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        // ✅ CORREGIDO
        exception.Message.Should().Contain("TagIds contiene JSON inválido");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithSpecificFeedsAndEmptyFeedIds_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Empty Feed Rule",
            scope: RuleScope.SpecificFeeds);
        newRule.FeedIds = "";

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("FeedIds are required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithSpecificCategoriesAndValidCategoryIds_ShouldCreateRule()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Category Specific Rule",
            scope: RuleScope.SpecificCategories);
        newRule.CategoryIds = "[5, 6, 7]"; // ✅ JSON válido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Once);
    }


    [Fact]
    public async Task CreateRuleAsync_WithApplyTagsActionAndValidTagIds_ShouldCreateRule()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Apply Tags Rule",
            actionType: RuleActionType.ApplyTags);
        newRule.TagIds = "[10, 11, 12]"; // ✅ JSON válido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Once);
    }



    [Fact]
    public async Task CreateRuleAsync_WithApplyTagsActionAndEmptyTagIds_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Empty Tags Rule",
            actionType: RuleActionType.ApplyTags);
        newRule.TagIds = "";

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("TagIds are required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithMoveToCategoryActionAndNoCategoryId_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Move Category Rule",
            actionType: RuleActionType.MoveToCategory);
        newRule.CategoryId = null; // ❌ Requerido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("CategoryId is required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithHighlightArticleActionAndNoColor_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Highlight Rule",
            actionType: RuleActionType.HighlightArticle);
        newRule.HighlightColor = ""; // ❌ Requerido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("HighlightColor is required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithSimpleRuleAndRegexOperatorNoPattern_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "Regex Rule",
            @operator: RuleOperator.Regex,
            usesAdvancedConditions: false);
        newRule.RegexPattern = ""; // ❌ Requerido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("RegexPattern is required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithSimpleRuleAndOperatorRequiringValue_NoValue_ShouldThrowArgumentException()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "No Value Rule",
            @operator: RuleOperator.Contains,
            usesAdvancedConditions: false);
        newRule.Value = ""; // ❌ Requerido

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.CreateRuleAsync(newRule));

        exception.Message.Should().Contain("Value is required");
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task CreateRuleAsync_WithSimpleRuleAndIsEmptyOperator_NoValue_ShouldCreateRule()
    {
        // Arrange
        var newRule = CreateTestRule(
            name: "IsEmpty Rule",
            @operator: RuleOperator.IsEmpty,
            usesAdvancedConditions: false);
        newRule.Value = ""; // ✅ Válido para IsEmpty

        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(newRule.Name))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.CreateRuleAsync(newRule);

        // Assert
        result.Should().NotBeNull();
        _mockRuleRepository.Verify(x => x.InsertAsync(It.IsAny<Rule>()), Times.Once);
    }

    #endregion

    #region UpdateRuleAsync - JSON Validation Tests

    [Fact]
    public async Task UpdateRuleAsync_WithValidRule_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var existingRule = CreateTestRule(id: 1, name: "Existing Rule");
        var updatedRule = CreateTestRule(id: 1, name: "Updated Rule");

        _mockRuleRepository.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(existingRule);
        _mockRuleRepository.Setup(x => x.UpdateAsync(updatedRule))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.UpdateRuleAsync(updatedRule);

        // Assert
        result.Should().BeTrue();
        _mockRuleRepository.Verify(x => x.UpdateAsync(It.IsAny<Rule>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRuleAsync_WithNonExistentRule_ShouldReturnFalse()
    {
        // Arrange
        var rule = CreateTestRule(id: 999);
        _mockRuleRepository.Setup(x => x.GetByIdAsync(999))
            .ReturnsAsync((Rule?)null);

        // Act
        var result = await _ruleService.UpdateRuleAsync(rule);

        // Assert
        result.Should().BeFalse();
        _mockRuleRepository.Verify(x => x.UpdateAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRuleAsync_WithNullRule_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _ruleService.UpdateRuleAsync(null!));
    }

    // Línea 645-660 - Test de FeedIds malformado en Update
    [Fact]
    public async Task UpdateRuleAsync_WithMalformedFeedIds_ShouldThrowArgumentException()
    {
        // Arrange
        var existingRule = CreateTestRule(id: 1, name: "Existing Rule");
        var updatedRule = CreateTestRule(
            id: 1,
            name: "Updated Rule",
            scope: RuleScope.SpecificFeeds);
        updatedRule.FeedIds = "bad json";

        _mockRuleRepository.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(existingRule);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.UpdateRuleAsync(updatedRule));

        // ✅ CORREGIDO
        exception.Message.Should().Contain("FeedIds contiene JSON inválido");
        _mockRuleRepository.Verify(x => x.UpdateAsync(It.IsAny<Rule>()), Times.Never);
    }

    // Línea 662-677 - Test de CategoryIds malformado en Update
    [Fact]
    public async Task UpdateRuleAsync_WithMalformedCategoryIds_ShouldThrowArgumentException()
    {
        // Arrange
        var existingRule = CreateTestRule(id: 1, name: "Existing Rule");
        var updatedRule = CreateTestRule(
            id: 1,
            name: "Updated Rule",
            scope: RuleScope.SpecificCategories);
        updatedRule.CategoryIds = "[1,2";

        _mockRuleRepository.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(existingRule);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _ruleService.UpdateRuleAsync(updatedRule));

        // ✅ CORREGIDO
        exception.Message.Should().Contain("CategoryIds contiene JSON inválido");
        _mockRuleRepository.Verify(x => x.UpdateAsync(It.IsAny<Rule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRuleAsync_WhenNoChangesMade_ShouldReturnFalse()
    {
        // Arrange
        var existingRule = CreateTestRule(id: 1);
        var sameRule = CreateTestRule(id: 1);

        _mockRuleRepository.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(existingRule);
        _mockRuleRepository.Setup(x => x.UpdateAsync(sameRule))
            .ReturnsAsync(0);

        // Act
        var result = await _ruleService.UpdateRuleAsync(sameRule);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteRuleAsync Tests

    [Fact]
    public async Task DeleteRuleAsync_WithExistingId_ShouldReturnTrue()
    {
        // Arrange
        var ruleId = 1;
        _mockRuleRepository.Setup(x => x.DeleteAsync(ruleId))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.DeleteRuleAsync(ruleId);

        // Assert
        result.Should().BeTrue();
        _mockRuleRepository.Verify(x => x.DeleteAsync(ruleId), Times.Once);
    }

    [Fact]
    public async Task DeleteRuleAsync_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        var ruleId = 999;
        _mockRuleRepository.Setup(x => x.DeleteAsync(ruleId))
            .ReturnsAsync(0);

        // Act
        var result = await _ruleService.DeleteRuleAsync(ruleId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region EvaluateArticleAgainstRulesAsync Tests

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithMatchingRule_ShouldReturnMatchedRules()
    {
        // Arrange
        var article = CreateTestArticle(title: "AI News");
        var rule = CreateTestRule(
            id: 1,
            target: RuleFieldTarget.Title,
            @operator: RuleOperator.Contains,
            value: "AI",
            isEnabled: true);

        var feed = CreateTestFeed(id: article.FeedId);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(rule.Id))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(rule))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(rule.Id), Times.Once);
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithNonMatchingRule_ShouldReturnEmptyList()
    {
        // Arrange
        var article = CreateTestArticle(title: "Sports News");
        var rule = CreateTestRule(
            target: RuleFieldTarget.Title,
            @operator: RuleOperator.Contains,
            value: "AI",
            isEnabled: true);

        var feed = CreateTestFeed(id: article.FeedId);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().BeEmpty();
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithNullArticle_ShouldReturnEmptyList()
    {
        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(null!);

        // Assert
        result.Should().BeEmpty();
        _mockRuleRepository.Verify(x => x.GetActiveRulesAsync(), Times.Never);
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithNoActiveRules_ShouldReturnEmptyList()
    {
        // Arrange
        var article = CreateTestArticle();
        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule>());

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithStopOnMatch_ShouldStopEvaluation()
    {
        // Arrange
        var article = CreateTestArticle(title: "AI Technology");
        var rule1 = CreateTestRule(
            id: 1,
            name: "First Rule",
            value: "AI",
            stopOnMatch: true);
        var rule2 = CreateTestRule(
            id: 2,
            name: "Second Rule",
            value: "Technology");

        var feed = CreateTestFeed(id: article.FeedId);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule1, rule2 });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(It.IsAny<int>()))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(It.IsAny<Rule>()))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(1), Times.Once);
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(2), Times.Never);
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithSpecificFeedsScope_ShouldRespectScope()
    {
        // Arrange
        var article = CreateTestArticle(feedId: 1);
        var rule = CreateTestRule(
            value: "AI",
            scope: RuleScope.SpecificFeeds);
        rule.FeedIds = "[1]";

        var feed = CreateTestFeed(id: 1);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(rule.Id))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(rule))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WithSpecificFeedsScope_FeedNotInList_ShouldNotMatch()
    {
        // Arrange
        var article = CreateTestArticle(feedId: 2);
        var rule = CreateTestRule(
            value: "AI",
            scope: RuleScope.SpecificFeeds);
        rule.FeedIds = "[1]";

        var feed = CreateTestFeed(id: 2);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WhenFeedNotFound_ShouldSkipRule()
    {
        // Arrange
        var article = CreateTestArticle();
        var rule = CreateTestRule(isEnabled: true);

        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ReturnsAsync(new List<Rule> { rule });
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync((Feed?)null);

        // Act
        var result = await _ruleService.EvaluateArticleAgainstRulesAsync(article);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ExecuteRuleActionsAsync Tests

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithMarkAsReadAction_ShouldUpdateArticle()
    {
        // Arrange
        var rule = CreateTestRule(actionType: RuleActionType.MarkAsRead);
        var article = CreateTestArticle();

        var feed = CreateTestFeed(id: article.FeedId);
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(rule.Id))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(rule))
            .ReturnsAsync(1);
        _mockArticleRepository.Setup(x => x.UpdateAsync(article))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(rule, article);

        // Assert
        result.Should().BeTrue();
        article.Status.Should().Be(ArticleStatus.Read);
        _mockArticleRepository.Verify(x => x.UpdateAsync(article), Times.Once);
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(rule.Id), Times.Once);
    }

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithMarkAsStarredAction_ShouldStarArticle()
    {
        // Arrange
        var rule = CreateTestRule(actionType: RuleActionType.MarkAsStarred);
        var article = CreateTestArticle();

        var feed = CreateTestFeed(id: article.FeedId);
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(rule.Id))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(rule))
            .ReturnsAsync(1);
        _mockArticleRepository.Setup(x => x.UpdateAsync(article))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(rule, article);

        // Assert
        result.Should().BeTrue();
        article.IsStarred.Should().BeTrue();
        _mockArticleRepository.Verify(x => x.UpdateAsync(article), Times.Once);
    }

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithMoveToCategoryAction_ShouldUpdateFeedCategory()
    {
        // Arrange
        var rule = CreateTestRule(actionType: RuleActionType.MoveToCategory);
        rule.CategoryId = 2;

        var article = CreateTestArticle(feedId: 1);
        var feed = CreateTestFeed(id: 1, categoryId: 1);

        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);
        _mockFeedRepository.Setup(x => x.UpdateAsync(feed))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.IncrementMatchCountAsync(rule.Id))
            .ReturnsAsync(1);
        _mockRuleRepository.Setup(x => x.UpdateAsync(rule))
            .ReturnsAsync(1);

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(rule, article);

        // Assert
        result.Should().BeTrue();
        feed.CategoryId.Should().Be(2);
        _mockFeedRepository.Verify(x => x.UpdateAsync(feed), Times.Once);
    }

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithNonMatchingRule_ShouldReturnFalse()
    {
        // Arrange
        var rule = CreateTestRule(value: "XYZ123");
        var article = CreateTestArticle(title: "Sports News Today");

        var feed = CreateTestFeed(id: article.FeedId);
        _mockFeedRepository.Setup(x => x.GetByIdAsync(article.FeedId))
            .ReturnsAsync(feed);

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(rule, article);

        // Assert
        result.Should().BeFalse();
        _mockArticleRepository.Verify(x => x.UpdateAsync(It.IsAny<Article>()), Times.Never);
        _mockRuleRepository.Verify(x => x.IncrementMatchCountAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithNullRule_ShouldReturnFalse()
    {
        // Arrange
        var article = CreateTestArticle();

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(null!, article);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteRuleActionsAsync_WithNullArticle_ShouldReturnFalse()
    {
        // Arrange
        var rule = CreateTestRule();

        // Act
        var result = await _ruleService.ExecuteRuleActionsAsync(rule, null!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetTotalMatchCountAsync Tests

    [Fact]
    public async Task GetTotalMatchCountAsync_ShouldReturnCountFromRepository()
    {
        // Arrange
        var expectedCount = 42;
        _mockRuleRepository.Setup(x => x.GetTotalMatchCountAsync())
            .ReturnsAsync(expectedCount);

        // Act
        var result = await _ruleService.GetTotalMatchCountAsync();

        // Assert
        result.Should().Be(expectedCount);
    }

    #endregion

    #region GetTopRulesByMatchCountAsync Tests

    [Fact]
    public async Task GetTopRulesByMatchCountAsync_ShouldReturnTopRules()
    {
        // Arrange
        var rule1 = CreateTestRule(id: 1, name: "Top Rule");
        rule1.MatchCount = 100;
        var rule2 = CreateTestRule(id: 2, name: "Second Rule");
        rule2.MatchCount = 50;
        var expectedRules = new List<Rule> { rule1, rule2 };

        _mockRuleRepository.Setup(x => x.GetTopRulesByMatchCountAsync(10))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _ruleService.GetTopRulesByMatchCountAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedRules);
    }

    [Fact]
    public async Task GetTopRulesByMatchCountAsync_WithCustomLimit_ShouldRespectLimit()
    {
        // Arrange
        var limit = 5;
        _mockRuleRepository.Setup(x => x.GetTopRulesByMatchCountAsync(limit))
            .ReturnsAsync(new List<Rule>());

        // Act
        await _ruleService.GetTopRulesByMatchCountAsync(limit);

        // Assert
        _mockRuleRepository.Verify(x => x.GetTopRulesByMatchCountAsync(limit), Times.Once);
    }

    #endregion

    #region ValidateRuleAsync Tests

    [Fact]
    public async Task ValidateRuleAsync_WithValidRule_ShouldReturnTrue()
    {
        // Arrange
        var rule = CreateTestRule(name: "Valid Rule");

        // Act
        var result = await _ruleService.ValidateRuleAsync(rule);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRuleAsync_WithNullRule_ShouldReturnFalse()
    {
        // Act
        var result = await _ruleService.ValidateRuleAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task ValidateRuleAsync_WithInvalidName_ShouldReturnFalse(string ruleName)
    {
        // Arrange
        var rule = CreateTestRule(name: ruleName!);

        // Act
        var result = await _ruleService.ValidateRuleAsync(rule);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRuleAsync_WithMalformedFeedIds_ShouldReturnFalse()
    {
        // Arrange
        var rule = CreateTestRule(
            name: "Invalid JSON",
            scope: RuleScope.SpecificFeeds);
        rule.FeedIds = "bad json";

        // Act
        var result = await _ruleService.ValidateRuleAsync(rule);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RuleExistsByNameAsync Tests

    [Fact]
    public async Task RuleExistsByNameAsync_WithExistingName_ShouldReturnTrue()
    {
        // Arrange
        var ruleName = "Existing Rule";
        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(ruleName))
            .ReturnsAsync(true);

        // Act
        var result = await _ruleService.RuleExistsByNameAsync(ruleName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RuleExistsByNameAsync_WithNonExistingName_ShouldReturnFalse()
    {
        // Arrange
        var ruleName = "Non-existing Rule";
        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(ruleName))
            .ReturnsAsync(false);

        // Act
        var result = await _ruleService.RuleExistsByNameAsync(ruleName);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CreateRuleAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var rule = CreateTestRule();
        _mockRuleRepository.Setup(x => x.RuleExistsByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockRuleRepository.Setup(x => x.InsertAsync(It.IsAny<Rule>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _ruleService.CreateRuleAsync(rule));
    }

    [Fact]
    public async Task UpdateRuleAsync_WhenRepositoryThrows_ShouldPropagateException()
    {
        // Arrange
        var rule = CreateTestRule(id: 1);
        _mockRuleRepository.Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(rule);
        _mockRuleRepository.Setup(x => x.UpdateAsync(It.IsAny<Rule>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _ruleService.UpdateRuleAsync(rule));
    }

    [Fact]
    public async Task EvaluateArticleAgainstRulesAsync_WhenExceptionOccurs_ShouldThrow()
    {
        // Arrange
        var article = CreateTestArticle();
        _mockRuleRepository.Setup(x => x.GetActiveRulesAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _ruleService.EvaluateArticleAgainstRulesAsync(article));
    }

    #endregion
}