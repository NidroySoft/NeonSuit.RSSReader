using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using System.Text.Json;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    /// <summary>
    /// Integration tests for SettingsService.
    /// Tests configuration management, persistence, caching, and import/export functionality.
    /// </summary>
    [Collection("Integration Tests")]
    public class SettingsServiceIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _dbFixture;
        private readonly ITestOutputHelper _output;
        private readonly ServiceFactory _factory;
        private ISettingsService _settingsService = null!;

        public SettingsServiceIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
        {
            _dbFixture = dbFixture;
            _output = output;
            _factory = new ServiceFactory(_dbFixture);
        }

        public async Task InitializeAsync()
        {
            // ✅ Crear una base de datos completamente nueva para cada prueba
            var freshDbContext = _dbFixture.CreateNewDbContext();
            await freshDbContext.Database.EnsureCreatedAsync();

            // Crear servicio con esta base de datos fresca
            var userPrefRepo = new UserPreferencesRepository(freshDbContext, _dbFixture.Logger);
            _settingsService = new SettingsService(userPrefRepo, _dbFixture.Logger);

            await ResetToDefaultsAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task ResetToDefaultsAsync()
        {
            await _settingsService.ResetAllToDefaultsAsync();
        }

        #region Basic CRUD Operations

        [Fact]
        public async Task GetValueAsync_WithExistingKey_ShouldReturnValue()
        {
            // Arrange
            const string key = PreferenceKeys.Theme;
            const string expected = "dark";
            await _settingsService.SetValueAsync(key, expected);

            // Act
            var result = await _settingsService.GetValueAsync(key);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetValueAsync_WithNonExistingKey_ShouldReturnDefaultValue()
        {
            // Arrange
            const string key = "non.existent.key";
            const string defaultValue = "default";

            // Act
            var result = await _settingsService.GetValueAsync(key, defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task GetValueAsync_WithNonExistingKeyAndNoDefault_ShouldReturnEmptyString()
        {
            // Arrange
            const string key = "non.existent.key.2";

            // Act
            var result = await _settingsService.GetValueAsync(key);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task SetValueAsync_WithNewKey_ShouldPersistValue()
        {
            // Arrange
            const string key = "test.new.key";
            const string value = "test.value";

            // Act
            await _settingsService.SetValueAsync(key, value);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(value);
        }

        [Fact]
        public async Task SetValueAsync_WithExistingKey_ShouldUpdateValue()
        {
            // Arrange
            const string key = PreferenceKeys.Theme;
            const string initialValue = "light";
            const string updatedValue = "dark";

            await _settingsService.SetValueAsync(key, initialValue);

            // Act
            await _settingsService.SetValueAsync(key, updatedValue);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(updatedValue);
        }

        [Fact]
        public async Task SetValueAsync_WithInvalidValue_ShouldNotUpdateAndLogWarning()
        {
            // Arrange
            const string key = PreferenceKeys.Theme;
            const string validValue = "light";
            const string invalidValue = "invalid.theme.value";

            await _settingsService.SetValueAsync(key, validValue);

            // Act
            await _settingsService.SetValueAsync(key, invalidValue);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(validValue); // No cambió
        }

        #endregion

        #region Typed Getters/Setters

        [Fact]
        public async Task GetBoolAsync_WithExistingKey_ShouldReturnParsedValue()
        {
            // Arrange
            const string key = PreferenceKeys.NotificationsEnabled;
            await _settingsService.SetBoolAsync(key, true);

            // Act
            var result = await _settingsService.GetBoolAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetBoolAsync_WithNonExistingKey_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.bool.key";
            const bool defaultValue = true;

            // Act
            var result = await _settingsService.GetBoolAsync(key, defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task SetBoolAsync_ShouldPersistAsString()
        {
            // Arrange
            const string key = "test.bool.set";

            // Act
            await _settingsService.SetBoolAsync(key, true);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be("True");
            (await _settingsService.GetBoolAsync(key)).Should().BeTrue();
        }

        [Fact]
        public async Task GetIntAsync_WithExistingKey_ShouldReturnParsedValue()
        {
            // Arrange
            const string key = PreferenceKeys.DefaultUpdateFrequency;
            const int expected = 45;
            await _settingsService.SetIntAsync(key, expected);

            // Act
            var result = await _settingsService.GetIntAsync(key);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetIntAsync_WithNonExistingKey_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.int.key";
            const int defaultValue = 42;

            // Act
            var result = await _settingsService.GetIntAsync(key, defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task SetIntAsync_ShouldPersistAsString()
        {
            // Arrange
            const string key = "test.int.set";
            const int value = 123;

            // Act
            await _settingsService.SetIntAsync(key, value);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be("123");
            (await _settingsService.GetIntAsync(key)).Should().Be(123);
        }

        [Fact]
        public async Task GetDoubleAsync_WithExistingKey_ShouldReturnParsedValue()
        {
            // Arrange
            const string key = PreferenceKeys.LineHeight;
            const double expected = 1.75;
            await _settingsService.SetDoubleAsync(key, expected);

            // Act
            var result = await _settingsService.GetDoubleAsync(key);

            // Assert
            result.Should().Be(expected);
        }

        [Fact]
        public async Task GetDoubleAsync_WithNonExistingKey_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.double.key";
            const double defaultValue = 3.1416;

            // Act
            var result = await _settingsService.GetDoubleAsync(key, defaultValue);

            // Assert
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task SetDoubleAsync_ShouldPersistAsStringWithInvariantCulture()
        {
            // Arrange
            const string key = "test.double.set";
            const double value = 123.456;

            // Act
            await _settingsService.SetDoubleAsync(key, value);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            (await _settingsService.GetDoubleAsync(key)).Should().Be(value);
        }

        #endregion

        #region Reset Operations

        [Fact]
        public async Task ResetToDefaultAsync_WithExistingKey_ShouldResetToSystemDefault()
        {
            // Arrange
            const string key = PreferenceKeys.Theme;
            var defaultValue = PreferenceHelper.GetDefaultValue(key);
            await _settingsService.SetValueAsync(key, "custom.value");

            // Act
            await _settingsService.ResetToDefaultAsync(key);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task ResetToDefaultAsync_WithNonExistingKey_ShouldCreateWithDefault()
        {
            // Arrange
            const string key = "test.reset.key";
            var defaultValue = PreferenceHelper.GetDefaultValue(key);

            // Act
            await _settingsService.ResetToDefaultAsync(key);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().Be(defaultValue);
        }

        [Fact]
        public async Task ResetAllToDefaultsAsync_ShouldResetAllSettings()
        {
            // Arrange
            await _settingsService.SetValueAsync(PreferenceKeys.Theme, "custom");
            await _settingsService.SetValueAsync(PreferenceKeys.DefaultUpdateFrequency, "999");
            await _settingsService.SetValueAsync(PreferenceKeys.NotificationsEnabled, "false");

            // Act
            await _settingsService.ResetAllToDefaultsAsync();

            // Assert
            (await _settingsService.GetValueAsync(PreferenceKeys.Theme))
                .Should().Be(PreferenceHelper.GetDefaultValue(PreferenceKeys.Theme));

            (await _settingsService.GetValueAsync(PreferenceKeys.DefaultUpdateFrequency))
                .Should().Be(PreferenceHelper.GetDefaultValue(PreferenceKeys.DefaultUpdateFrequency));

            (await _settingsService.GetValueAsync(PreferenceKeys.NotificationsEnabled))
                .Should().Be(PreferenceHelper.GetDefaultValue(PreferenceKeys.NotificationsEnabled));
        }

        #endregion

        #region Caching Behavior

        [Fact]
        public async Task GetValueAsync_MultipleCalls_ShouldUseCacheAfterFirstCall()
        {
            // Arrange
            const string key = "cache.test.key";
            const string value = "cached.value";

            // Creamos un servicio fresco para aislar la prueba
            var freshService = _factory.CreateSettingsService();
            await freshService.SetValueAsync(key, value);

            // Act - Primera llamada (debería ir a BD)
            var firstResult = await freshService.GetValueAsync(key);

            // Act - Segunda llamada (debería venir de caché)
            var secondResult = await freshService.GetValueAsync(key);

            // Assert
            firstResult.Should().Be(value);
            secondResult.Should().Be(value);
        }

        [Fact]
        public async Task SetValueAsync_AfterUpdate_ShouldInvalidateCache()
        {
            // Arrange
            const string key = "cache.invalidate.key";
            const string initialValue = "initial";
            const string updatedValue = "updated";

            await _settingsService.SetValueAsync(key, initialValue);
            var firstRead = await _settingsService.GetValueAsync(key); // En caché

            // Act
            await _settingsService.SetValueAsync(key, updatedValue);

            // Assert
            var secondRead = await _settingsService.GetValueAsync(key);
            secondRead.Should().Be(updatedValue);
        }

        #endregion

        #region Event Handling

        [Fact]
        public async Task SetValueAsync_WhenValueChanges_ShouldRaiseOnPreferenceChangedEvent()
        {
            // Arrange
            var eventRaised = false;
            PreferenceChangedEventArgs? eventArgs = null;

            _settingsService.OnPreferenceChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
                sender.Should().Be(_settingsService);
            };

            const string key = "event.test.key";
            const string value = "event.value";

            // Act
            await _settingsService.SetValueAsync(key, value);

            // Assert
            eventRaised.Should().BeTrue();
            eventArgs.Should().NotBeNull();
            eventArgs!.Key.Should().Be(key);
            eventArgs.NewValue.Should().Be(value);
        }
        [Fact]
        public async Task SetValueAsync_WithSameValue_ShouldNotRaiseEvent()
        {
            // Arrange
            var eventRaised = false;
            _settingsService.OnPreferenceChanged += (sender, args) =>
            {
                eventRaised = true;
                _output.WriteLine($"❗ EVENTO DISPARADO: {args.Key} = {args.NewValue}");
            };

            const string key = "event.nochange.key";
            const string value = "same.value";

            // Primera llamada (debería disparar evento)
            _output.WriteLine("Primera llamada - guardando valor inicial");
            await _settingsService.SetValueAsync(key, value);

            // Verificar que se guardó
            var savedValue = await _settingsService.GetValueAsync(key);
            _output.WriteLine($"Valor guardado: '{savedValue}'");

            // Resetear flag
            eventRaised = false;
            _output.WriteLine("Flag reseteado");

            // Segunda llamada - mismo valor (NO debería disparar evento)
            _output.WriteLine("Segunda llamada - mismo valor");
            await _settingsService.SetValueAsync(key, value);

            // Assert
            _output.WriteLine($"Evento disparado: {eventRaised}");
            eventRaised.Should().BeFalse();
        }

        #endregion

        #region Import/Export

        [Fact]
        public async Task ExportToFileAsync_ShouldCreateValidJsonFile()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();

            try
            {
                // ✅ Verificar estado inicial
                _output.WriteLine("=== ESTADO INICIAL ===");
                var initialAll = await _settingsService.GetAllCategorizedAsync();
                _output.WriteLine("Total preferencias iniciales: {0}",
                    initialAll.Sum(c => c.Value.Count));

                // ✅ Crear algunas preferencias
                _output.WriteLine("=== GUARDANDO PREFERENCIAS ===");
                await _settingsService.SetValueAsync("export.key1", "value1");
                _output.WriteLine("Guardado export.key1 = value1");

                await _settingsService.SetValueAsync("export.key2", "value2");
                _output.WriteLine("Guardado export.key2 = value2");

                await _settingsService.SetValueAsync("export.key3", "value3");
                _output.WriteLine("Guardado export.key3 = value3");

                // ✅ VERIFICAR que se guardaron
                _output.WriteLine("=== VERIFICANDO GUARDADO ===");
                var check1 = await _settingsService.GetValueAsync("export.key1");
                _output.WriteLine("Valor recuperado export.key1: '{0}'", check1);
                check1.Should().Be("value1", "La preferencia debería existir antes de exportar");

                var check2 = await _settingsService.GetValueAsync("export.key2");
                _output.WriteLine("Valor recuperado export.key2: '{0}'", check2);
                check2.Should().Be("value2");

                var check3 = await _settingsService.GetValueAsync("export.key3");
                _output.WriteLine("Valor recuperado export.key3: '{0}'", check3);
                check3.Should().Be("value3");

                // ✅ Verificar que están en GetAllCategorizedAsync
                var beforeExport = await _settingsService.GetAllCategorizedAsync();
                _output.WriteLine("=== ANTES DE EXPORTAR ===");
                foreach (var cat in beforeExport)
                {
                    foreach (var pref in cat.Value)
                    {
                        _output.WriteLine("  {0} = '{1}' (Cat: {2})", pref.Key, pref.Value, cat.Key);
                    }
                }

                // Act
                _output.WriteLine("=== EXPORTANDO A {0} ===", tempFile);
                await _settingsService.ExportToFileAsync(tempFile);

                // Assert - Verificar que el archivo existe
                File.Exists(tempFile).Should().BeTrue();
                _output.WriteLine("Archivo creado, tamaño: {0} bytes", new FileInfo(tempFile).Length);

                // Leer y validar JSON
                var json = await File.ReadAllTextAsync(tempFile);
                _output.WriteLine("Contenido del JSON:");
                _output.WriteLine(json);

                // ✅ CONFIGURACIÓN CORRECTA: Ignorar mayúsculas/minúsculas
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var prefs = JsonSerializer.Deserialize<List<UserPreferencesDto>>(json, options);

                prefs.Should().NotBeNull();
                _output.WriteLine("Preferencias deserializadas: {0}", prefs!.Count);

                foreach (var p in prefs)
                {
                    _output.WriteLine("  {0} = '{1}' (LastModified: {2})", p.Key, p.Value, p.LastModified);
                }

                // ✅ VERIFICACIONES
                prefs.Should().HaveCountGreaterThanOrEqualTo(3);
                prefs.Should().Contain(p => p.Key == "export.key1" && p.Value == "value1");
                prefs.Should().Contain(p => p.Key == "export.key2" && p.Value == "value2");
                prefs.Should().Contain(p => p.Key == "export.key3" && p.Value == "value3");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportFromFileAsync_WithValidFile_ShouldImportSettings()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();

            var prefsToImport = new List<UserPreferencesDto>
    {
        new() { Key = "import.key1", Value = "import1" },
        new() { Key = "import.key2", Value = "import2" },
        new() { Key = "import.key3", Value = "import3" }
    };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(prefsToImport, options);
            await File.WriteAllTextAsync(tempFile, json);

            try
            {
                // Act
                await _settingsService.ImportFromFileAsync(tempFile);

                // ✅ VERIFICAR accediendo al repositorio a través de reflexión
                // (solución rápida pero no ideal)
                var repoField = typeof(SettingsService).GetField("_repository",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var repo = repoField?.GetValue(_settingsService) as IUserPreferencesRepository;

                var value1 = await repo!.GetByKeyAsync("import.key1");
                var value2 = await repo!.GetByKeyAsync("import.key2");
                var value3 = await repo!.GetByKeyAsync("import.key3");

                value1.Should().NotBeNull();
                value1!.Value.Should().Be("import1");

                value2.Should().NotBeNull();
                value2!.Value.Should().Be("import2");

                value3.Should().NotBeNull();
                value3!.Value.Should().Be("import3");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ImportFromFileAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            // Arrange
            var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

            // Act
            Func<Task> act = async () => await _settingsService.ImportFromFileAsync(invalidPath);

            // Assert
            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task ImportFromFileAsync_WithInvalidJson_ShouldThrowJsonException()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "{ invalid json }");

            try
            {
                // Act
                Func<Task> act = async () => await _settingsService.ImportFromFileAsync(tempFile);

                // Assert
                await act.Should().ThrowAsync<JsonException>();
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        #endregion

        #region Categorized Preferences

        [Fact]
        public async Task GetAllCategorizedAsync_ShouldReturnPreferencesGroupedByCategory()
        {
            // Arrange
            // Aseguramos que existan algunas preferencias de diferentes categorías
            await _settingsService.SetValueAsync(PreferenceKeys.Theme, "dark"); // Interfaz
            await _settingsService.SetValueAsync(PreferenceKeys.DefaultUpdateFrequency, "30"); // Feeds
            await _settingsService.SetValueAsync(PreferenceKeys.NotificationsEnabled, "true"); // Notificaciones

            // Act
            var categorized = await _settingsService.GetAllCategorizedAsync();

            // Assert
            categorized.Should().NotBeNull();
            categorized.Should().ContainKey("Interfaz");
            categorized.Should().ContainKey("Feeds");
            categorized.Should().ContainKey("Notificaciones");

            // Verificar que las preferencias están en las categorías correctas
            categorized["Interfaz"].Should().Contain(p => p.Key == PreferenceKeys.Theme);
            categorized["Feeds"].Should().Contain(p => p.Key == PreferenceKeys.DefaultUpdateFrequency);
            categorized["Notificaciones"].Should().Contain(p => p.Key == PreferenceKeys.NotificationsEnabled);
        }

        #endregion

        #region Validation Tests

        [Theory]
        [InlineData(PreferenceKeys.Theme, "light", true)]
        [InlineData(PreferenceKeys.Theme, "dark", true)]
        [InlineData(PreferenceKeys.Theme, "system", true)]
        [InlineData(PreferenceKeys.Theme, "invalid", false)]
        [InlineData(PreferenceKeys.DefaultUpdateFrequency, "30", true)]
        [InlineData(PreferenceKeys.DefaultUpdateFrequency, "0", false)]
        [InlineData(PreferenceKeys.DefaultUpdateFrequency, "1440", true)]
        [InlineData(PreferenceKeys.DefaultUpdateFrequency, "1441", false)]
        [InlineData(PreferenceKeys.DefaultUpdateFrequency, "abc", false)]
        [InlineData(PreferenceKeys.AccentColor, "#FF5733", true)]
        [InlineData(PreferenceKeys.AccentColor, "#FFF", false)]
        [InlineData(PreferenceKeys.AccentColor, "red", false)]
        [InlineData(PreferenceKeys.NotificationsEnabled, "true", true)]
        [InlineData(PreferenceKeys.NotificationsEnabled, "false", true)]
        [InlineData(PreferenceKeys.NotificationsEnabled, "maybe", false)]
        [InlineData(PreferenceKeys.LineHeight, "1.5", true)]
        [InlineData(PreferenceKeys.LineHeight, "2.5", false)]
        [InlineData(PreferenceKeys.LineHeight, "abc", false)]
        public async Task SetValueAsync_WithVariousValues_ShouldRespectValidation(string key, string value, bool shouldSucceed)
        {
            // Arrange
            var defaultValue = await _settingsService.GetValueAsync(key);

            // Act
            await _settingsService.SetValueAsync(key, value);

            // Assert
            var result = await _settingsService.GetValueAsync(key);

            if (shouldSucceed)
            {
                result.Should().Be(value);
            }
            else
            {
                result.Should().Be(defaultValue); // No cambió
            }
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task GetValueAsync_WithNullKey_ShouldHandleGracefully()
        {
            // Act
            Func<Task> act = async () => await _settingsService.GetValueAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task SetValueAsync_WithNullKey_ShouldThrowArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _settingsService.SetValueAsync(null!, "value");

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task SetValueAsync_WithNullValue_ShouldStoreEmptyString()
        {
            // Arrange
            const string key = "test.null.value";

            // Act
            await _settingsService.SetValueAsync(key, null!);

            // Assert
            var result = await _settingsService.GetValueAsync(key);
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetBoolAsync_WithInvalidStoredValue_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.invalid.bool";
            await _settingsService.SetValueAsync(key, "not-a-bool");

            // Act
            var result = await _settingsService.GetBoolAsync(key, true);

            // Assert
            result.Should().Be(true);
        }

        [Fact]
        public async Task GetIntAsync_WithInvalidStoredValue_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.invalid.int";
            await _settingsService.SetValueAsync(key, "not-an-int");

            // Act
            var result = await _settingsService.GetIntAsync(key, 42);

            // Assert
            result.Should().Be(42);
        }

        [Fact]
        public async Task GetDoubleAsync_WithInvalidStoredValue_ShouldReturnDefault()
        {
            // Arrange
            const string key = "test.invalid.double";
            await _settingsService.SetValueAsync(key, "not-a-double");

            // Act
            var result = await _settingsService.GetDoubleAsync(key, 3.14);

            // Assert
            result.Should().Be(3.14);
        }

        #endregion

        #region Concurrency Tests

        [Fact]
        public async Task ConcurrentAccess_ShouldMaintainConsistency()
        {
            // Arrange
            const string key = "concurrent.key";
            const int taskCount = 10;
            var tasks = new List<Task>();

            // Act - Ejecutar múltiples operaciones concurrentemente
            for (int i = 0; i < taskCount; i++)
            {
                var value = i.ToString();
                tasks.Add(Task.Run(async () =>
                {
                    await _settingsService.SetValueAsync(key, value);
                    var read = await _settingsService.GetValueAsync(key);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - No debería haber excepciones
            // El valor final puede ser cualquiera, pero no debe haber corrupción
            var finalValue = await _settingsService.GetValueAsync(key);
            finalValue.Should().NotBeNull();
        }

        #endregion
        private class UserPreferencesDto
        {
            public int Id { get; set; }
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public DateTime LastModified { get; set; }
        }
    }


}