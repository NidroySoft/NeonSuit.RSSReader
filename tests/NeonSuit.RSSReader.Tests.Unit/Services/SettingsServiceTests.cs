using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;
using System.Collections.Concurrent;
using System.Text.Json;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    public class SettingsServiceTests
    {
        private readonly Mock<IUserPreferencesRepository> _mockRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly SettingsService _service;

        public SettingsServiceTests()
        {
            _mockRepository = new Mock<IUserPreferencesRepository>();
            _mockLogger = new Mock<ILogger>();

            // CRITICAL: Configure logger context
            _mockLogger.Setup(x => x.ForContext<SettingsService>())
                      .Returns(_mockLogger.Object);

            // Service with injected logger
            _service = new SettingsService(_mockRepository.Object, _mockLogger.Object);
        }

        // Test data factories
        private UserPreferences CreateTestPreference(string key = "testKey", string value = "testValue")
        {
            return new UserPreferences
            {
                Key = key,
                Value = value,
                LastModified = DateTime.UtcNow
            };
        }

        private List<UserPreferences> CreateTestPreferencesList(int count = 3)
        {
            var list = new List<UserPreferences>();
            for (int i = 0; i < count; i++)
            {
                list.Add(CreateTestPreference($"key{i}", $"value{i}"));
            }
            return list;
        }

        [Fact]
        public async Task GetValueAsync_WithCachedValue_ShouldReturnFromCache()
        {
            // Arrange
            var expectedKey = "cachedKey";
            var expectedValue = "cachedValue";
            var testPref = CreateTestPreference(expectedKey, expectedValue);

            // Initialize cache via reflection
            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.TryAdd(expectedKey, testPref);

            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            var result = await _service.GetValueAsync(expectedKey, "default");

            // Assert
            result.Should().Be(expectedValue);
            _mockRepository.Verify(x => x.GetValueAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetValueAsync_WithNonCachedValue_ShouldQueryRepository()
        {
            // Arrange
            var expectedKey = "nonCachedKey";
            var expectedValue = "repoValue";

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>());
            _mockRepository.Setup(x => x.GetValueAsync(expectedKey, It.IsAny<string>()))
                          .ReturnsAsync(expectedValue);
            _mockRepository.Setup(x => x.GetByKeyAsync(expectedKey))
                          .ReturnsAsync(CreateTestPreference(expectedKey, expectedValue));

            // Act
            var result = await _service.GetValueAsync(expectedKey, "default");

            // Assert
            result.Should().Be(expectedValue);
            _mockRepository.Verify(x => x.GetValueAsync(expectedKey, It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("invalid", false)] // Should return default
        [InlineData(null, false)] // Should handle null gracefully
        public async Task GetBoolAsync_WithVariousInputs_ShouldParseCorrectly(string? inputValue, bool expected)
        {
            // Arrange
            var key = "boolKey";
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>());
            _mockRepository.Setup(x => x.GetValueAsync(key, It.IsAny<string>()))
                          .ReturnsAsync(inputValue ?? string.Empty);

            // Act
            var result = await _service.GetBoolAsync(key, false);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task SetValueAsync_WithValidData_ShouldUpdateCacheAndRepository()
        {
            // Arrange
            var key = "updateKey";
            var value = "newValue";

            // Mock repository to handle the update
            _mockRepository.Setup(x => x.SetValueAsync(key, value))
                          .Returns(Task.CompletedTask)
                          .Verifiable();

            var testPref = CreateTestPreference(key, "oldValue");
            _mockRepository.Setup(x => x.GetByKeyAsync(key))
                          .ReturnsAsync(testPref);

            // Initialize cache with existing value
            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.TryAdd(key, CreateTestPreference(key, "oldValue"));

            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            await _service.SetValueAsync(key, value);

            // Assert
            _mockRepository.Verify(x => x.SetValueAsync(key, value), Times.Once);
            _mockLogger.Verify(x => x.Debug("Setting updated: {Key}", key), Times.Once);
        }

        [Fact]
        public async Task SetValueAsync_WhenRepositoryThrows_ShouldPropagateException()
        {
            // Arrange
            var key = "errorKey";
            var value = "errorValue";
            var expectedException = new InvalidOperationException("Test exception");

            _mockRepository.Setup(x => x.SetValueAsync(key, value))
                          .ThrowsAsync(expectedException);

            // Initialize cache
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act & Assert
            await _service.Invoking(s => s.SetValueAsync(key, value))
                         .Should().ThrowAsync<InvalidOperationException>()
                         .WithMessage("Test exception");

            _mockLogger.Verify(x => x.Error(expectedException, "Error updating setting {Key}.", key), Times.Once);
        }

        [Fact]
        public async Task ResetAllToDefaultsAsync_ShouldClearCacheAndRepository()
        {
            // Arrange
            var testPrefs = CreateTestPreferencesList(3);

            // ✅ CORREGIDO: Usar ReturnsAsync
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(testPrefs); // ✅ Tarea que SI completa

            _mockRepository.Setup(x => x.DeleteAllAsync())
                          .ReturnsAsync(1)
                          .Verifiable();

            // También mockear GetValueAsync para la inicialización
            _mockRepository.Setup(x => x.GetValueAsync("anyKey", It.IsAny<string>()))
                          .ReturnsAsync("default");

            // Initialize cache - pero mejor usar reflexión directa
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.Clear();
            foreach (var pref in testPrefs)
            {
                cache?.TryAdd(pref.Key, pref);
            }

            // Act
            await _service.ResetAllToDefaultsAsync();

            // Assert
            _mockRepository.Verify(x => x.DeleteAllAsync(), Times.Once);
            cache?.Count.Should().Be(0); // Cache should be cleared
            _mockLogger.Verify(x => x.Information("All settings have been reset to default values."), Times.Once);
        }

        [Fact]
        public async Task ExportToFileAsync_WithValidData_ShouldExportSuccessfully()
        {
            // Arrange
            var filePath = "test_export.json";
            var testPrefs = CreateTestPreferencesList(2);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(testPrefs);

            // Act
            await _service.ExportToFileAsync(filePath);

            // Assert
            _mockRepository.Verify(x => x.GetAllAsync(), Times.Once);
            _mockLogger.Verify(x => x.Information("Settings successfully exported to {Path}.", filePath), Times.Once);

            // Verify file was created and contains valid JSON
            File.Exists(filePath).Should().BeTrue();
            var json = await File.ReadAllTextAsync(filePath);
            var deserialized = JsonSerializer.Deserialize<List<UserPreferences>>(json);
            deserialized.Should().NotBeNull();
            deserialized?.Count.Should().Be(2);

            // Cleanup
            File.Delete(filePath);
        }

        [Fact]
        public async Task ExportToFileAsync_WhenFileWriteFails_ShouldThrowException()
        {
            // Arrange
            var filePath = "test_export.json";
            var testPrefs = CreateTestPreferencesList(2);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(testPrefs);

            // Create a directory with the same name to cause write failure
            Directory.CreateDirectory(filePath);

            // Act & Assert
            await _service.Invoking(s => s.ExportToFileAsync(filePath))
                         .Should().ThrowAsync<Exception>();

            _mockLogger.Verify(x => x.Error(It.IsAny<Exception>(), "Export failed."), Times.Once);

            // Cleanup
            if (Directory.Exists(filePath))
                Directory.Delete(filePath);
        }

        [Fact]
        public async Task ImportFromFileAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var invalidPath = "nonexistent.json";

            // Act & Assert
            await _service.Invoking(s => s.ImportFromFileAsync(invalidPath))
                         .Should().ThrowAsync<FileNotFoundException>()
                         .WithMessage("Import file not found.*");
        }

        [Fact]
        public async Task ImportFromFileAsync_WithInvalidJson_ShouldThrowJsonException()
        {
            // Arrange
            var filePath = "invalid.json";
            var invalidJson = "{ invalid json }";
            await File.WriteAllTextAsync(filePath, invalidJson);

            // Act & Assert
            await _service.Invoking(s => s.ImportFromFileAsync(filePath))
                         .Should().ThrowAsync<JsonException>();

            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        [Fact]
        public async Task ImportFromFileAsync_WithNullDeserializedList_ShouldThrowInvalidDataException()
        {
            // Arrange
            var filePath = "null_list.json";
            var json = "null"; // Esto deserializará a null
            await File.WriteAllTextAsync(filePath, json);

            // Act & Assert
            await _service.Invoking(s => s.ImportFromFileAsync(filePath))
                         .Should().ThrowAsync<InvalidDataException>()
                         .WithMessage("Invalid JSON format.");

            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        [Fact]
        public async Task ImportFromFileAsync_WithValidJson_ShouldImportSuccessfully()
        {
            // Arrange
            var filePath = "valid_import.json";
            var testPrefs = CreateTestPreferencesList(3);
            var json = JsonSerializer.Serialize(testPrefs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            // ✅ CORREGIDO: Usar ReturnsAsync en lugar de setup por defecto
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>()); // ✅ Tarea que SI completa

            _mockRepository.Setup(x => x.DeleteAllAsync())
                          .ReturnsAsync(1)
                          .Verifiable();

            _mockRepository.Setup(x => x.InsertAllAsync(It.IsAny<List<UserPreferences>>()))
                          .ReturnsAsync(1)
                          .Verifiable();

            // Clear cache state
            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.Clear();

            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, false);

            // Act
            await _service.ImportFromFileAsync(filePath);

            // Assert
            _mockRepository.Verify(x => x.DeleteAllAsync(), Times.Once);
            _mockRepository.Verify(x => x.InsertAllAsync(It.Is<List<UserPreferences>>(list => list.Count == 3)), Times.Once);
            _mockLogger.Verify(x => x.Information("Settings successfully imported from {Path}.", filePath), Times.Once);

            // Cleanup
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        [Fact]
        public async Task SetValueAsync_WhenSettingUpdated_ShouldRaiseOnPreferenceChangedEvent()
        {
            // Arrange
            var key = "eventKey";
            var value = "newValue";
            bool eventRaised = false;
            string? eventKey = null;
            string? eventValue = null;

            // Suscribirse al evento
            _service.OnPreferenceChanged += (sender, args) =>
            {
                eventRaised = true;
                eventKey = args.Key;
                eventValue = args.NewValue;
                sender.Should().Be(_service);
            };

            // Configurar mocks para SetValueAsync
            _mockRepository.Setup(x => x.SetValueAsync(key, value))
                          .Returns(Task.CompletedTask);

            var testPref = CreateTestPreference(key, "oldValue");
            _mockRepository.Setup(x => x.GetByKeyAsync(key))
                          .ReturnsAsync(testPref);

            // Initialize cache
            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.TryAdd(key, CreateTestPreference(key, "oldValue"));

            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act - Esta llamada debería disparar el evento internamente
            await _service.SetValueAsync(key, value);

            // Assert
            eventRaised.Should().BeTrue();
            eventKey.Should().Be(key);
            eventValue.Should().Be(value);
        }

        [Fact]
        public async Task EnsureCacheInitializedAsync_WhenCalledConcurrently_ShouldInitializeOnce()
        {
            // Arrange
            var testPrefs = CreateTestPreferencesList(5);
            var callCount = 0;

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(testPrefs)
                          .Callback(() => callCount++);

            // Act - Simulate concurrent calls
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // Access any method that triggers cache initialization
                    await _service.GetValueAsync("testKey", "default");
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            callCount.Should().Be(1); // Repository should be called only once
            _mockLogger.Verify(x => x.Debug("Settings cache initialized with {Count} items.", testPrefs.Count), Times.Once);
        }

        [Theory]
        [InlineData("testKey", "123", 123)]
        [InlineData("testKey", "invalid", 0)] // default value
        [InlineData("testKey", "", 0)]
        public async Task GetIntAsync_WithVariousInputs_ShouldParseCorrectly(string key, string storedValue, int expected)
        {
            // Arrange
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>());
            _mockRepository.Setup(x => x.GetValueAsync(key, It.IsAny<string>()))
                          .ReturnsAsync(storedValue);

            // Act
            var result = await _service.GetIntAsync(key, 0);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("testKey", "123.45", 123.45)]
        [InlineData("testKey", "invalid", 0.0)] // default value
        [InlineData("testKey", "", 0.0)]
        public async Task GetDoubleAsync_WithVariousInputs_ShouldParseCorrectly(string key, string storedValue, double expected)
        {
            // Arrange
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>());
            _mockRepository.Setup(x => x.GetValueAsync(key, It.IsAny<string>()))
                          .ReturnsAsync(storedValue);

            // Act
            var result = await _service.GetDoubleAsync(key, 0.0);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetAllCategorizedAsync_ShouldReturnCategorizedDictionary()
        {
            // Arrange
            var expectedDict = new Dictionary<string, List<UserPreferences>>
            {
                ["UI"] = new List<UserPreferences>
                {
                    CreateTestPreference("ui.theme", "dark"),
                    CreateTestPreference("ui.fontSize", "14")
                },
                ["Feeds"] = new List<UserPreferences>
                {
                    CreateTestPreference("feed.refreshInterval", "30")
                },
                ["General"] = new List<UserPreferences>
                {
                    CreateTestPreference("app.version", "1.0")
                }
            };

            _mockRepository.Setup(x => x.GetAllCategorizedAsync())
                          .ReturnsAsync(expectedDict);

            // Act
            var result = await _service.GetAllCategorizedAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedDict);
            _mockRepository.Verify(x => x.GetAllCategorizedAsync(), Times.Once);
        }

        [Fact]
        public async Task ResetToDefaultAsync_ShouldCallSetValueWithDefault()
        {
            // Arrange
            var key = "resetKey";

            _mockRepository.Setup(x => x.SetValueAsync(key, It.IsAny<string>()))
                          .Returns(Task.CompletedTask)
                          .Verifiable();

            // Initialize cache
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            await _service.ResetToDefaultAsync(key);

            // Assert
            _mockRepository.Verify(x => x.SetValueAsync(key, It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task SetBoolAsync_ShouldCallSetValueWithStringRepresentation()
        {
            // Arrange
            var key = "boolKey";
            bool value = true;

            _mockRepository.Setup(x => x.SetValueAsync(key, "True"))
                          .Returns(Task.CompletedTask)
                          .Verifiable();

            // Initialize cache
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            await _service.SetBoolAsync(key, value);

            // Assert
            _mockRepository.Verify(x => x.SetValueAsync(key, "True"), Times.Once);
        }

        [Fact]
        public async Task SetIntAsync_ShouldCallSetValueWithStringRepresentation()
        {
            // Arrange
            var key = "intKey";
            int value = 42;

            _mockRepository.Setup(x => x.SetValueAsync(key, "42"))
                          .Returns(Task.CompletedTask)
                          .Verifiable();

            // Initialize cache
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            await _service.SetIntAsync(key, value);

            // Assert
            _mockRepository.Verify(x => x.SetValueAsync(key, "42"), Times.Once);
        }

        [Fact]
        public async Task SetDoubleAsync_ShouldCallSetValueWithStringRepresentation()
        {
            // Arrange
            var key = "doubleKey";
            double value = 3.14159;

            _mockRepository.Setup(x => x.SetValueAsync(key, value.ToString()))
                          .Returns(Task.CompletedTask)
                          .Verifiable();

            // Initialize cache
            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, true);

            // Act
            await _service.SetDoubleAsync(key, value);

            // Assert
            _mockRepository.Verify(x => x.SetValueAsync(key, value.ToString()), Times.Once);
        }

        [Fact]
        public async Task GetValueAsync_WhenRepositoryReturnsNull_ShouldReturnDefaultValue()
        {
            // Arrange
            var key = "nullKey";
            var defaultValue = "default";

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(new List<UserPreferences>());
            _mockRepository.Setup(x => x.GetValueAsync(key, defaultValue))
                          .ReturnsAsync(defaultValue);
            _mockRepository.Setup(x => x.GetByKeyAsync(key))
                          .ReturnsAsync((UserPreferences?)null);

            // Act
            var result = await _service.GetValueAsync(key, defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task EnsureCacheInitializedAsync_WhenRepositoryThrows_ShouldLogError()
        {
            // Arrange
            var exception = new InvalidOperationException("DB connection failed");

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ThrowsAsync(exception);

            // Access cache initialization
            var cacheField = typeof(SettingsService).GetField("_cache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<string, UserPreferences>;
            cache?.Clear(); // Clear any existing cache

            var initField = typeof(SettingsService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(_service, false);

            // Act
            // This will trigger cache initialization
            await _service.GetValueAsync("anyKey", "default");

            // Assert
            _mockLogger.Verify(x => x.Error(exception, "Failed to initialize settings cache."), Times.Once);
        }
    }
}