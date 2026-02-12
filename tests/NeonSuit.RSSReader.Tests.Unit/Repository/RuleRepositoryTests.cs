using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;
using System.Text.Json;

namespace NeonSuit.RSSReader.Tests.Unit.Repository;

[CollectionDefinition("Database_Rule")]
public class RuleData : ICollectionFixture<DatabaseFixture> { }

[Collection("Database_Rule")]
public class RuleRepositoryTests : IAsyncLifetime
{
    private readonly RssReaderDbContext _dbContext;
    private readonly RuleRepository _repository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DatabaseFixture _fixture;

    public RuleRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockLogger = new Mock<ILogger>();
        SetupMockLogger();

        _dbContext = fixture.Context;
        _repository = new RuleRepository(_dbContext, _mockLogger.Object);
    }

    private void SetupMockLogger()
    {
        _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
            .Returns(_mockLogger.Object);
        _mockLogger.Setup(x => x.ForContext<RuleRepository>())
            .Returns(_mockLogger.Object);

        _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()));
        _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()));
        _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()));
        _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()));
    }

    public async Task InitializeAsync()
    {
        await ClearTestData();
        await SeedTestData();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task ClearTestData()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");
        _dbContext.ChangeTracker.Clear();

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RuleConditions;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Rules;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('RuleConditions', 'Rules');");

        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    private async Task SeedTestData()
    {
        // ✅ Rules con IDs NEGATIVOS - SIN FeedId
        var rules = new List<Rule>
        {
            new Rule
            {
                Id = -1001,
                Name = "Important Content Rule",
                Description = "Detect important articles",
                Target = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "critical",
                Scope = RuleScope.AllFeeds,
                IsEnabled = true,
                ActionType = RuleActionType.MarkAsRead,
                Priority = 100,
                UsesAdvancedConditions = false,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastModified = DateTime.UtcNow.AddDays(-1),
                MatchCount = 42,
                LastMatchDate = DateTime.UtcNow.AddHours(-5)
            },
            new Rule
            {
                Id = -1002,
                Name = "Admin Posts Rule",
                Description = "Highlight admin posts",
                Target = RuleFieldTarget.Author,
                Operator = RuleOperator.Equals,
                Value = "admin",
                Scope = RuleScope.SpecificFeeds,
                FeedIds = "[-2001, -2002]",
                IsEnabled = true,
                ActionType = RuleActionType.HighlightArticle,
                HighlightColor = "#FFFF00",
                Priority = 200,
                UsesAdvancedConditions = false,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                LastModified = DateTime.UtcNow.AddDays(-2),
                MatchCount = 18,
                LastMatchDate = DateTime.UtcNow.AddHours(-12)
            },
            new Rule
            {
                Id = -1003,
                Name = "Tech News Rule",
                Description = "Technology category rule",
                Target = RuleFieldTarget.Categories,
                Operator = RuleOperator.Contains,
                Value = "technology",
                Scope = RuleScope.SpecificCategories,
                CategoryIds = "[-3001]",
                IsEnabled = true,
                ActionType = RuleActionType.ApplyTags,
                TagIds = "[1, 2]",
                Priority = 150,
                UsesAdvancedConditions = false,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                LastModified = DateTime.UtcNow.AddDays(-3),
                MatchCount = 31,
                LastMatchDate = DateTime.UtcNow.AddHours(-24)
            },
            new Rule
            {
                Id = -1004,
                Name = "Disabled Test Rule",
                Description = "This rule is disabled",
                Target = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "test",
                Scope = RuleScope.AllFeeds,
                IsEnabled = false,
                ActionType = RuleActionType.MarkAsStarred,
                Priority = 300,
                UsesAdvancedConditions = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                LastModified = DateTime.UtcNow.AddDays(-1),
                MatchCount = 0,
                LastMatchDate = null
            },
            new Rule
            {
                Id = -1005,
                Name = "Advanced Conditions Rule",
                Description = "Rule with multiple conditions",
                Target = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "default",
                Scope = RuleScope.AllFeeds,
                IsEnabled = true,
                ActionType = RuleActionType.SendNotification,
                NotificationTemplate = "Alert: {Title}",
                NotificationPriority = NotificationPriority.High,
                Priority = 50,
                UsesAdvancedConditions = true,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                LastModified = DateTime.UtcNow.AddDays(-1),
                MatchCount = 7,
                LastMatchDate = DateTime.UtcNow.AddHours(-3)
            }
        };

        await _dbContext.Rules.AddRangeAsync(rules);
        await _dbContext.SaveChangesAsync();

        // ✅ RuleConditions para reglas avanzadas - IDs NEGATIVOS
        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                Id = -5001,
                RuleId = -1005,
                GroupId = 1,
                Order = 1,
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "urgent",
                Negate = false,
                CombineWithNext = LogicalOperator.AND,
                IsCaseSensitive = false
            },
            new RuleCondition
            {
                Id = -5002,
                RuleId = -1005,
                GroupId = 1,
                Order = 2,
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "critical",
                Negate = false,
                CombineWithNext = LogicalOperator.OR,
                IsCaseSensitive = false
            },
            new RuleCondition
            {
                Id = -5003,
                RuleId = -1005,
                GroupId = 2,
                Order = 1,
                Field = RuleFieldTarget.Author,
                Operator = RuleOperator.Equals,
                Value = "system",
                Negate = false,
                CombineWithNext = LogicalOperator.AND,
                IsCaseSensitive = true
            }
        };

        await _dbContext.RuleConditions.AddRangeAsync(conditions);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
    }

    #region GetActiveRulesAsync

    [Fact]
    public async Task GetActiveRulesAsync_ReturnsOnlyEnabledRules()
    {
        // Act
        var result = await _repository.GetActiveRulesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4);
        result.Should().NotContain(r => r.Id == -1004);
        result.Should().AllSatisfy(r => r.IsEnabled.Should().BeTrue());
    }

    [Fact]
    public async Task GetActiveRulesAsync_WithAdvancedRules_LoadsConditions()
    {
        // Act
        var result = await _repository.GetActiveRulesAsync();
        var advancedRule = result.FirstOrDefault(r => r.Id == -1005);

        // Assert
        advancedRule.Should().NotBeNull();
        advancedRule!.UsesAdvancedConditions.Should().BeTrue();
        advancedRule.Conditions.Should().NotBeNull();
        advancedRule.Conditions.Should().HaveCount(3);
        advancedRule.Conditions.Should().BeInAscendingOrder(c => c.GroupId).And.ThenBeInAscendingOrder(c => c.Order);
    }

    [Fact]
    public async Task GetActiveRulesAsync_WithNoActiveRules_ReturnsEmptyList()
    {
        // Arrange
        await _dbContext.Database.ExecuteSqlRawAsync("UPDATE Rules SET IsEnabled = 0;");
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetActiveRulesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetRulesByPriorityAsync

    [Fact]
    public async Task GetRulesByPriorityAsync_ReturnsRulesOrderedByPriority()
    {
        // Act
        var result = await _repository.GetRulesByPriorityAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeInAscendingOrder(r => r.Priority);
        result.Should().AllSatisfy(r => r.IsEnabled.Should().BeTrue());
    }

    [Fact]
    public async Task GetRulesByPriorityAsync_WithSamePriority_OrdersByName()
    {
        // Arrange - Crear dos reglas con mismo priority
        var rule1 = new Rule
        {
            Id = -1101,
            Name = "Aardvark Rule",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsRead,
            Priority = 500
        };

        var rule2 = new Rule
        {
            Id = -1102,
            Name = "Zebra Rule",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsRead,
            Priority = 500
        };

        await _dbContext.Rules.AddRangeAsync(rule1, rule2);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetRulesByPriorityAsync();
        var samePriorityRules = result.Where(r => r.Priority == 500).ToList();

        // Assert
        samePriorityRules.Should().BeInAscendingOrder(r => r.Name);
    }

    #endregion

    #region GetRulesByFeedIdAsync

    [Fact]
    public async Task GetRulesByFeedIdAsync_WithAllFeedsScope_ReturnsRules()
    {
        // Act
        var result = await _repository.GetRulesByFeedIdAsync(-2001);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(r => r.Id == -1001);
        result.Should().Contain(r => r.Id == -1004);
        result.Should().Contain(r => r.Id == -1005);
    }

    [Fact]
    public async Task GetRulesByFeedIdAsync_WithSpecificFeedsScope_ReturnsMatchingRules()
    {
        // Act
        var result = await _repository.GetRulesByFeedIdAsync(-2001);

        // Assert
        result.Should().Contain(r => r.Id == -1002);
    }

    [Fact]
    public async Task GetRulesByFeedIdAsync_WithNonMatchingFeedId_ReturnsOnlyApplicableRules()
    {
        // Act
        var result = await _repository.GetRulesByFeedIdAsync(-9999);

        // Assert
        result.Should().NotBeNull();

        // ✅ CORREGIDO: Debe incluir AllFeeds Y SpecificCategories (sin filtro de feed)
        result.Should().OnlyContain(r =>
            r.Scope == RuleScope.AllFeeds ||
            r.Scope == RuleScope.SpecificCategories);

        // ✅ Verificar que NO incluye SpecificFeeds (porque el feed no coincide)
        result.Should().NotContain(r => r.Scope == RuleScope.SpecificFeeds);
    }

    #endregion

    #region GetRulesByCategoryIdAsync

    [Fact]
    public async Task GetRulesByCategoryIdAsync_WithMatchingCategory_ReturnsRules()
    {
        // Act
        var result = await _repository.GetRulesByCategoryIdAsync(-3001);

        // Assert
        result.Should().Contain(r => r.Id == -1003);
    }

    [Fact]
    public async Task GetRulesByCategoryIdAsync_WithNonMatchingCategory_ReturnsOnlyAllFeedsRules()
    {
        // Act
        var result = await _repository.GetRulesByCategoryIdAsync(-9999);

        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(r => r.Scope.Should().Be(RuleScope.AllFeeds));
    }

    #endregion

    #region InsertAsync

    [Fact]
    public async Task InsertAsync_WithSimpleRule_AddsToDatabaseAndSetsTimestamps()
    {
        // Arrange
        var beforeInsert = DateTime.UtcNow;

        var rule = new Rule
        {
            Id = -1201,
            Name = "New Test Rule",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.StartsWith,
            Value = "[IMPORTANT]",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsStarred,
            Priority = 400,
            UsesAdvancedConditions = false
        };

        // Act
        var result = await _repository.InsertAsync(rule);
        _dbContext.ChangeTracker.Clear();
        var inserted = await _dbContext.Rules.FindAsync(-1201);

        // Assert
        result.Should().Be(1);
        inserted.Should().NotBeNull();
        inserted!.Name.Should().Be("New Test Rule");
        inserted.CreatedAt.Should().BeOnOrAfter(beforeInsert);
        inserted.LastModified.Should().BeOnOrAfter(beforeInsert);
        inserted.MatchCount.Should().Be(0);
        inserted.LastMatchDate.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_WithAdvancedRule_InsertsConditions()
    {
        // Arrange
        var rule = new Rule
        {
            Id = -1202,
            Name = "Advanced Test Rule",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.SendNotification,
            Priority = 450,
            UsesAdvancedConditions = true,
            Conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                Id = 0,
                GroupId = 1,
                Order = 1,
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "urgent"
            },
            new RuleCondition
            {
                Id = 0,
                GroupId = 1,
                Order = 2,
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "critical"
            }
        }
        };

        // ✅ RESETEAR SECUENCIA
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name='RuleConditions';");
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.InsertAsync(rule);
        _dbContext.ChangeTracker.Clear();

        var inserted = await _dbContext.Rules.FindAsync(-1202);
        var conditions = await _dbContext.RuleConditions
            .Where(c => c.RuleId == -1202)
            .ToListAsync();

        // Assert
        result.Should().Be(3); // ✅ 1 regla + 2 condiciones = 3 filas afectadas
        inserted.Should().NotBeNull();
        conditions.Should().HaveCount(4);
        conditions.Should().AllSatisfy(c => c.RuleId.Should().Be(-1202));
    }

    [Fact]
    public async Task InsertAsync_WithNullRule_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _repository.InsertAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("rule");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WithExistingRule_UpdatesPropertiesAndTimestamp()
    {
        // Arrange
        var rule = await _dbContext.Rules.FindAsync(-1001);
        rule!.Name = "Updated Rule Name";
        rule!.Description = "Updated description";
        rule!.Priority = 999;

        var beforeUpdate = DateTime.UtcNow;

        // Act
        var result = await _repository.UpdateAsync(rule);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        updated!.Name.Should().Be("Updated Rule Name");
        updated.Description.Should().Be("Updated description");
        updated.Priority.Should().Be(999);
        updated.LastModified.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task UpdateAsync_WithAdvancedRule_ReplacesConditions()
    {
        // Arrange
        var rule = await _dbContext.Rules
            .FirstOrDefaultAsync(r => r.Id == -1005);  // ✅ Usar el ID del seed

        // ✅ Recargar la entidad para evitar concurrency issues
        await _dbContext.Entry(rule ?? throw new InvalidOperationException()).ReloadAsync();

        rule!.UsesAdvancedConditions = true;
        rule!.Conditions = new List<RuleCondition>
    {
        new RuleCondition
        {
            Id = 0,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Author,
            Operator = RuleOperator.Equals,
            Value = "newadmin"
        }
    };

        // Act
        var result = await _repository.UpdateAsync(rule);
        _dbContext.ChangeTracker.Clear();

        var conditions = await _dbContext.RuleConditions
            .Where(c => c.RuleId == -1005)
            .ToListAsync();

        // Assert
        result.Should().Be(2);
        conditions.Should().HaveCount(1);
        conditions.First().Value.Should().Be("newadmin");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingRule_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var nonExisting = new Rule
        {
            Id = -9999,
            Name = "Non Existing",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsRead
        };

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(nonExisting);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WithExistingRule_RemovesRuleAndConditions()
    {
        // Act
        var result = await _repository.DeleteAsync(-1005);
        _dbContext.ChangeTracker.Clear();

        var deleted = await _dbContext.Rules.FindAsync(-1005);
        var conditions = await _dbContext.RuleConditions
            .Where(c => c.RuleId == -1005)
            .ToListAsync();

        // Assert
        result.Should().Be(1);
        deleted.Should().BeNull();
        conditions.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingId_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteAsync(-9999);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region IncrementMatchCountAsync

    [Fact]
    public async Task IncrementMatchCountAsync_WithExistingRule_IncrementsCountAndUpdatesLastMatchDate()
    {
        // Arrange
        var beforeIncrement = DateTime.UtcNow;
        var rule = await _dbContext.Rules.FindAsync(-1001);
        var initialCount = rule!.MatchCount;

        // Act
        var result = await _repository.IncrementMatchCountAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        updated!.MatchCount.Should().Be(initialCount + 1);
        updated.LastMatchDate.Should().BeOnOrAfter(beforeIncrement);
    }

    [Fact]
    public async Task IncrementMatchCountAsync_WithNonExistingRule_ReturnsZero()
    {
        // Act
        var result = await _repository.IncrementMatchCountAsync(-9999);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetRuleConditionsAsync

    [Fact]
    public async Task GetRuleConditionsAsync_WithExistingRule_ReturnsOrderedConditions()
    {
        // Act
        var result = await _repository.GetRuleConditionsAsync(-1005);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(c => c.GroupId)
            .And.ThenBeInAscendingOrder(c => c.Order);
    }

    [Fact]
    public async Task GetRuleConditionsAsync_WithRuleHavingNoConditions_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetRuleConditionsAsync(-1001);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region RuleExistsByNameAsync

    [Fact]
    public async Task RuleExistsByNameAsync_WithExistingName_ReturnsTrue()
    {
        // Act
        var result = await _repository.RuleExistsByNameAsync("Important Content Rule");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RuleExistsByNameAsync_WithNonExistingName_ReturnsFalse()
    {
        // Act
        var result = await _repository.RuleExistsByNameAsync("Non Existent Rule");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RuleExistsByNameAsync_WithCaseSensitivity_ReturnsFalse()
    {
        // Act
        var result = await _repository.RuleExistsByNameAsync("important content rule");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UpdateLastMatchDateAsync

    [Fact]
    public async Task UpdateLastMatchDateAsync_WithExistingRule_UpdatesDate()
    {
        // Arrange
        var beforeUpdate = DateTime.UtcNow;
        var rule = await _dbContext.Rules.FindAsync(-1001);
        var lastMatchDate = rule!.LastMatchDate;

        // Act
        var result = await _repository.UpdateLastMatchDateAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        updated!.LastMatchDate.Should().NotBe(lastMatchDate);
        updated.LastMatchDate.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task UpdateLastMatchDateAsync_WithNonExistingRule_ReturnsZero()
    {
        // Act
        var result = await _repository.UpdateLastMatchDateAsync(-9999);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetTotalMatchCountAsync

    [Fact]
    public async Task GetTotalMatchCountAsync_ReturnsSumOfAllMatchCounts()
    {
        // Act
        var result = await _repository.GetTotalMatchCountAsync();

        // Assert
        result.Should().Be(42 + 18 + 31 + 0 + 7); // 98
    }

    [Fact]
    public async Task GetTotalMatchCountAsync_WithNoRules_ReturnsZero()
    {
        // Arrange
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Rules;");
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetTotalMatchCountAsync();

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region GetTopRulesByMatchCountAsync

    [Fact]
    public async Task GetTopRulesByMatchCountAsync_ReturnsRulesOrderedByMatchCount()
    {
        // Act
        var result = await _repository.GetTopRulesByMatchCountAsync(3);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(r => r.MatchCount);
        result.First().Id.Should().Be(-1001); // 42 matches
    }

    [Fact]
    public async Task GetTopRulesByMatchCountAsync_WithLimitSmallerThanTotal_RespectsLimit()
    {
        // Act
        var result = await _repository.GetTopRulesByMatchCountAsync(2);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region GetRulesForArticleEvaluationAsync

    [Fact]
    public async Task GetRulesForArticleEvaluationAsync_WithFeedId_ReturnsApplicableRules()
    {
        // Act
        var result = await _repository.GetRulesForArticleEvaluationAsync(-2001, null);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(r => r.Id == -1001);
        result.Should().Contain(r => r.Id == -1002);
        result.Should().Contain(r => r.Id == -1005);
        result.Should().NotContain(r => r.Id == -1003); // Category rule
        result.Should().NotContain(r => r.Id == -1004); // Disabled
    }

    [Fact]
    public async Task GetRulesForArticleEvaluationAsync_WithFeedAndCategory_ReturnsAllApplicable()
    {
        // Act
        var result = await _repository.GetRulesForArticleEvaluationAsync(-2001, -3001);

        // Assert
        result.Should().Contain(r => r.Id == -1003);
    }

    [Fact]
    public async Task GetRulesForArticleEvaluationAsync_ReturnsRulesOrderedByPriority()
    {
        // Act
        var result = await _repository.GetRulesForArticleEvaluationAsync(-2001, null);

        // Assert
        result.Should().BeInAscendingOrder(r => r.Priority);
    }

    #endregion

    #region GetRulesWithAdvancedConditionsAsync

    [Fact]
    public async Task GetRulesWithAdvancedConditionsAsync_ReturnsOnlyEnabledAdvancedRules()
    {
        // Act
        var result = await _repository.GetRulesWithAdvancedConditionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.Should().AllSatisfy(r =>
        {
            r.UsesAdvancedConditions.Should().BeTrue();
            r.IsEnabled.Should().BeTrue();
        });
    }

    [Fact]
    public async Task GetRulesWithAdvancedConditionsAsync_LoadsConditions()
    {
        // Act
        var result = await _repository.GetRulesWithAdvancedConditionsAsync();
        var rule = result.First();

        // Assert
        rule.Conditions.Should().NotBeNull();
        rule.Conditions.Should().HaveCount(3);
    }

    #endregion

    #region GetRulesWithSimpleConditionsAsync

    [Fact]
    public async Task GetRulesWithSimpleConditionsAsync_ReturnsOnlyEnabledSimpleRules()
    {
        // Act
        var result = await _repository.GetRulesWithSimpleConditionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // -1001, -1002, -1003
        result.Should().AllSatisfy(r =>
        {
            r.UsesAdvancedConditions.Should().BeFalse();
            r.IsEnabled.Should().BeTrue();
        });
    }

    #endregion

    #region ToggleRuleStatusAsync

    [Fact]
    public async Task ToggleRuleStatusAsync_WithEnabledRule_DisablesIt()
    {
        // Arrange
        var rule = await _dbContext.Rules.FindAsync(-1001);
        rule!.IsEnabled.Should().BeTrue();

        // Act
        var result = await _repository.ToggleRuleStatusAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1001);

        // Assert
        result.Should().BeTrue();
        updated!.IsEnabled.Should().BeFalse();
        updated.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ToggleRuleStatusAsync_WithDisabledRule_EnablesIt()
    {
        // Arrange
        var rule = await _dbContext.Rules.FindAsync(-1004);
        rule!.IsEnabled.Should().BeFalse();

        // Act
        var result = await _repository.ToggleRuleStatusAsync(-1004);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1004);

        // Assert
        result.Should().BeTrue();
        updated!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleRuleStatusAsync_WithNonExistingRule_ReturnsFalse()
    {
        // Act
        var result = await _repository.ToggleRuleStatusAsync(-9999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ResetRuleStatisticsAsync

    [Fact]
    public async Task ResetRuleStatisticsAsync_WithExistingRule_ResetsCountAndLastMatchDate()
    {
        // Arrange
        var rule = await _dbContext.Rules.FindAsync(-1001);
        rule!.MatchCount.Should().BePositive();
        rule!.LastMatchDate.Should().NotBeNull();

        // Act
        var result = await _repository.ResetRuleStatisticsAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Rules.FindAsync(-1001);

        // Assert
        result.Should().BeTrue();
        updated!.MatchCount.Should().Be(0);
        updated.LastMatchDate.Should().BeNull();
        updated.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ResetRuleStatisticsAsync_WithNonExistingRule_ReturnsFalse()
    {
        // Act
        var result = await _repository.ResetRuleStatisticsAsync(-9999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetConditionsForRulesAsync

    [Fact]
    public async Task GetConditionsForRulesAsync_WithMultipleRuleIds_ReturnsGroupedConditions()
    {
        // Arrange
        var ruleIds = new[] { -1005, -1001 };

        // Act
        var result = await _repository.GetConditionsForRulesAsync(ruleIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey(-1005);
        result[-1005].Should().HaveCount(3);
        result.Should().NotContainKey(-1001);
    }

    [Fact]
    public async Task GetConditionsForRulesAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _repository.GetConditionsForRulesAsync(new List<int>());

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion   

    #region Performance Tests

    [Fact(Skip = "Performance test - run manually")]
    public async Task GetActiveRulesAsync_Performance_Under100ms()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.GetActiveRulesAsync();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task GetRulesForArticleEvaluationAsync_Performance_Under200ms()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.GetRulesForArticleEvaluationAsync(-2001, null);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    #endregion
}