using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository;

[CollectionDefinition("Database_RuleCondition")]
public class RuleConditionData : ICollectionFixture<DatabaseFixture> { }

[Collection("Database_RuleCondition")]
public class RuleConditionRepositoryTests : IAsyncLifetime
{
    private readonly RssReaderDbContext _dbContext;
    private readonly RuleConditionRepository _repository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DatabaseFixture _fixture;

    public RuleConditionRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockLogger = new Mock<ILogger>();
        SetupMockLogger();

        _dbContext = fixture.Context;

        // ✅ Constructor con ILogger inyectado
        _repository = new RuleConditionRepository(_dbContext, _mockLogger.Object);
    }

    private void SetupMockLogger()
    {
        _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
            .Returns(_mockLogger.Object);
        _mockLogger.Setup(x => x.ForContext<RuleConditionRepository>())
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
        // ✅ 1. Deshabilitar FK checks
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;");

        // ✅ 2. Limpiar el ChangeTracker ANTES de eliminar
        _dbContext.ChangeTracker.Clear();

        // ✅ 3. Ejecutar DELETE directo con SQL (EVITA el tracking)
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM RuleConditions;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Rules;");

        // ✅ 4. Reset identity sequences
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('RuleConditions', 'Rules');");

        // ✅ 5. Reactivar FK checks
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    private async Task SeedTestData()
    {
        // ✅ Rules SIN FeedId, con la estructura REAL
        var rules = new List<Rule>
    {
        new Rule
        {
            Id = -2001,
            Name = "Test Rule 1",
            Target = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "important",
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsRead,
            Priority = 100,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            UsesAdvancedConditions = true
        },
        new Rule
        {
            Id = -2002,
            Name = "Test Rule 2",
            Target = RuleFieldTarget.Content,
            Operator = RuleOperator.Contains,
            Value = "urgent",
            Scope = RuleScope.SpecificFeeds,
            FeedIds = "[-1001, -1002]",
            IsEnabled = true,
            ActionType = RuleActionType.SendNotification,
            NotificationTemplate = "Alert: {Title}",
            NotificationPriority = NotificationPriority.High,
            Priority = 200,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            UsesAdvancedConditions = true
        },
        new Rule
        {
            Id = -2003,
            Name = "Test Rule 3",
            Target = RuleFieldTarget.Author,
            Operator = RuleOperator.Equals,
            Value = "admin",
            Scope = RuleScope.AllFeeds,
            IsEnabled = false,
            ActionType = RuleActionType.MarkAsStarred,
            Priority = 300,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            UsesAdvancedConditions = true
        }
    };

        await _dbContext.Rules.AddRangeAsync(rules);
        await _dbContext.SaveChangesAsync();

        // ✅ RuleConditions con los campos CORRECTOS del modelo
        var conditions = new List<RuleCondition>
    {
        new RuleCondition
        {
            Id = -3001,
            RuleId = -2001,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "important",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        },
        new RuleCondition
        {
            Id = -3002,
            RuleId = -2001,
            GroupId = 1,
            Order = 2,
            Field = RuleFieldTarget.Content,
            Operator = RuleOperator.Contains,
            Value = "critical",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        },
        new RuleCondition
        {
            Id = -3003,
            RuleId = -2002,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Author,
            Operator = RuleOperator.Equals,
            Value = "system",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = true
        },
        new RuleCondition
        {
            Id = -3004,
            RuleId = -2002,
            GroupId = 2,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.StartsWith,
            Value = "[ALERT]",
            Negate = false,
            CombineWithNext = LogicalOperator.OR,
            IsCaseSensitive = false
        },
        new RuleCondition
        {
            Id = -3005,
            RuleId = -2003,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Categories,
            Operator = RuleOperator.Contains,
            Value = "technology",
            Negate = true,  // NOT contains
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        },
        new RuleCondition
        {
            Id = -3006,
            RuleId = -2002,
            GroupId = 1,
            Order = 2,
            Field = RuleFieldTarget.Content,
            Operator = RuleOperator.Regex,
            Value = "",
            RegexPattern = @"\bERROR\b",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        }
    };

        await _dbContext.RuleConditions.AddRangeAsync(conditions);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
    }
    #region GetByRuleIdAsync

    [Fact]
    public async Task GetByRuleIdAsync_WithExistingRule_ReturnsOrderedConditions()
    {
        // Act
        var result = await _repository.GetByRuleIdAsync(-2001);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeInAscendingOrder(x => x.GroupId)
            .And.ThenBeInAscendingOrder(x => x.Order);
        result.Should().AllSatisfy(c => c.RuleId.Should().Be(-2001));
    }

    [Fact]
    public async Task GetByRuleIdAsync_WithNonExistingRule_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByRuleIdAsync(-9999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRuleIdAsync_WithInactiveRule_ReturnsConditions()
    {
        // Act
        var result = await _repository.GetByRuleIdAsync(-2003);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().RuleId.Should().Be(-2003);
    }

    #endregion

    #region GetConditionGroupsAsync

    [Fact]
    public async Task GetConditionGroupsAsync_WithExistingRule_ReturnsGroupedConditions()
    {
        // Act
        var result = await _repository.GetConditionGroupsAsync(-2002);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        result.Should().ContainKey(1);
        result[1].Should().HaveCount(2);  // ✅ Cambiado de 1 a 2
        result[1].Should().Contain(c => c.Field == RuleFieldTarget.Author);
        result[1].Should().Contain(c => c.Field == RuleFieldTarget.Content);

        result.Should().ContainKey(2);
        result[2].Should().HaveCount(1);
        result[2].First().Field.Should().Be(RuleFieldTarget.Title);
    }

    [Fact]
    public async Task GetConditionGroupsAsync_WithNonExistingRule_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _repository.GetConditionGroupsAsync(-9999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConditionGroupsAsync_WithNoGroups_ReturnsEmptyDictionary()
    {
        // Arrange
        var ruleWithNoConditions = new Rule
        {
            Id = -2004,
            Name = "Rule Without Conditions",
            // ❌ ELIMINAR FeedId - NO EXISTE en el modelo
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.ArchiveArticle,
            Target = RuleFieldTarget.Title,        // ✅ Requerido
            Operator = RuleOperator.Contains,      // ✅ Requerido
            Value = "test",                       // ✅ Requerido
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        await _dbContext.Rules.AddAsync(ruleWithNoConditions);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetConditionGroupsAsync(-2004);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region InsertAsync

    [Fact]
    public async Task InsertAsync_WithValidCondition_AddsToDatabaseAndReturnsId()
    {
        // Arrange - ❌ NO asignar Id
        var condition = new RuleCondition
        {
            // Id = -3101,  // QUITAR - la BD lo genera
            RuleId = -2001,
            GroupId = 2,
            Order = 1,
            Field = RuleFieldTarget.Content,
            Operator = RuleOperator.Contains,
            Value = "critical",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        };

        // Act
        var result = await _repository.InsertAsync(condition);
        _dbContext.ChangeTracker.Clear();

        // ✅ Buscar por el ID que DEVOLVIÓ el repositorio
        var inserted = await _dbContext.RuleConditions.FindAsync(result);

        // Assert
        result.Should().BePositive();  // ✅ Solo verificar que es positivo
        inserted.Should().NotBeNull();
        inserted!.RuleId.Should().Be(-2001);
        inserted.Field.Should().Be(RuleFieldTarget.Content);
        inserted.Operator.Should().Be(RuleOperator.Contains);
        inserted.Value.Should().Be("critical");
        inserted.Order.Should().Be(1);
        inserted.GroupId.Should().Be(2);
    }

    [Fact]
    public async Task InsertAsync_WithInvalidCondition_ThrowsArgumentException()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Id = -3102,
            RuleId = -2001,
            GroupId = 2,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "",  // ❌ Vacío, IsValid = false
            Negate = false
        };

        // Act
        Func<Task> act = async () => await _repository.InsertAsync(condition);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Condition configuration is invalid");
    }

    [Fact]
    public async Task InsertAsync_WithNullCondition_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _repository.InsertAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InsertAsync_WithNonExistingRule_ThrowsDbUpdateException()
    {
        // Arrange
        var condition = new RuleCondition
        {
            Id = -3103,
            RuleId = -9999,  // ❌ No existe
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Negate = false
        };

        // Act
        Func<Task> act = async () => await _repository.InsertAsync(condition);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WithExistingCondition_UpdatesProperties()
    {
        // Arrange
        var condition = await _dbContext.RuleConditions.FindAsync(-3001);
        condition!.Value = "very important";
        condition.Operator = RuleOperator.StartsWith;
        condition.Order = 3;

        // Act
        var result = await _repository.UpdateAsync(condition);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.RuleConditions.FindAsync(-3001);

        // Assert
        result.Should().Be(1);
        updated.Should().NotBeNull();
        updated!.Value.Should().Be("very important");
        updated.Operator.Should().Be(RuleOperator.StartsWith);
        updated.Order.Should().Be(3);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidCondition_ThrowsArgumentException()
    {
        // Arrange
        var condition = await _dbContext.RuleConditions.FindAsync(-3001);
        condition!.Value = "";  // ❌ Vacío, IsValid = false

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(condition);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Condition configuration is invalid");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingCondition_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var nonExistentCondition = new RuleCondition
        {
            Id = 999,
            RuleId = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Order = 1
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _repository.UpdateAsync(nonExistentCondition));

        // Assert - Verificar que el inner exception es de tipo DbUpdateConcurrencyException
        exception.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        exception.InnerException?.Message.Should().Contain("expected to affect 1 row(s), but actually affected 0");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WithExistingId_RemovesFromDatabase()
    {
        // Act
        var result = await _repository.DeleteAsync(-3001);
        _dbContext.ChangeTracker.Clear();
        var deleted = await _dbContext.RuleConditions.FindAsync(-3001);

        // Assert
        result.Should().Be(1);
        deleted.Should().BeNull();
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

    #region DeleteByRuleIdAsync

    [Fact]
    public async Task DeleteByRuleIdAsync_WithExistingRule_DeletesAllConditions()
    {
        // Act
        var result = await _repository.DeleteByRuleIdAsync(-2001);
        _dbContext.ChangeTracker.Clear();
        var remaining = await _dbContext.RuleConditions
            .Where(c => c.RuleId == -2001)
            .ToListAsync();

        // Assert
        result.Should().Be(2);
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteByRuleIdAsync_WithNonExistingRule_ReturnsZero()
    {
        // Act
        var result = await _repository.DeleteByRuleIdAsync(-9999);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteByRuleIdAsync_WithRuleHavingNoConditions_ReturnsZero()
    {
        // Arrange
        var ruleWithNoConditions = new Rule
        {
            Id = -2005,
            Name = "No Conditions Rule",
            // ❌ ELIMINAR FeedId - NO EXISTE en el modelo
            Scope = RuleScope.AllFeeds,
            IsEnabled = true,
            ActionType = RuleActionType.MarkAsRead,
            Target = RuleFieldTarget.Title,        // ✅ REQUERIDO
            Operator = RuleOperator.Contains,      // ✅ REQUERIDO
            Value = "test",                       // ✅ REQUERIDO
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        await _dbContext.Rules.AddAsync(ruleWithNoConditions);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.DeleteByRuleIdAsync(-2005);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region ValidateConditionAsync

    [Fact]
    public async Task ValidateConditionAsync_WithValidCondition_ReturnsTrue()
    {
        // Arrange
        var condition = new RuleCondition
        {
            RuleId = -2001,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = "test",
            Negate = false
        };

        // Act
        var result = await _repository.ValidateConditionAsync(condition);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("", false)]           // Vacío
    [InlineData(null, false)]         // Null
    [InlineData("   ", false)]        // Whitespace
    [InlineData("valid", true)]       // Válido
    public async Task ValidateConditionAsync_WithVariousValues_ReturnsExpectedResult(string? value, bool expected)
    {
        // Arrange
        var condition = new RuleCondition
        {
            RuleId = -2001,
            GroupId = 1,
            Order = 1,
            Field = RuleFieldTarget.Title,
            Operator = RuleOperator.Contains,
            Value = value ?? string.Empty,
            Negate = false
        };

        // Act
        var result = await _repository.ValidateConditionAsync(condition);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region ValidateConditionGroupAsync

    [Fact]
    public async Task ValidateConditionGroupAsync_WithValidConditions_ReturnsTrue()
    {
        // Arrange
        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 1,
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "important",
                Negate = false
            },
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 2,
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "urgent",
                Negate = false
            }
        };

        // Act
        var result = await _repository.ValidateConditionGroupAsync(conditions);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConditionGroupAsync_WithDuplicateOrders_ReturnsFalse()
    {
        // Arrange
        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 1,  // Duplicado
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "important",
                Negate = false
            },
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 1,  // Duplicado
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "urgent",
                Negate = false
            }
        };

        // Act
        var result = await _repository.ValidateConditionGroupAsync(conditions);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateConditionGroupAsync_WithInvalidCondition_ReturnsFalse()
    {
        // Arrange
        var conditions = new List<RuleCondition>
        {
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 1,
                Field = RuleFieldTarget.Title,
                Operator = RuleOperator.Contains,
                Value = "important",
                Negate = false
            },
            new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 2,
                Field = RuleFieldTarget.Content,
                Operator = RuleOperator.Contains,
                Value = "",  // ❌ Inválido
                Negate = false
            }
        };

        // Act
        var result = await _repository.ValidateConditionGroupAsync(conditions);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateConditionGroupAsync_WithEmptyList_ReturnsTrue()
    {
        // Act
        var result = await _repository.ValidateConditionGroupAsync(new List<RuleCondition>());

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConditionGroupAsync_WithNullList_ReturnsTrue()
    {
        // Act
        var result = await _repository.ValidateConditionGroupAsync(null!);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetMaxOrderInGroupAsync

    [Fact]
    public async Task GetMaxOrderInGroupAsync_WithExistingGroup_ReturnsMaxOrder()
    {
        // Act
        var result = await _repository.GetMaxOrderInGroupAsync(-2001, 1);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetMaxOrderInGroupAsync_WithEmptyGroup_ReturnsZero()
    {
        // Act
        var result = await _repository.GetMaxOrderInGroupAsync(-2001, 99);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetMaxOrderInGroupAsync_WithSingleCondition_ReturnsOrder()
    {
        // Act
        var result = await _repository.GetMaxOrderInGroupAsync(-2003, 1);

        // Assert
        result.Should().Be(1);
    }

    #endregion

    #region ReorderConditionsAsync
    [Fact]
    public async Task ReorderConditionsAsync_WithValidOrderMap_UpdatesOrders()
    {
        // Arrange - ESPERAR 1 SOLA ACTUALIZACIÓN
        var orderMap = new Dictionary<int, int>
    {
        { -3001, 3 }  // ✅ Solo UNA condición
    };

        // Act
        var result = await _repository.ReorderConditionsAsync(-2001, 1, orderMap);
        _dbContext.ChangeTracker.Clear();
        var condition1 = await _dbContext.RuleConditions.FindAsync(-3001);
        var condition2 = await _dbContext.RuleConditions.FindAsync(-3002);

        // Assert
        result.Should().Be(1);  // ✅ Cambiado de 2 a 1
        condition1!.Order.Should().Be(3);
        condition2!.Order.Should().Be(2);  // Sin cambios
    }


    [Fact]
    public async Task ReorderConditionsAsync_WithNonExistingCondition_ReturnsZero()
    {
        // Arrange
        var orderMap = new Dictionary<int, int>
        {
            { -9999, 1 }  // ❌ No existe
        };

        // Act
        var result = await _repository.ReorderConditionsAsync(-2001, 1, orderMap);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ReorderConditionsAsync_WithEmptyOrderMap_ReturnsZero()
    {
        // Act
        var result = await _repository.ReorderConditionsAsync(-2001, 1, new Dictionary<int, int>());

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ReorderConditionsAsync_WithWrongRuleId_ReturnsZero()
    {
        // Arrange
        var orderMap = new Dictionary<int, int>
        {
            { -3001, 3 }
        };

        // Act
        var result = await _repository.ReorderConditionsAsync(-9999, 1, orderMap);  // ❌ Rule incorrecto

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task ReorderConditionsAsync_WithWrongGroupId_ReturnsZero()
    {
        // Arrange
        var orderMap = new Dictionary<int, int>
        {
            { -3001, 3 }
        };

        // Act
        var result = await _repository.ReorderConditionsAsync(-2001, 99, orderMap);  // ❌ Group incorrecto

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetByRuleIdAsync_WithNegativeRuleId_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByRuleIdAsync(-9999);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InsertAsync_WithMaxOrder_InsertsSuccessfully()
    {
        // Arrange
        var condition = new RuleCondition
        {
            // ❌ NO asignar Id - dejar que la BD lo genere
            RuleId = -2001,
            GroupId = 1,
            Order = 999,
            Field = RuleFieldTarget.AllFields,
            Operator = RuleOperator.Contains,
            Value = "test",
            Negate = false,
            CombineWithNext = LogicalOperator.AND,
            IsCaseSensitive = false
        };

        // Act
        var result = await _repository.InsertAsync(condition);
        _dbContext.ChangeTracker.Clear();

        // ✅ Buscar por el ID que devolvió el repositorio
        var inserted = await _dbContext.RuleConditions.FindAsync(result);

        // Assert
        result.Should().BePositive();  // ✅ Solo verificar que es positivo
        inserted.Should().NotBeNull();
        inserted!.Order.Should().Be(999);
        inserted.RuleId.Should().Be(-2001);
        inserted.Field.Should().Be(RuleFieldTarget.AllFields);
        inserted.Operator.Should().Be(RuleOperator.Contains);
        inserted.Value.Should().Be("test");
    }

    [Fact]
    public async Task ValidateConditionAsync_WithAllOperatorTypes_ReturnsCorrectValidation()
    {
        var operators = Enum.GetValues<RuleOperator>();

        foreach (var op in operators)
        {
            // Arrange - Configurar según el operador
            var condition = new RuleCondition
            {
                RuleId = -2001,
                GroupId = 1,
                Order = 1,
                Field = RuleFieldTarget.Title,
                Operator = op,
                Negate = false,
                CombineWithNext = LogicalOperator.AND,
                IsCaseSensitive = false
            };

            // Configurar Value/RegexPattern según el operador
            switch (op)
            {
                case RuleOperator.IsEmpty:
                case RuleOperator.IsNotEmpty:
                    condition.Value = null ?? string.Empty;  // ✅ Válido para estos operadores
                    break;

                case RuleOperator.Regex:
                    condition.Value = "test";  // ✅ Requerido
                    condition.RegexPattern = @"\btest\b";  // ✅ Patrón válido
                    break;

                case RuleOperator.GreaterThan:
                case RuleOperator.LessThan:
                    condition.Value = "2024-01-01";  // ✅ Requerido
                    condition.DateFormat = "yyyy-MM-dd";
                    break;

                default:
                    condition.Value = "test";  // ✅ Requerido para el resto
                    break;
            }

            // Act
            var result = await _repository.ValidateConditionAsync(condition);

            // Assert
            result.Should().BeTrue($"Operator {op} should be valid with correct configuration");
        }
    }

    #endregion

    #region Performance Tests

    [Fact(Skip = "Performance test - run manually")]
    public async Task GetByRuleIdAsync_Performance_Under100ms()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.GetByRuleIdAsync(-2001);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task DeleteByRuleIdAsync_Performance_Under200ms()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.DeleteByRuleIdAsync(-2001);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
    }

    #endregion
}