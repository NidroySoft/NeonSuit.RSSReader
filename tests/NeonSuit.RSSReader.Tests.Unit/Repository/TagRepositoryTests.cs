using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository;

[CollectionDefinition("Database_Tag")]
public class TagData : ICollectionFixture<DatabaseFixture> { }

[Collection("Database_Tag")]
public class TagRepositoryTests : IAsyncLifetime
{
    private readonly RssReaderDbContext _dbContext;
    private readonly TagRepository _repository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DatabaseFixture _fixture;

    public TagRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockLogger = new Mock<ILogger>();
        SetupMockLogger();

        _dbContext = fixture.Context;
        _repository = new TagRepository(_dbContext, _mockLogger.Object);
    }

    private void SetupMockLogger()
    {
        _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
            .Returns(_mockLogger.Object);
        _mockLogger.Setup(x => x.ForContext<TagRepository>())
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

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ArticleTags;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM Tags;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('Tags', 'ArticleTags');");

        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    private async Task SeedTestData()
    {
        // ✅ Tags con IDs NEGATIVOS
        var tags = new List<Tag>
        {
            new Tag
            {
                Id = -1001,
                Name = "Technology",
                Description = "Tech related articles",
                Color = "#3498db",
                Icon = "computer",
                IsPinned = true,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastUsedAt = DateTime.UtcNow.AddDays(-1),
                UsageCount = 42
            },
            new Tag
            {
                Id = -1002,
                Name = "Programming",
                Description = "Coding and development",
                Color = "#e74c3c",
                Icon = "code",
                IsPinned = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                LastUsedAt = DateTime.UtcNow.AddDays(-2),
                UsageCount = 38
            },
            new Tag
            {
                Id = -1003,
                Name = "Design",
                Description = "UI/UX and design",
                Color = "#2ecc71",
                Icon = "brush",
                IsPinned = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                LastUsedAt = DateTime.UtcNow.AddDays(-3),
                UsageCount = 15
            },
            new Tag
            {
                Id = -1004,
                Name = "HiddenTag",
                Description = "This tag is hidden",
                Color = "#95a5a6",
                Icon = "eye-off",
                IsPinned = false,
                IsVisible = false,
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                LastUsedAt = DateTime.UtcNow.AddDays(-10),
                UsageCount = 5
            },
            new Tag
            {
                Id = -1005,
                Name = "PinnedHidden",
                Description = "Pinned but hidden",
                Color = "#f1c40f",
                Icon = "pin",
                IsPinned = true,
                IsVisible = false,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                LastUsedAt = null,
                UsageCount = 0
            },
            new Tag
            {
                Id = -1006,
                Name = "NeverUsed",
                Description = "Never been used",
                Color = "#9b59b6",
                Icon = "tag",
                IsPinned = false,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                LastUsedAt = null,
                UsageCount = 0
            }
        };

        await _dbContext.Tags.AddRangeAsync(tags);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetByIdAsync (Base)

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsTag()
    {
        // Act
        var result = await _repository.GetByIdAsync(-1001);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(-1001);
        result.Name.Should().Be("Technology");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(-9999);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByNameAsync

    [Fact]
    public async Task GetByNameAsync_WithExistingName_ReturnsTag()
    {
        // Act
        var result = await _repository.GetByNameAsync("Technology");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(-1001);
        result.Name.Should().Be("Technology");
    }

    [Fact]
    public async Task GetByNameAsync_WithCaseInsensitive_ReturnsTag()
    {
        // Act
        var result = await _repository.GetByNameAsync("technology");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(-1001);
    }

    [Fact]
    public async Task GetByNameAsync_WithNonExistingName_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("NonExistent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync (Base)

    [Fact]
    public async Task GetAllAsync_ReturnsAllTags()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(6);
    }

    #endregion

    #region GetPopularTagsAsync

    [Fact]
    public async Task GetPopularTagsAsync_ReturnsVisibleTagsOrderedByUsageCount()
    {
        // Act
        var result = await _repository.GetPopularTagsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4); // Solo visibles: Tech(42), Prog(38), Design(15), NeverUsed(0)
        result.Should().BeInDescendingOrder(t => t.UsageCount);
        result.First().Id.Should().Be(-1001); // Technology (42)
        result.Last().Id.Should().Be(-1006);   // NeverUsed (0)
    }

    [Fact]
    public async Task GetPopularTagsAsync_WithLimit_ReturnsSpecifiedCount()
    {
        // Act
        var result = await _repository.GetPopularTagsAsync(2);

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be(-1001);
        result.Last().Id.Should().Be(-1002);
    }

    [Fact]
    public async Task GetPopularTagsAsync_ExcludesHiddenTags()
    {
        // Act
        var result = await _repository.GetPopularTagsAsync();

        // Assert
        result.Should().NotContain(t => t.Id == -1004); // HiddenTag
        result.Should().NotContain(t => t.Id == -1005); // PinnedHidden
    }

    #endregion

    #region GetPinnedTagsAsync

    [Fact]
    public async Task GetPinnedTagsAsync_ReturnsVisiblePinnedTagsOrderedByName()
    {
        // Act
        var result = await _repository.GetPinnedTagsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1); // Solo Technology (pinned y visible)
        result.Should().BeInAscendingOrder(t => t.Name);
        result.First().Id.Should().Be(-1001);
    }

    [Fact]
    public async Task GetPinnedTagsAsync_ExcludesHiddenPinnedTags()
    {
        // Act
        var result = await _repository.GetPinnedTagsAsync();

        // Assert
        result.Should().NotContain(t => t.Id == -1005); // PinnedHidden
    }

    #endregion

    #region GetVisibleTagsAsync

    [Fact]
    public async Task GetVisibleTagsAsync_ReturnsAllVisibleTagsOrderedByName()
    {
        // Act
        var result = await _repository.GetVisibleTagsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4); // Tech, Prog, Design, NeverUsed

        // ✅ CORREGIDO: Ordenar por nombre, no por ID
        result.Should().BeInAscendingOrder(t => t.Name);

        // ✅ Verificar nombres específicos
        result.Select(t => t.Name).Should().ContainInOrder(
            "Design",      // -1003
            "NeverUsed",   // -1006
            "Programming", // -1002
            "Technology"   // -1001
        );

        // ✅ Verificar IDs correspondientes
        result.Select(t => t.Id).Should().ContainInOrder(
            -1003,  // Design
            -1006,  // NeverUsed
            -1002,  // Programming
            -1001   // Technology
        );
    }

    [Fact]
    public async Task GetVisibleTagsAsync_ExcludesHiddenTags()
    {
        // Act
        var result = await _repository.GetVisibleTagsAsync();

        // Assert
        result.Should().NotContain(t => t.Id == -1004);
        result.Should().NotContain(t => t.Id == -1005);
    }

    #endregion

    #region SearchByNameAsync

    [Fact]
    public async Task SearchByNameAsync_WithExactMatch_ReturnsTag()
    {
        // Act
        var result = await _repository.SearchByNameAsync("Technology");

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(-1001);
    }

    [Fact]
    public async Task SearchByNameAsync_WithPartialMatch_ReturnsMatchingTags()
    {
        // Act
        var result = await _repository.SearchByNameAsync("Prog");

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(-1002);
    }

    [Fact]
    public async Task SearchByNameAsync_WithCaseInsensitive_ReturnsMatches()
    {
        // Act
        var result = await _repository.SearchByNameAsync("technology");

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(-1001);
    }

    [Fact]
    public async Task SearchByNameAsync_WithEmptyString_ReturnsAllTags()
    {
        // Act
        var result = await _repository.SearchByNameAsync("");

        // Assert
        result.Should().HaveCount(6);
    }

    [Fact]
    public async Task SearchByNameAsync_WithNull_ReturnsAllTags()
    {
        // Act
        var result = await _repository.SearchByNameAsync(null!);

        // Assert
        result.Should().HaveCount(6);
    }

    [Fact]
    public async Task SearchByNameAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.SearchByNameAsync("XYZ123");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateLastUsedAsync

    [Fact]
    public async Task UpdateLastUsedAsync_WithExistingTag_UpdatesTimestamp()
    {
        // Arrange
        var beforeUpdate = DateTime.UtcNow;
        var tag = await _dbContext.Tags.FindAsync(-1006);
        tag!.LastUsedAt.Should().BeNull();

        // Act
        await _repository.UpdateLastUsedAsync(-1006);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1006);

        // Assert
        updated!.LastUsedAt.Should().NotBeNull();

        // ✅ Usar BeCloseTo en lugar de BeOnOrAfter
        updated.LastUsedAt.Should().BeCloseTo(beforeUpdate, TimeSpan.FromSeconds(2));

        // ✅ O simplemente verificar que NO es null y es reciente
        updated.LastUsedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task UpdateLastUsedAsync_WithNonExistingTag_DoesNotThrow()
    {
        // Act
        Func<Task> act = async () => await _repository.UpdateLastUsedAsync(-9999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region IncrementUsageCountAsync

    [Fact]
    public async Task IncrementUsageCountAsync_WithExistingTag_IncrementsByOne()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1006);
        var initialCount = tag!.UsageCount;

        // Act
        await _repository.IncrementUsageCountAsync(-1006);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1006);

        // Assert
        updated!.UsageCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task IncrementUsageCountAsync_WithCustomIncrement_IncrementsBySpecifiedAmount()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1006);
        var initialCount = tag!.UsageCount;

        // Act
        await _repository.IncrementUsageCountAsync(-1006, 5);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1006);

        // Assert
        updated!.UsageCount.Should().Be(initialCount + 5);
    }

    [Fact]
    public async Task IncrementUsageCountAsync_WithNonExistingTag_DoesNotThrow()
    {
        // Act
        Func<Task> act = async () => await _repository.IncrementUsageCountAsync(-9999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region GetTagsByColorAsync

    [Fact]
    public async Task GetTagsByColorAsync_WithExistingColor_ReturnsTags()
    {
        // Act
        var result = await _repository.GetTagsByColorAsync("#3498db");

        // Assert
        result.Should().HaveCount(1);
        result.First().Id.Should().Be(-1001);
    }

    [Fact]
    public async Task GetTagsByColorAsync_WithNonExistingColor_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetTagsByColorAsync("#000000");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTagsByColorAsync_WithInvalidColor_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetTagsByColorAsync("invalid");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ExistsByNameAsync

    [Fact]
    public async Task ExistsByNameAsync_WithExistingName_ReturnsTrue()
    {
        // Act
        var result = await _repository.ExistsByNameAsync("Technology");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WithCaseInsensitive_ReturnsTrue()
    {
        // Act
        var result = await _repository.ExistsByNameAsync("technology");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByNameAsync_WithNonExistingName_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByNameAsync("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region InsertAsync

    [Fact]
    public async Task InsertAsync_WithValidTag_AddsToDatabaseAndSetsDefaults()
    {
        // Arrange
        var beforeInsert = DateTime.UtcNow;
        var newTag = new Tag
        {
            Id = -2001,
            Name = "New Tag",
            Description = "Test Description",
            Color = "#ff5733",
            Icon = "star",
            IsPinned = true,
            IsVisible = true
        };

        // Act
        var result = await _repository.InsertAsync(newTag);
        _dbContext.ChangeTracker.Clear();
        var inserted = await _dbContext.Tags.FindAsync(-2001);

        // Assert
        result.Should().Be(1);
        inserted.Should().NotBeNull();
        inserted!.Name.Should().Be("New Tag");
        inserted.CreatedAt.Should().BeOnOrAfter(beforeInsert);
        inserted.UsageCount.Should().Be(0);
        inserted.LastUsedAt.Should().BeNull();
    }

    [Fact]
    public async Task InsertAsync_WithInvalidColor_UsesDefaultColor()
    {
        // Arrange
        var newTag = new Tag
        {
            Id = -2002,
            Name = "Invalid Color Tag",
            Color = "not-a-color", // Inválido
            IsVisible = true
        };

        // Act
        var result = await _repository.InsertAsync(newTag);
        _dbContext.ChangeTracker.Clear();
        var inserted = await _dbContext.Tags.FindAsync(-2002);

        // Assert
        result.Should().Be(1);
        inserted!.Color.Should().Be("#3498db"); // Default color
    }

    [Fact]
    public async Task InsertAsync_WithNullColor_UsesDefaultColor()
    {
        // Arrange
        var newTag = new Tag
        {
            Id = -2003,
            Name = "Null Color Tag",
            Color = null!,
            IsVisible = true
        };

        // Act
        var result = await _repository.InsertAsync(newTag);
        _dbContext.ChangeTracker.Clear();
        var inserted = await _dbContext.Tags.FindAsync(-2003);

        // Assert
        result.Should().Be(1);
        inserted!.Color.Should().Be("#3498db");
    }

    [Fact]
    public async Task InsertAsync_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var duplicateTag = new Tag
        {
            Id = -2004,
            Name = "Technology", // Ya existe en el seed
            Color = "#ff5733",
            IsVisible = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.InsertAsync(duplicateTag));

        exception.Message.Should().Contain("Tag with name 'Technology' already exists");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WithExistingTag_UpdatesProperties()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1001);
        tag!.Name = "Updated Tech";
        tag!.Description = "Updated description";
        tag!.Color = "#ff0000";
        tag!.IsPinned = false;

        var beforeUpdate = DateTime.UtcNow;

        // Act
        var result = await _repository.UpdateAsync(tag);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        updated!.Name.Should().Be("Updated Tech");
        updated.Description.Should().Be("Updated description");
        updated.Color.Should().Be("#ff0000");
        updated.IsPinned.Should().BeFalse();
        // CreatedAt no debe cambiar
        updated.CreatedAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidColor_UsesDefaultColor()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1001);
        tag!.Color = "bad-color";

        // Act
        var result = await _repository.UpdateAsync(tag);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        updated!.Color.Should().Be("#3498db");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingTag_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var nonExisting = new Tag
        {
            Id = -9999,
            Name = "Non Existing",
            Color = "#ff5733"
        };

        // Act
        Func<Task> act = async () => await _repository.UpdateAsync(nonExisting);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    #endregion

    #region DeleteAsync (Base)

    #region DeleteAsync (Base)

    [Fact]
    public async Task DeleteAsync_WithExistingTag_RemovesTag()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1006);
        tag.Should().NotBeNull();

        // Act
        await _repository.DeleteAsync(tag!);
        _dbContext.ChangeTracker.Clear();
        var deleted = await _dbContext.Tags.FindAsync(-1006);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistingTag_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange
        var nonExisting = new Tag
        {
            Id = -9999,
            Name = "Non Existing",
            Color = "#ff5733"
        };

        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(nonExisting);

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task DeleteAsync_WithNullTag_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await _repository.DeleteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
    #endregion

    #region BatchUpdateUsageCountAsync

    [Fact]
    public async Task BatchUpdateUsageCountAsync_WithValidDictionary_UpdatesMultipleTags()
    {
        // Arrange
        var increments = new Dictionary<int, int>
        {
            { -1001, 5 },
            { -1002, 3 },
            { -1003, 2 }
        };

        // Act
        var result = await _repository.BatchUpdateUsageCountAsync(increments);
        _dbContext.ChangeTracker.Clear();
        var tag1 = await _dbContext.Tags.FindAsync(-1001);
        var tag2 = await _dbContext.Tags.FindAsync(-1002);
        var tag3 = await _dbContext.Tags.FindAsync(-1003);

        // Assert
        result.Should().Be(3);
        tag1!.UsageCount.Should().Be(47); // 42 + 5
        tag2!.UsageCount.Should().Be(41); // 38 + 3
        tag3!.UsageCount.Should().Be(17); // 15 + 2
    }

    [Fact]
    public async Task BatchUpdateUsageCountAsync_WithEmptyDictionary_ReturnsZero()
    {
        // Act
        var result = await _repository.BatchUpdateUsageCountAsync(new Dictionary<int, int>());

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task BatchUpdateUsageCountAsync_WithNonExistingIds_SkipsThem()
    {
        // Arrange
        var increments = new Dictionary<int, int>
        {
            { -9999, 5 },
            { -1001, 1 }
        };

        // Act
        var result = await _repository.BatchUpdateUsageCountAsync(increments);
        _dbContext.ChangeTracker.Clear();
        var tag1 = await _dbContext.Tags.FindAsync(-1001);

        // Assert
        result.Should().Be(1);
        tag1!.UsageCount.Should().Be(43);
    }

    #endregion

    #region GetTagsWithMinUsageAsync

    [Fact]
    public async Task GetTagsWithMinUsageAsync_ReturnsTagsAboveThreshold()
    {
        // Act
        var result = await _repository.GetTagsWithMinUsageAsync(20);

        // Assert
        result.Should().HaveCount(2); // Tech(42), Prog(38)
        result.Should().BeInDescendingOrder(t => t.UsageCount);
    }

    [Fact]
    public async Task GetTagsWithMinUsageAsync_WithZeroThreshold_ReturnsAllTags()
    {
        // Act
        var result = await _repository.GetTagsWithMinUsageAsync(0);

        // Assert
        result.Should().HaveCount(6);
    }

    [Fact]
    public async Task GetTagsWithMinUsageAsync_WithHighThreshold_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetTagsWithMinUsageAsync(100);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetTagsByDateRangeAsync

    [Fact]
    public async Task GetTagsByDateRangeAsync_ReturnsTagsInRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow.AddDays(-15);

        // Act
        var result = await _repository.GetTagsByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().HaveCount(3); // Tech(-30), Prog(-25), Design(-20)
        result.Should().BeInDescendingOrder(t => t.CreatedAt);
    }

    [Fact]
    public async Task GetTagsByDateRangeAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-100);
        var endDate = DateTime.UtcNow.AddDays(-90);

        // Act
        var result = await _repository.GetTagsByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetRecentlyUsedTagsAsync

    [Fact]
    public async Task GetRecentlyUsedTagsAsync_ReturnsTagsUsedInLastNDays()
    {
        // Act
        var result = await _repository.GetRecentlyUsedTagsAsync(5);

        // Assert
        result.Should().HaveCount(3); // Tech, Prog, Design (todos usados hace 1-3 días)
        result.Should().BeInDescendingOrder(t => t.LastUsedAt);
    }

    [Fact]
    public async Task GetRecentlyUsedTagsAsync_WithNoRecentUsage_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetRecentlyUsedTagsAsync(1);

        // Assert
        result.Should().BeEmpty(); // Ninguno usado en el último día
    }

    [Fact]
    public async Task GetRecentlyUsedTagsAsync_ExcludesNeverUsedTags()
    {
        // Act
        var result = await _repository.GetRecentlyUsedTagsAsync(30);

        // Assert
        result.Should().NotContain(t => t.Id == -1006); // NeverUsed
    }

    #endregion

    #region TogglePinStatusAsync

    [Fact]
    public async Task TogglePinStatusAsync_WithExistingTag_TogglesPinStatus()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1001);
        var initialStatus = tag!.IsPinned;

        // Act
        var result = await _repository.TogglePinStatusAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1001);

        // Assert
        result.Should().BeTrue();
        updated!.IsPinned.Should().Be(!initialStatus);
    }

    [Fact]
    public async Task TogglePinStatusAsync_WithNonExistingTag_ReturnsFalse()
    {
        // Act
        var result = await _repository.TogglePinStatusAsync(-9999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ToggleVisibilityAsync

    [Fact]
    public async Task ToggleVisibilityAsync_WithExistingTag_TogglesVisibility()
    {
        // Arrange
        var tag = await _dbContext.Tags.FindAsync(-1001);
        var initialStatus = tag!.IsVisible;

        // Act
        var result = await _repository.ToggleVisibilityAsync(-1001);
        _dbContext.ChangeTracker.Clear();
        var updated = await _dbContext.Tags.FindAsync(-1001);

        // Assert
        result.Should().BeTrue();
        updated!.IsVisible.Should().Be(!initialStatus);
    }

    [Fact]
    public async Task ToggleVisibilityAsync_WithNonExistingTag_ReturnsFalse()
    {
        // Act
        var result = await _repository.ToggleVisibilityAsync(-9999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetTagsByArticleIdAsync

    [Fact]
    public async Task GetTagsByArticleIdAsync_WithExistingArticle_ReturnsTags()
    {
        // Arrange - PRIMERO crear un artículo que SÍ exista
        var article = new Article
        {
            Id = -5001,
            FeedId = 1,
            Title = "Test Article for Tags",
            Content = "Test content",
            Guid = Guid.NewGuid().ToString(),
            ContentHash = Guid.NewGuid().ToString(),
            PublishedDate = DateTime.UtcNow,
            Status = ArticleStatus.Unread
        };

        await _dbContext.Articles.AddAsync(article);
        await _dbContext.SaveChangesAsync();

        // Ahora sí, crear las relaciones ArticleTag
        var articleTags = new List<ArticleTag>
    {
        new ArticleTag { ArticleId = -5001, TagId = -1001 },
        new ArticleTag { ArticleId = -5001, TagId = -1002 }
    };

        await _dbContext.ArticleTags.AddRangeAsync(articleTags);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var result = await _repository.GetTagsByArticleIdAsync(-5001);

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeInAscendingOrder(t => t.Name);
        result.Select(t => t.Id).Should().Contain(new[] { -1001, -1002 });
    }

    [Fact]
    public async Task GetTagsByArticleIdAsync_WithNoTags_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetTagsByArticleIdAsync(-9999);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task InsertAsync_WithNameExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var newTag = new Tag
        {
            Id = -3001,
            Name = new string('A', 51), // 51 caracteres - excede 50
            Color = "#ff5733",
            IsVisible = true
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.InsertAsync(newTag));

        exception.Message.Should().Contain("cannot exceed 50 characters");
        exception.ParamName.Should().Be("Name");
    }

    [Fact]
    public async Task GetTagsByDateRangeAsync_WithReversedDates_ReturnsEmptyList()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-30);

        // Act
        var result = await _repository.GetTagsByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Performance Tests

    [Fact(Skip = "Performance test - run manually")]
    public async Task GetPopularTagsAsync_Performance_Under100ms()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.GetPopularTagsAsync();

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task BatchUpdateUsageCountAsync_Performance_Under500ms()
    {
        // Arrange
        var increments = new Dictionary<int, int>();
        for (int i = 1; i <= 100; i++)
        {
            increments.Add(-i, 1);
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _repository.BatchUpdateUsageCountAsync(increments);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    #endregion
}