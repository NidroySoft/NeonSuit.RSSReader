using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;
using System.Globalization;

namespace NeonSuit.RSSReader.Tests.Unit.Repository;

[CollectionDefinition("Database_UserPreferences")]
public class UserPreferencesData : ICollectionFixture<DatabaseFixture> { }

[Collection("Database_UserPreferences")]
public class UserPreferencesRepositoryTests : IAsyncLifetime
{
    private readonly RssReaderDbContext _dbContext;
    private readonly UserPreferencesRepository _repository;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DatabaseFixture _fixture;

    public UserPreferencesRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockLogger = new Mock<ILogger>();
        SetupMockLogger();

        _dbContext = fixture.Context;
        _repository = new UserPreferencesRepository(_dbContext, _mockLogger.Object);
    }

    private void SetupMockLogger()
    {
        _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
            .Returns(_mockLogger.Object);
        _mockLogger.Setup(x => x.ForContext<UserPreferencesRepository>())
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

        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM UserPreferences;");
        await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name='UserPreferences';");

        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");
    }

    private async Task SeedTestData()
    {
        var now = DateTime.UtcNow;
        var yesterday = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        var preferences = new List<UserPreferences>
        {
            new UserPreferences
            {
                Id = -1001,
                Key = PreferenceKeys.Theme,
                Value = "dark",
                LastModified = yesterday
            },
            new UserPreferences
            {
                Id = -1002,
                Key = PreferenceKeys.DefaultUpdateFrequency,
                Value = "30",
                LastModified = yesterday
            },
            new UserPreferences
            {
                Id = -1003,
                Key = PreferenceKeys.NotificationsEnabled,
                Value = "true",
                LastModified = twoDaysAgo
            },
            new UserPreferences
            {
                Id = -1004,
                Key = PreferenceKeys.ArticleFontSize,
                Value = "large",
                LastModified = twoDaysAgo
            },
            new UserPreferences
            {
                Id = -1005,
                Key = PreferenceKeys.AutoMarkAsRead,
                Value = "true",
                LastModified = yesterday
            },
            new UserPreferences
            {
                Id = -1006,
                Key = PreferenceKeys.LineHeight,
                Value = "1.5",
                LastModified = twoDaysAgo
            },
            new UserPreferences
            {
                Id = -1007,
                Key = PreferenceKeys.AccentColor,
                Value = "#FF4CAF50",
                LastModified = yesterday
            },
            new UserPreferences
            {
                Id = -1008,
                Key = PreferenceKeys.Language,
                Value = "es",
                LastModified = twoDaysAgo
            }
        };

        await _dbContext.UserPreferences.AddRangeAsync(preferences);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #region GetByKeyAsync

    [Fact]
    public async Task GetByKeyAsync_WithExistingKey_ReturnsPreference()
    {
        // Act
        var result = await _repository.GetByKeyAsync(PreferenceKeys.Theme);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(PreferenceKeys.Theme);
        result.Value.Should().Be("dark");
    }

    [Fact]
    public async Task GetByKeyAsync_WithNonExistingKey_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByKeyAsync("non.existing.key");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByKeyAsync_WithEmptyKey_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByKeyAsync("");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetValueAsync

    [Fact]
    public async Task GetValueAsync_WithExistingKey_ReturnsValue()
    {
        // Act
        var result = await _repository.GetValueAsync(PreferenceKeys.Theme);

        // Assert
        result.Should().Be("dark");
    }

    [Fact]
    public async Task GetValueAsync_WithNonExistingKeyAndDefault_CreatesAndReturnsDefault()
    {
        // Arrange
        var key = "test.key";
        var defaultValue = "test.value";

        // Act
        var result = await _repository.GetValueAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(defaultValue);
        created.Should().NotBeNull();
        created!.Key.Should().Be(key);
        created.Value.Should().Be(defaultValue);
        created.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetValueAsync_WithNonExistingKeyNoDefault_UsesSystemDefault()
    {
        // Arrange
        var key = PreferenceKeys.ReaderViewMode;
        var systemDefault = PreferenceHelper.GetDefaultValue(key);

        // Act
        var result = await _repository.GetValueAsync(key);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(systemDefault);
        created.Should().NotBeNull();
        created!.Value.Should().Be(systemDefault);
    }

    [Fact]
    public async Task GetValueAsync_WhenExceptionOccurs_ReturnsDefaultValue()
    {
        // Arrange
        var key = "error.key";
        var defaultValue = "fallback";

        // Simular error
        var repositoryWithError = new UserPreferencesRepository(_dbContext, _mockLogger.Object);

        // Act
        var result = await repositoryWithError.GetValueAsync(key, defaultValue);

        // Assert
        result.Should().Be(defaultValue);
    }

    #endregion

    #region SetValueAsync

    [Fact]
    public async Task SetValueAsync_WithNewKey_InsertsPreference()
    {
        // Arrange
        var key = "test.new.key";
        var value = "test.value";

        // Act
        await _repository.SetValueAsync(key, value);
        _dbContext.ChangeTracker.Clear();
        var inserted = await _repository.GetByKeyAsync(key);

        // Assert
        inserted.Should().NotBeNull();
        inserted!.Key.Should().Be(key);
        inserted.Value.Should().Be(value);
        inserted.LastModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetValueAsync_WithExistingKey_UpdatesValueAndTimestamp()
    {
        // Arrange
        var key = PreferenceKeys.Theme;
        var newValue = "light";
        var beforeUpdate = DateTime.UtcNow;

        // Act
        await _repository.SetValueAsync(key, newValue);
        _dbContext.ChangeTracker.Clear();
        var updated = await _repository.GetByKeyAsync(key);

        // Assert
        updated.Should().NotBeNull();
        updated!.Value.Should().Be(newValue);
        updated.LastModified.Should().BeOnOrAfter(beforeUpdate);
    }

    [Fact]
    public async Task SetValueAsync_WithInvalidValue_DoesNotUpdateAndLogsWarning()
    {
        // Arrange
        var key = PreferenceKeys.Theme;
        var invalidValue = "invalid.theme";
        var original = await _repository.GetValueAsync(key);

        // Act
        await _repository.SetValueAsync(key, invalidValue);
        _dbContext.ChangeTracker.Clear();
        var unchanged = await _repository.GetValueAsync(key);

        // Assert
        unchanged.Should().Be(original);
    }

    #endregion

    #region SetValuesAsync (Batch)

    [Fact]
    public async Task SetValuesAsync_WithMultipleValidValues_InsertsOrUpdatesAll()
    {
        // Arrange
        var batch = new Dictionary<string, string>
        {
            { PreferenceKeys.Theme, "system" },
            { "batch.test.key1", "value1" },
            { "batch.test.key2", "value2" }
        };

        // Act
        await _repository.SetValuesAsync(batch);
        _dbContext.ChangeTracker.Clear();

        var theme = await _repository.GetValueAsync(PreferenceKeys.Theme);
        var new1 = await _repository.GetValueAsync("batch.test.key1");
        var new2 = await _repository.GetValueAsync("batch.test.key2");

        // Assert
        theme.Should().Be("system");
        new1.Should().Be("value1");
        new2.Should().Be("value2");
    }

    [Fact]
    public async Task SetValuesAsync_WithInvalidValues_SkipsInvalidOnes()
    {
        // Arrange - Keys únicas, una con valor inválido
        var batch = new Dictionary<string, string>
    {
        { PreferenceKeys.Theme, "system" },           // ✅ Válido
        { PreferenceKeys.DefaultUpdateFrequency, "abc" }, // ❌ Inválido (no es número)
        { "test.valid", "value" }                    // ✅ Válido
    };

        // Act
        await _repository.SetValuesAsync(batch);
        _dbContext.ChangeTracker.Clear();

        var theme = await _repository.GetValueAsync(PreferenceKeys.Theme);
        var frequency = await _repository.GetValueAsync(PreferenceKeys.DefaultUpdateFrequency);
        var test = await _repository.GetValueAsync("test.valid");

        // Assert
        theme.Should().Be("system");
        frequency.Should().Be("30"); // Valor del seed, no se actualizó
        test.Should().Be("value");
    }

    #endregion

    #region GetBoolAsync

    [Fact]
    public async Task GetBoolAsync_WithExistingKey_ReturnsParsedBool()
    {
        // Act
        var result = await _repository.GetBoolAsync(PreferenceKeys.NotificationsEnabled);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetBoolAsync_WithNonExistingKey_ReturnsDefaultAndCreatesPreference()
    {
        // Arrange
        var key = "test.bool";
        var defaultValue = true;

        // Act
        var result = await _repository.GetBoolAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(defaultValue);
        created.Should().NotBeNull();
        created!.Value.Should().Be(defaultValue.ToString());
    }

    [Fact]
    public async Task GetBoolAsync_WithInvalidValue_ReturnsDefault()
    {
        // Arrange
        var key = PreferenceKeys.Theme; // "dark" no es bool

        // Act
        var result = await _repository.GetBoolAsync(key, true);

        // Assert
        result.Should().Be(true); // Devuelve defaultValue
    }

    #endregion

    #region GetIntAsync

    [Fact]
    public async Task GetIntAsync_WithExistingKey_ReturnsParsedInt()
    {
        // Act
        var result = await _repository.GetIntAsync(PreferenceKeys.DefaultUpdateFrequency);

        // Assert
        result.Should().Be(30);
    }

    [Fact]
    public async Task GetIntAsync_WithNonExistingKey_ReturnsDefaultAndCreatesPreference()
    {
        // Arrange
        var key = "test.int";
        var defaultValue = 42;

        // Act
        var result = await _repository.GetIntAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(defaultValue);
        created!.Value.Should().Be(defaultValue.ToString());
    }

    [Fact]
    public async Task GetIntAsync_WithInvalidValue_ReturnsDefault()
    {
        // Arrange
        var key = PreferenceKeys.Theme; // "dark" no es int

        // Act
        var result = await _repository.GetIntAsync(key, 99);

        // Assert
        result.Should().Be(99);
    }

    #endregion

    #region GetDoubleAsync

    [Fact]
    public async Task GetDoubleAsync_WithExistingKey_ReturnsParsedDouble()
    {
        // Act
        var result = await _repository.GetDoubleAsync(PreferenceKeys.LineHeight);

        // Assert
        result.Should().Be(1.5);
    }

    [Fact]
    public async Task GetDoubleAsync_WithNonExistingKey_ReturnsDefaultAndCreatesPreference()
    {
        // Arrange
        var key = "test.double";
        var defaultValue = 3.1416;

        // Act
        var result = await _repository.GetDoubleAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(defaultValue);
        created!.Value.Should().Be(defaultValue.ToString(CultureInfo.InvariantCulture));
    }

    #endregion

    #region GetDateTimeAsync

    [Fact]
    public async Task GetDateTimeAsync_WithNonExistingKey_ReturnsDefaultAndCreatesPreference()
    {
        // Arrange
        var key = "test.datetime";
        var defaultValue = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetDateTimeAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        // ✅ Convertir a UTC para comparación
        result.ToUniversalTime().Should().Be(defaultValue);

        // ✅ Verificar que el valor guardado está en formato ISO 8601 UTC
        created!.Value.Should().Be(defaultValue.ToString("O"));

        // ✅ Verificar que al parsear se mantiene UTC
        var parsed = DateTime.Parse(created.Value, null, DateTimeStyles.RoundtripKind);
        parsed.Kind.Should().Be(DateTimeKind.Utc);
        parsed.Should().Be(defaultValue);
    }
    #endregion

    #region GetTimeSpanAsync

    [Fact]
    public async Task GetTimeSpanAsync_WithNonExistingKey_ReturnsDefaultAndCreatesPreference()
    {
        // Arrange
        var key = "test.timespan";
        var defaultValue = TimeSpan.FromHours(2);

        // Act
        var result = await _repository.GetTimeSpanAsync(key, defaultValue);
        var created = await _repository.GetByKeyAsync(key);

        // Assert
        result.Should().Be(defaultValue);
        created!.Value.Should().Be(defaultValue.ToString());
    }

    #endregion

    #region SetBool/Int/Double/DateTime/TimeSpan

    [Fact]
    public async Task SetBoolAsync_StoresAsString()
    {
        // Arrange
        var key = "test.setbool";

        // Act
        await _repository.SetBoolAsync(key, true);
        var stored = await _repository.GetValueAsync(key);

        // Assert
        stored.Should().Be("True");
    }

    [Fact]
    public async Task SetIntAsync_StoresAsString()
    {
        // Arrange
        var key = "test.setint";

        // Act
        await _repository.SetIntAsync(key, 123);
        var stored = await _repository.GetValueAsync(key);

        // Assert
        stored.Should().Be("123");
    }

    [Fact]
    public async Task SetDoubleAsync_UsesInvariantCulture()
    {
        // Arrange
        var key = "test.setdouble";
        var value = 123.456;

        // Act
        await _repository.SetDoubleAsync(key, value);
        var stored = await _repository.GetValueAsync(key);

        // Assert
        stored.Should().Be(value.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task SetDateTimeAsync_StoresInRoundTripFormat()
    {
        // Arrange
        var key = "test.setdatetime";
        var value = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        await _repository.SetDateTimeAsync(key, value);
        var stored = await _repository.GetValueAsync(key);

        // ✅ Usar RoundtripKind para preservar UTC
        var retrieved = DateTime.Parse(stored, null, DateTimeStyles.RoundtripKind);
        var retrievedViaMethod = await _repository.GetDateTimeAsync(key, DateTime.MinValue);

        // Assert
        stored.Should().Be(value.ToString("O"));

        // ✅ Verificar con RoundtripKind
        retrieved.Kind.Should().Be(DateTimeKind.Utc);
        retrieved.Should().Be(value);

        // ✅ También verificar el método GetDateTimeAsync con conversión UTC
        retrievedViaMethod.ToUniversalTime().Should().Be(value);
    }

    [Fact]
    public async Task SetTimeSpanAsync_StoresAsString()
    {
        // Arrange
        var key = "test.settimespan";
        var value = TimeSpan.FromHours(1.5);

        // Act
        await _repository.SetTimeSpanAsync(key, value);
        var stored = await _repository.GetValueAsync(key);
        var retrieved = await _repository.GetTimeSpanAsync(key, TimeSpan.Zero);

        // Assert
        stored.Should().Be(value.ToString());
        retrieved.Should().Be(value);
    }

    #endregion

    #region ResetToDefaultAsync

    [Fact]
    public async Task ResetToDefaultAsync_WithExistingKey_ResetsToSystemDefault()
    {
        // Arrange
        var key = PreferenceKeys.Theme;
        var defaultValue = PreferenceHelper.GetDefaultValue(key);
        await _repository.SetValueAsync(key, "custom.value");

        // Act
        await _repository.ResetToDefaultAsync(key);
        _dbContext.ChangeTracker.Clear();
        var result = await _repository.GetValueAsync(key);

        // Assert
        result.Should().Be(defaultValue);
    }

    [Fact]
    public async Task ResetToDefaultAsync_WithNonExistingKey_CreatesWithDefault()
    {
        // Arrange
        var key = "test.reset.default";
        var defaultValue = PreferenceHelper.GetDefaultValue(key);

        // Act
        await _repository.ResetToDefaultAsync(key);
        var result = await _repository.GetValueAsync(key);

        // Assert
        result.Should().Be(defaultValue);
    }

    #endregion

    #region ResetToDefaultsAsync (Batch)

    [Fact]
    public async Task ResetToDefaultsAsync_WithMultipleKeys_ResetsAll()
    {
        // Arrange
        var keys = new[] { PreferenceKeys.Theme, PreferenceKeys.ArticleFontSize };
        await _repository.SetValueAsync(PreferenceKeys.Theme, "custom");
        await _repository.SetValueAsync(PreferenceKeys.ArticleFontSize, "custom");

        // Act
        await _repository.ResetToDefaultsAsync(keys);
        _dbContext.ChangeTracker.Clear();

        var theme = await _repository.GetValueAsync(PreferenceKeys.Theme);
        var fontSize = await _repository.GetValueAsync(PreferenceKeys.ArticleFontSize);

        // Assert
        theme.Should().Be(PreferenceHelper.GetDefaultValue(PreferenceKeys.Theme));
        fontSize.Should().Be(PreferenceHelper.GetDefaultValue(PreferenceKeys.ArticleFontSize));
    }

    #endregion

    #region GetAllCategorizedAsync

    [Fact]
    public async Task GetAllCategorizedAsync_ReturnsPreferencesGroupedByCategory()
    {
        // Act
        var result = await _repository.GetAllCategorizedAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey("Interfaz");
        result["Interfaz"].Should().Contain(p => p.Key == PreferenceKeys.Theme);
        result["Interfaz"].Should().Contain(p => p.Key == PreferenceKeys.AccentColor);

        result.Should().ContainKey("Feeds");
        result["Feeds"].Should().Contain(p => p.Key == PreferenceKeys.DefaultUpdateFrequency);

        result.Should().ContainKey("Notificaciones");
        result["Notificaciones"].Should().Contain(p => p.Key == PreferenceKeys.NotificationsEnabled);
    }

    [Fact]
    public async Task GetAllCategorizedAsync_WithNoPreferences_ReturnsEmptyCategories()
    {
        // Arrange
        await ClearTestData();

        // Act
        var result = await _repository.GetAllCategorizedAsync();

        // Assert
        result.Should().NotBeNull();
        result.Values.Should().AllSatisfy(list => list.Should().BeEmpty());
    }

    #endregion

    #region ExistsAsync

    [Fact]
    public async Task ExistsAsync_WithExistingKey_ReturnsTrue()
    {
        // Act
        var result = await _repository.ExistsAsync(PreferenceKeys.Theme);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistingKey_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync("non.existent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteByKeyAsync

    [Fact]
    public async Task DeleteByKeyAsync_WithExistingKey_RemovesPreferenceAndReturnsTrue()
    {
        // Act
        var result = await _repository.DeleteByKeyAsync(PreferenceKeys.Theme);
        _dbContext.ChangeTracker.Clear();
        var deleted = await _repository.GetByKeyAsync(PreferenceKeys.Theme);

        // Assert
        result.Should().BeTrue();
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteByKeyAsync_WithNonExistingKey_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteByKeyAsync("non.existent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetByKeyPatternAsync

    [Fact]
    public async Task GetByKeyPatternAsync_WithPattern_ReturnsMatchingPreferences()
    {
        // Act
        var result = await _repository.GetByKeyPatternAsync("%update%");

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(p => p.Key == PreferenceKeys.DefaultUpdateFrequency);
        result.Should().BeInAscendingOrder(p => p.Key);
    }

    [Fact]
    public async Task GetByKeyPatternAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetByKeyPatternAsync("%xyz123%");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetModifiedAfterAsync

    [Fact]
    public async Task GetModifiedAfterAsync_WithDate_ReturnsPreferencesModifiedAfter()
    {
        // Arrange
        var cutoff = DateTime.UtcNow.AddDays(-1.5);

        // Act
        var result = await _repository.GetModifiedAfterAsync(cutoff);

        // Assert
        result.Should().NotBeNull();
        result.Should().AllSatisfy(p => p.LastModified.Should().BeOnOrAfter(cutoff));
        result.Should().BeInDescendingOrder(p => p.LastModified);
    }

    [Fact]
    public async Task GetModifiedAfterAsync_WithFutureDate_ReturnsEmptyList()
    {
        // Arrange
        var cutoff = DateTime.UtcNow.AddDays(1);

        // Act
        var result = await _repository.GetModifiedAfterAsync(cutoff);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task SetValueAsync_WithNullValue_InsertsNullString()
    {
        // Arrange
        var key = "test.null.value";

        // Act
        await _repository.SetValueAsync(key, null!);
        var stored = await _repository.GetValueAsync(key);

        // Assert
        stored.Should().BeEmpty(); // Se convierte a string.Empty
    }

    [Fact]
    public async Task GetByKeyPatternAsync_WithEmptyPattern_ReturnsAll()
    {
        // Act
        var result = await _repository.GetByKeyPatternAsync("%");

        // Assert
        result.Should().HaveCount(8);
    }

    #endregion

    #region Performance Tests

    [Fact(Skip = "Performance test - run manually")]
    public async Task SetValuesAsync_Performance_With100Items_Under500ms()
    {
        // Arrange
        var batch = new Dictionary<string, string>();
        for (int i = 0; i < 100; i++)
        {
            batch[$"test.key.{i}"] = $"value.{i}";
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _repository.SetValuesAsync(batch);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task GetValueAsync_Performance_WithCachedVsNonCached_Under50ms()
    {
        // Arrange
        var key = PreferenceKeys.Theme;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Primera llamada (no cacheada)
        await _repository.GetValueAsync(key);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    #endregion
}