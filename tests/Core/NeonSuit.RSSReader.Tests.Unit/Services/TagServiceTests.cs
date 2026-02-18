using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;
using System.Collections.Concurrent;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Contains unit tests for the <see cref="TagService"/> class.
    /// Tests cover tag CRUD operations, caching, validation, and event handling.
    /// </summary>
    public class TagServiceTests
    {
        private readonly Mock<ITagRepository> _mockRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly TagService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="TagServiceTests"/> class.
        /// Sets up common mocks and configurations used across all tests.
        /// </summary>
        public TagServiceTests()
        {
            _mockRepository = new Mock<ITagRepository>();
            _mockLogger = new Mock<ILogger>();

            // Critical logger configuration - matches production pattern
            _mockLogger.Setup(x => x.ForContext<TagService>())
                      .Returns(_mockLogger.Object);

            // Initialize service with mocked dependencies
            _service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Replace the static logger with our mock using reflection
            // Note: Current implementation uses static logger - should be refactored to inject ILogger
            var loggerField = typeof(TagService).GetField("_logger",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            loggerField?.SetValue(_service, _mockLogger.Object);
        }

        /// <summary>
        /// Creates a test tag with optional parameters.
        /// </summary>
        /// <param name="id">The tag ID.</param>
        /// <param name="name">The tag name.</param>
        /// <param name="color">The tag color.</param>
        /// <returns>A configured test tag.</returns>
        private Tag CreateTestTag(int id = 1, string name = "TestTag", string color = "#3498db")
        {
            return new Tag
            {
                Id = id,
                Name = name,
                Color = color,
                CreatedAt = DateTime.UtcNow,
                UsageCount = 0,
                IsPinned = false,
                IsVisible = true
            };
        }

        /// <summary>
        /// Creates a list of test tags.
        /// </summary>
        /// <param name="count">Number of tags to create.</param>
        /// <returns>A list of test tags.</returns>
        private List<Tag> CreateTestTagsList(int count = 3)
        {
            var tags = new List<Tag>();
            for (int i = 1; i <= count; i++)
            {
                tags.Add(CreateTestTag(i, $"Tag{i}", $"#Color{i}"));
            }
            return tags;
        }

        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor initializes the service with empty cache.
        /// </summary>
        [Fact]
        public void Constructor_WhenCalled_ShouldInitializeWithEmptyCache()
        {
            // Arrange & Act
            var service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Assert
            // Verify cache is empty initially
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(service) as ConcurrentDictionary<int, Tag>;

            cache.Should().NotBeNull();
            cache?.Count.Should().Be(0);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when repository is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new TagService(null!, _mockLogger.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("tagRepository");
        }

        #endregion

        #region GetTagAsync Tests

        /// <summary>
        /// Tests that GetTagAsync returns cached tag when available.
        /// </summary>
        [Fact]
        public async Task GetTagAsync_WithCachedTag_ShouldReturnFromCacheWithoutRepositoryCall()
        {
            // Arrange
            var testTag = CreateTestTag(1, "CachedTag");
            var tagsList = new List<Tag> { testTag };

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tagsList);

            // Initialize cache
            await _service.GetAllTagsAsync();

            // Clear setup to verify no additional calls
            _mockRepository.Invocations.Clear();

            // Act
            var result = await _service.GetTagAsync(1);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(1);
            result.Name.Should().Be("CachedTag");

            // Verify no repository call was made for cached item
            _mockRepository.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// Tests that GetTagAsync queries repository when tag is not cached.
        /// </summary>
        [Fact]
        public async Task GetTagAsync_WithNonCachedTag_ShouldQueryRepositoryAndCacheResult()
        {
            // Arrange
            var testTag = CreateTestTag(2, "NonCachedTag");

            _mockRepository.Setup(x => x.GetByIdAsync(2))
                          .ReturnsAsync(testTag);

            // Crear el servicio con el logger mockeado
            var service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Inicializar cache vacío
            InitializeCacheAsEmpty(service);

            // Act
            var result = await service.GetTagAsync(2);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(2);
            result.Name.Should().Be("NonCachedTag");

            _mockRepository.Verify(x => x.GetByIdAsync(2), Times.Once);

            // Verify cache now contains the tag
            var cache = GetCache(service);
            cache.Should().ContainKey(2);
            cache[2].Name.Should().Be("NonCachedTag");
        }

        /// <summary>
        /// Helper method to get the cache dictionary.
        /// </summary>
        private ConcurrentDictionary<int, Tag> GetCache(TagService service)
        {
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ConcurrentDictionary<int, Tag>)cacheField!.GetValue(service)!;
        }

        /// <summary>
        /// Tests that GetTagAsync throws KeyNotFoundException when tag does not exist.
        /// </summary>
        [Fact]
        public async Task GetTagAsync_WithNonExistentId_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetByIdAsync(999))
                          .ReturnsAsync((Tag?)null);

            // Configurar el logger para evitar null reference
            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>()))
                      .Verifiable();

            // Inicializar el cache como vacío pero inicializado
            var service = new TagService(_mockRepository.Object, _mockLogger.Object);
            InitializeCacheAsEmpty(service);

            // Act
            Func<Task> act = async () => await service.GetTagAsync(999);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage("Tag with ID 999 not found.");
        }

        /// <summary>
        /// <summary>
        /// Tests that GetTagAsync caches the result after repository query.
        /// </summary>
        [Fact]
        public async Task GetTagAsync_AfterRepositoryQuery_ShouldAddToCache()
        {
            // Arrange
            var testTag = CreateTestTag(3, "NewTag");

            // Setup repository to return the tag
            _mockRepository.Setup(x => x.GetByIdAsync(3))
                          .ReturnsAsync(testTag);

            // Clear the cache completely and mark as initialized
            var cache = GetCache();
            cache.Clear();
            SetCacheAsInitialized();

            // Act - First call (should query repository since cache is empty)
            var result1 = await _service.GetTagAsync(3);

            // Assert - Verify repository was called
            _mockRepository.Verify(x => x.GetByIdAsync(3), Times.Once,
                "Repository should be called when tag is not in cache");

            // Act - Second call (should be from cache)
            _mockRepository.Invocations.Clear();
            var result2 = await _service.GetTagAsync(3);

            // Assert - Verify repository was NOT called again
            _mockRepository.Verify(x => x.GetByIdAsync(3), Times.Never,
                "Repository should not be called again when tag is cached");

            // Verify results are the same
            result1.Should().BeEquivalentTo(result2);

            // Verify tag was added to cache
            cache.Should().ContainKey(3);
            cache[3].Name.Should().Be("NewTag");
        }

        /// <summary>
        /// Helper method to initialize cache as empty and mark it as initialized.
        /// </summary>
        private void InitializeCacheAsEmpty(TagService service)
        {
            // Primero, establecer el cache como inicializado para evitar que se llame a EnsureCacheInitializedAsync
            var initField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(service, true);

            // Luego limpiar el cache si es necesario
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(service) as ConcurrentDictionary<int, Tag>;
            cache?.Clear();
        }

        #endregion

        #region GetTagByNameAsync Tests

        /// <summary>
        /// Tests that GetTagByNameAsync returns tag when it exists.
        /// </summary>
        [Fact]
        public async Task GetTagByNameAsync_WithExistingTag_ShouldReturnTag()
        {
            // Arrange
            var testTag = CreateTestTag(1, "ExistingTag");

            _mockRepository.Setup(x => x.GetByNameAsync("ExistingTag"))
                          .ReturnsAsync(testTag);

            // Act
            var result = await _service.GetTagByNameAsync("ExistingTag");

            // Assert
            result.Should().NotBeNull();
            result?.Id.Should().Be(1);
            result?.Name.Should().Be("ExistingTag");
        }

        /// <summary>
        /// Tests that GetTagByNameAsync returns null when tag does not exist.
        /// </summary>
        [Fact]
        public async Task GetTagByNameAsync_WithNonExistentTag_ShouldReturnNull()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetByNameAsync("NonExistent"))
                          .ReturnsAsync((Tag?)null);

            // Act
            var result = await _service.GetTagByNameAsync("NonExistent");

            // Assert
            result.Should().BeNull();
        }

        /// <summary>
        /// Tests that GetTagByNameAsync handles case-insensitive search.
        /// </summary>
        [Fact]
        public async Task GetTagByNameAsync_WithDifferentCase_ShouldFindTag()
        {
            // Arrange
            var testTag = CreateTestTag(1, "TestTag");

            // Configurar el mock para que coincida sin importar mayúsculas/minúsculas
            _mockRepository.Setup(x => x.GetByNameAsync(It.IsAny<string>()))
                          .ReturnsAsync((string name) =>
                              name.Equals("TestTag", StringComparison.OrdinalIgnoreCase) ? testTag : null);

            // También configurar el logger mockeado para evitar null reference
            SetupMockLoggerForCacheInitialization();

            var service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Act - Buscar con diferentes combinaciones de mayúsculas/minúsculas
            var result1 = await service.GetTagByNameAsync("TestTag");
            var result2 = await service.GetTagByNameAsync("testtag");
            var result3 = await service.GetTagByNameAsync("TESTTAG");

            // Assert - Todas deberían encontrar el tag
            result1.Should().NotBeNull();
            result2.Should().NotBeNull();
            result3.Should().NotBeNull();

            result1!.Id.Should().Be(1);
            result2!.Id.Should().Be(1);
            result3!.Id.Should().Be(1);
        }

        /// <summary>
        /// Helper method to setup mock logger for cache initialization.
        /// </summary>
        private void SetupMockLoggerForCacheInitialization()
        {
            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();
        }

        #endregion

        #region GetAllTagsAsync Tests

        /// <summary>
        /// Tests that GetAllTagsAsync returns all tags ordered by name.
        /// </summary>
        [Fact]
        public async Task GetAllTagsAsync_WhenCalled_ShouldReturnAllTagsOrderedByName()
        {
            // Arrange
            var tags = new List<Tag>
            {
                CreateTestTag(1, "Zebra"),
                CreateTestTag(2, "Apple"),
                CreateTestTag(3, "Banana")
            };

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tags);

            // Act
            var result = await _service.GetAllTagsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Select(t => t.Name).Should().BeInAscendingOrder();
            result[0].Name.Should().Be("Apple");
            result[1].Name.Should().Be("Banana");
            result[2].Name.Should().Be("Zebra");
        }

        /// <summary>
        /// Tests that GetAllTagsAsync initializes cache on first call.
        /// </summary>
        [Fact]
        public async Task GetAllTagsAsync_OnFirstCall_ShouldInitializeCache()
        {
            // Arrange
            var tags = CreateTestTagsList(2);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tags);

            // Act
            var result = await _service.GetAllTagsAsync();

            // Assert
            result.Should().HaveCount(2);

            // Verify cache was initialized
            _mockLogger.Verify(x => x.Debug(
                "Tag cache initialized with {Count} items.",
                2),
                Times.Once);
        }


        /// <summary>
        /// Tests that GetAllTagsAsync returns cached data on subsequent calls.
        /// </summary>
        [Fact]
        public async Task GetAllTagsAsync_OnSubsequentCalls_ShouldReturnFromCache()
        {
            // Arrange
            var tags = CreateTestTagsList(2);

            // Setup repository to return test tags
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tags);

            // Pre-initialize cache to avoid EnsureCacheInitializedAsync issues
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = (ConcurrentDictionary<int, Tag>)cacheField!.GetValue(_service)!;

            foreach (var tag in tags)
            {
                cache[tag.Id] = tag;
            }

            // Mark cache as initialized
            var initField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField!.SetValue(_service, true);

            // Clear any previous invocations
            _mockRepository.Invocations.Clear();

            // Act - First call (should use cache, not call repository)
            var result1 = await _service.GetAllTagsAsync();

            // Act - Second call (should also use cache)
            var result2 = await _service.GetAllTagsAsync();

            // Assert
            result1.Should().BeEquivalentTo(result2);
            result1.Should().HaveCount(2);

            // Verify repository was never called (because cache was pre-initialized)
            _mockRepository.Verify(x => x.GetAllAsync(), Times.Never,
                "Repository should not be called when cache is already initialized");
        }

        #endregion

        #region CreateTagAsync Tests

        /// <summary>
        /// Tests that CreateTagAsync successfully creates a new tag.
        /// </summary>
        [Fact]
        public async Task CreateTagAsync_WithValidTag_ShouldCreateAndReturnId()
        {
            // Arrange
            var newTag = CreateTestTag(0, "NewTag"); // ID will be set by repository

            _mockRepository.Setup(x => x.ExistsByNameAsync("NewTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1); // Return new ID

            var eventRaised = false;
            _service.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                args.TagId.Should().Be(1);
                args.TagName.Should().Be("NewTag");
                args.ChangeType.Should().Be(TagChangeType.Created);
            };

            // Act
            var result = await _service.CreateTagAsync(newTag);

            // Assert
            result.Should().Be(1);
            eventRaised.Should().BeTrue("TagChanged event should be raised");

            _mockRepository.Verify(x => x.InsertAsync(It.Is<Tag>(t =>
                t.Name == "NewTag" &&
                t.Color == "#3498db" &&
                t.CreatedAt != default)),
                Times.Once);

            _mockLogger.Verify(x => x.Information(
                "Tag created: {TagName} (ID: {TagId})",
                "NewTag", 1),
                Times.Once);
        }

        /// <summary>
        /// Tests that CreateTagAsync applies default color when not provided.
        /// </summary>
        [Fact]
        public async Task CreateTagAsync_WithoutColor_ShouldApplyDefaultColor()
        {
            // Arrange
            var newTag = new Tag { Name = "NoColorTag", Color = null! };

            _mockRepository.Setup(x => x.ExistsByNameAsync("NoColorTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            await _service.CreateTagAsync(newTag);

            // Assert
            _mockRepository.Verify(x => x.InsertAsync(It.Is<Tag>(t =>
                t.Color == "#3498db")),
                Times.Once);
        }

        /// <summary>
        /// Tests that CreateTagAsync throws ArgumentNullException when tag is null.
        /// </summary>
        [Fact]
        public async Task CreateTagAsync_WithNullTag_ShouldThrowArgumentNullException()
        {
            // Act
            Func<Task> act = async () => await _service.CreateTagAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("tag");
        }

        /// <summary>
        /// Tests that CreateTagAsync throws ArgumentException when tag name is empty.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task CreateTagAsync_WithEmptyName_ShouldThrowArgumentException(string invalidName)
        {
            // Arrange
            var tag = new Tag { Name = invalidName };

            // Act
            Func<Task> act = async () => await _service.CreateTagAsync(tag);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Tag name cannot be empty.");
        }

        /// <summary>
        /// Tests that CreateTagAsync throws InvalidOperationException when tag already exists.
        /// </summary>
        [Fact]
        public async Task CreateTagAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tag = CreateTestTag(0, "ExistingTag");

            _mockRepository.Setup(x => x.ExistsByNameAsync("ExistingTag"))
                          .ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _service.CreateTagAsync(tag);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag 'ExistingTag' already exists.");
        }

        /// <summary>
        /// Tests that CreateTagAsync adds new tag to cache.
        /// </summary>
        [Fact]
        public async Task CreateTagAsync_AfterSuccess_ShouldAddTagToCache()
        {
            // Arrange
            var newTag = CreateTestTag(0, "CachedAfterCreate");

            _mockRepository.Setup(x => x.ExistsByNameAsync("CachedAfterCreate"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(5);

            // Act
            await _service.CreateTagAsync(newTag);

            // Assert - Verify tag can be retrieved from cache
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<int, Tag>;

            cache.Should().ContainKey(5);
            cache?[5].Name.Should().Be("CachedAfterCreate");
        }

        #endregion

        #region UpdateTagAsync Tests

        /// <summary>
        /// Tests that UpdateTagAsync successfully updates an existing tag.
        /// </summary>
        [Fact]
        public async Task UpdateTagAsync_WithValidTag_ShouldUpdateAndRaiseEvent()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "OldName");
            var updatedTag = CreateTestTag(1, "NewName");

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(existingTag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("NewName"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            var service = CreateConfiguredTagService(existingTag);

            TagChangedEventArgs? capturedArgs = null;
            service.OnTagChanged += (sender, args) => capturedArgs = args;

            // Act
            await service.UpdateTagAsync(updatedTag);

            // Assert
            capturedArgs.Should().NotBeNull();
            capturedArgs?.TagName.Should().Be("NewName");
            capturedArgs?.ChangeType.Should().Be(TagChangeType.Updated);

            _mockRepository.Verify(x => x.UpdateAsync(It.Is<Tag>(t =>
                t.Id == 1 && t.Name == "NewName")),
                Times.Once);
        }

        /// <summary>
        /// Tests that UpdateTagAsync throws when trying to change to existing tag name.
        /// </summary>
        [Fact]
        public async Task UpdateTagAsync_WithDuplicateNewName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "OriginalName");
            var updatedTag = CreateTestTag(1, "DuplicateName");

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(existingTag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("DuplicateName"))
                          .ReturnsAsync(true);

            var service = CreateConfiguredTagService(existingTag);

            // Act
            Func<Task> act = async () => await service.UpdateTagAsync(updatedTag);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag 'DuplicateName' already exists.");
        }

        /// <summary>
        /// Tests that UpdateTagAsync allows same name update.
        /// </summary>
        [Fact]
        public async Task UpdateTagAsync_WithSameName_ShouldUpdateSuccessfully()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "SameName");
            var updatedTag = CreateTestTag(1, "SameName");
            updatedTag.Color = "#ff0000";

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(existingTag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("SameName"))
                          .ReturnsAsync(true);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            var service = CreateConfiguredTagService(existingTag);

            // Act
            await service.UpdateTagAsync(updatedTag);

            // Assert
            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Tag>()), Times.Once);
        }

        /// <summary>
        /// Tests that UpdateTagAsync updates cache after successful update.
        /// </summary>
        [Fact]
        public async Task UpdateTagAsync_AfterSuccess_ShouldUpdateCache()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "Original");
            var updatedTag = CreateTestTag(1, "Updated");

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(existingTag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("Updated"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            var service = CreateConfiguredTagService(existingTag);

            // Act
            await service.UpdateTagAsync(updatedTag);

            // Assert
            var cache = GetCache(service);
            cache.Should().ContainKey(1);
            cache[1].Name.Should().Be("Updated");
        }

        #endregion

        #region DeleteTagAsync Tests

        /// <summary>
        /// Tests that DeleteTagAsync successfully deletes a tag.
        /// </summary>
        /// <summary>
        /// Tests that DeleteTagAsync successfully deletes a tag.
        /// </summary>
        [Fact]
        public async Task DeleteTagAsync_WithExistingTag_ShouldDeleteAndRaiseEvent()
        {
            // Arrange
            var tagToDelete = CreateTestTag(1, "ToDelete");

            // Setup repository to return the tag
            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(tagToDelete);

            // Setup repository delete method
            _mockRepository.Setup(x => x.DeleteAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            var eventRaised = false;
            _service.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                args.TagId.Should().Be(1);
                args.TagName.Should().Be("ToDelete");
                args.ChangeType.Should().Be(TagChangeType.Deleted);
            };

            // Initialize cache manually to avoid EnsureCacheInitializedAsync
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<int, Tag>;
            if (cache != null)
            {
                cache[1] = tagToDelete;
            }

            // Set cache as initialized to bypass EnsureCacheInitializedAsync
            var cacheInitField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cacheInitField?.SetValue(_service, true);

            // Act
            await _service.DeleteTagAsync(1);

            // Assert
            eventRaised.Should().BeTrue("TagChanged event should be raised");

            _mockRepository.Verify(x => x.DeleteAsync(It.Is<Tag>(t =>
                t.Id == 1 && t.Name == "ToDelete")),
                Times.Once);

            // Verify cache removal
            cache.Should().NotContainKey(1);
        }

        /// <summary>
        /// Tests that DeleteTagAsync throws when tag does not exist.
        /// </summary>
        [Fact]
        public async Task DeleteTagAsync_WithNonExistentTag_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetByIdAsync(999))
                          .ReturnsAsync((Tag?)null);

            // Initialize cache as empty to avoid EnsureCacheInitializedAsync
            SetCacheAsInitialized();

            // Act
            Func<Task> act = async () => await _service.DeleteTagAsync(999);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }

        /// <summary>
        /// Helper method to initialize cache with a tag and mark it as initialized.
        /// </summary>
        private void InitializeCacheWithTag(Tag tag)
        {
            var cache = GetCache();
            cache[tag.Id] = tag;
            SetCacheAsInitialized();
        }

        /// <summary>
        /// Helper method to get the cache dictionary.
        /// </summary>
        private ConcurrentDictionary<int, Tag> GetCache()
        {
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (ConcurrentDictionary<int, Tag>)cacheField?.GetValue(_service)!;
        }

        /// <summary>
        /// Helper method to mark cache as initialized.
        /// </summary>
        private void SetCacheAsInitialized()
        {
            var cacheInitField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            cacheInitField?.SetValue(_service, true);
        }




        #endregion

        #region GetOrCreateTagAsync Tests

        /// <summary>
        /// Tests that GetOrCreateTagAsync returns existing tag when found.
        /// </summary>
        [Fact]
        public async Task GetOrCreateTagAsync_WithExistingTag_ShouldReturnExistingTag()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "Existing");

            _mockRepository.Setup(x => x.GetByNameAsync("Existing"))
                          .ReturnsAsync(existingTag);

            // Act
            var result = await _service.GetOrCreateTagAsync("Existing");

            // Assert
            result.Should().BeSameAs(existingTag);
            _mockRepository.Verify(x => x.InsertAsync(It.IsAny<Tag>()), Times.Never,
                "Should not create new tag when existing found");
        }

        /// <summary>
        /// Tests that GetOrCreateTagAsync creates new tag when not found.
        /// </summary>
        [Fact]
        public async Task GetOrCreateTagAsync_WithNewTag_ShouldCreateAndReturnTag()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetByNameAsync("NewTag"))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.ExistsByNameAsync("NewTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            var result = await _service.GetOrCreateTagAsync("NewTag", "#ff0000");

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("NewTag");
            result.Color.Should().Be("#ff0000");
            _mockRepository.Verify(x => x.InsertAsync(It.IsAny<Tag>()), Times.Once);
        }

        /// <summary>
        /// Tests that GetOrCreateTagAsync applies default color when not provided.
        /// </summary>
        [Fact]
        public async Task GetOrCreateTagAsync_WithoutColor_ShouldApplyDefaultColor()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetByNameAsync("NoColor"))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.ExistsByNameAsync("NoColor"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            var result = await _service.GetOrCreateTagAsync("NoColor");

            // Assert
            result.Color.Should().Be("#3498db");
        }

        #endregion

        #region TogglePinStatusAsync Tests
        /// <summary>
        /// Tests that TogglePinStatusAsync toggles pin status and raises event.
        /// </summary>
        [Fact]
        public async Task TogglePinStatusAsync_WhenCalled_ShouldToggleStatusAndRaiseEvent()
        {
            // Arrange
            var tag = CreateTestTag(1, "TestTag");
            tag.IsPinned = false;

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(tag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("TestTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Usar el método helper para crear el servicio configurado
            var service = CreateConfiguredTagService(tag);

            TagChangedEventArgs? capturedArgs = null;
            service.OnTagChanged += (sender, args) => capturedArgs = args;

            // Act - Toggle from false to true
            await service.TogglePinStatusAsync(1);

            // Assert
            tag.IsPinned.Should().BeTrue("Should toggle from false to true");
            capturedArgs.Should().NotBeNull();
            capturedArgs?.ChangeType.Should().Be(TagChangeType.Pinned);

            // Verify update was called
            _mockRepository.Verify(x => x.UpdateAsync(It.Is<Tag>(t => t.IsPinned)), Times.Once);
        }
        
        /// <summary>
        /// Creates a TagService instance with properly configured mocks and initialized cache.
        /// </summary>
        /// <param name="initialTags">Optional tags to pre-load into cache.</param>
        /// <returns>A properly configured TagService instance.</returns>
        private TagService CreateConfiguredTagService(params Tag[] initialTags)
        {
            SetupCompleteMockLogger();

            var service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Pre-initialize cache if needed
            if (initialTags.Any())
            {
                InitializeCacheWithTags(service, initialTags);
            }
            else
            {
                MarkCacheAsInitialized(service);
            }

            return service;
        }

        /// <summary>
        /// Marks the cache as initialized to avoid EnsureCacheInitializedAsync calls.
        /// </summary>
        private void MarkCacheAsInitialized(TagService service)
        {
            var initField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(service, true);
        }

        /// <summary>
        /// Initializes cache with specific tags and marks it as initialized.
        /// </summary>
        private void InitializeCacheWithTags(TagService service, params Tag[] tags)
        {
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(service) as ConcurrentDictionary<int, Tag>;

            foreach (var tag in tags)
            {
                if (cache != null)
                {
                    cache[tag.Id] = tag;
                }
            }

            MarkCacheAsInitialized(service);
        }

        /// <summary>
        /// Tests that TogglePinStatusAsync raises unpinned event when toggling from true to false.
        /// </summary>
        [Fact]
        public async Task TogglePinStatusAsync_WhenUnpinning_ShouldRaiseUnpinnedEvent()
        {
            // Arrange
            var tag = CreateTestTag(1, "TestTag");
            tag.IsPinned = true;

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(tag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("TestTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Configurar logger completo
            SetupCompleteMockLogger();

            var service = new TagService(_mockRepository.Object, _mockLogger.Object);

            // Inicializar cache con el tag y marcarlo como inicializado
            InitializeCacheWithTags(service, tag);

            TagChangedEventArgs? capturedArgs = null;
            service.OnTagChanged += (sender, args) => capturedArgs = args;

            // Act - Toggle from true to false
            await service.TogglePinStatusAsync(1);

            // Assert
            tag.IsPinned.Should().BeFalse("Should toggle from true to false");
            capturedArgs?.ChangeType.Should().Be(TagChangeType.Unpinned);

            // Verificar que se llamó a UpdateAsync
            _mockRepository.Verify(x => x.UpdateAsync(It.Is<Tag>(t => !t.IsPinned)), Times.Once);
        }

        #endregion

        #region ToggleVisibilityAsync Tests

        /// <summary>
        /// Tests that ToggleVisibilityAsync toggles visibility and raises event.
        /// </summary>
        [Fact]
        public async Task ToggleVisibilityAsync_WhenCalled_ShouldToggleVisibilityAndRaiseEvent()
        {
            // Arrange
            var tag = CreateTestTag(1, "TestTag");
            tag.IsVisible = true;

            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(tag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("TestTag"))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            var service = CreateConfiguredTagService(tag);

            TagChangedEventArgs? capturedArgs = null;
            service.OnTagChanged += (sender, args) => capturedArgs = args;

            // Act
            await service.ToggleVisibilityAsync(1);

            // Assert
            tag.IsVisible.Should().BeFalse("Should toggle from true to false");
            capturedArgs.Should().NotBeNull();
            capturedArgs?.ChangeType.Should().Be(TagChangeType.VisibilityChanged);
        }

        #endregion

        #region UpdateTagUsageAsync Tests

        /// <summary>
        /// Tests that UpdateTagUsageAsync updates usage count and timestamp.
        /// </summary>
        [Fact]
        public async Task UpdateTagUsageAsync_WhenCalled_ShouldUpdateUsageAndCache()
        {
            // Arrange
            var tag = CreateTestTag(1, "TestTag");
            var initialUsageCount = tag.UsageCount;
            var initialLastUsed = tag.LastUsedAt;

            // Initialize cache
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(_service) as ConcurrentDictionary<int, Tag>;
            if (cache != null)
            {
                cache[1] = tag;
            }

            // Act
            await _service.UpdateTagUsageAsync(1);

            // Assert
            tag.UsageCount.Should().Be(initialUsageCount + 1);
            tag.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            _mockLogger.Verify(x => x.Debug(
                "Tag usage updated: {TagId}",
                1),
                Times.Once);
        }

        #endregion

        #region SearchTagsAsync Tests

        /// <summary>
        /// Tests that SearchTagsAsync returns search results.
        /// </summary>
        [Fact]
        public async Task SearchTagsAsync_WithSearchTerm_ShouldReturnMatchingTags()
        {
            // Arrange
            var searchTerm = "test";
            var expectedTags = new List<Tag>
            {
                CreateTestTag(1, "TestTag1"),
                CreateTestTag(2, "AnotherTest")
            };

            _mockRepository.Setup(x => x.SearchByNameAsync(searchTerm))
                          .ReturnsAsync(expectedTags);

            // Act
            var result = await _service.SearchTagsAsync(searchTerm);

            // Assert
            result.Should().BeEquivalentTo(expectedTags);
        }

        /// <summary>
        /// Tests that SearchTagsAsync returns all tags when search term is empty.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task SearchTagsAsync_WithEmptySearchTerm_ShouldReturnAllTags(string searchTerm)
        {
            // Arrange
            var allTags = CreateTestTagsList(3);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(allTags);

            // Act
            var result = await _service.SearchTagsAsync(searchTerm);

            // Assert
            result.Should().BeEquivalentTo(allTags);
        }

        #endregion

        #region ImportTagsAsync Tests

        /// <summary>
        /// Tests that ImportTagsAsync imports new tags successfully.
        /// </summary>
        [Fact]
        public async Task ImportTagsAsync_WithNewTags_ShouldCreateTags()
        {
            // Arrange
            var tagsToImport = new List<Tag>
            {
                new Tag { Name = "NewTag1", Color = "#ff0000" },
                new Tag { Name = "NewTag2", Color = "#00ff00" }
            };

            _mockRepository.Setup(x => x.GetByNameAsync(It.IsAny<string>()))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.ExistsByNameAsync(It.IsAny<string>()))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            var result = await _service.ImportTagsAsync(tagsToImport);

            // Assert
            result.Should().HaveCount(2);
            _mockRepository.Verify(x => x.InsertAsync(It.IsAny<Tag>()), Times.Exactly(2));
        }

        /// <summary>
        /// Tests that ImportTagsAsync updates existing tags.
        /// </summary>
        [Fact]
        public async Task ImportTagsAsync_WithExistingTags_ShouldUpdateTags()
        {
            // Arrange
            var existingTag = CreateTestTag(1, "ExistingTag");
            var updatedTag = new Tag { Name = "ExistingTag", Color = "#ff0000", Description = "Updated" };
            var tagsToImport = new List<Tag> { updatedTag };

            // Configurar repositorio
            _mockRepository.Setup(x => x.GetByNameAsync("ExistingTag"))
                          .ReturnsAsync(existingTag);

            _mockRepository.Setup(x => x.ExistsByNameAsync("ExistingTag"))
                          .ReturnsAsync(true);

            _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1)
                          .Callback<Tag>(t =>
                          {
                              existingTag.Color = t.Color;
                              existingTag.Description = t.Description;
                          });

            // ✅ Mock para GetByIdAsync (necesario para UpdateTagAsync)
            _mockRepository.Setup(x => x.GetByIdAsync(1))
                          .ReturnsAsync(existingTag);

            SetupCompleteMockLogger();

            var service = new TagService(_mockRepository.Object, _mockLogger.Object);
            InitializeCacheWithTag(service, existingTag);

            // Act
            var result = await service.ImportTagsAsync(tagsToImport);

            // Assert
            result.Should().HaveCount(1);
            result[0].Description.Should().Be("Updated");
            result[0].Color.Should().Be("#ff0000");
            result[0].Id.Should().Be(1); // Verificar que mantiene el ID original

            _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<Tag>()), Times.Once);
        }

        /// <summary>
        /// Helper method to setup complete mock logger for all operations.
        /// </summary>
        private void SetupCompleteMockLogger()
        {
            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();

            _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();

            _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()))
                      .Verifiable();
        }

        /// <summary>
        /// Helper method to initialize cache with a specific tag.
        /// </summary>
        private void InitializeCacheWithTag(TagService service, Tag tag)
        {
            // Marcar cache como inicializado
            var initField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initField?.SetValue(service, true);

            // Agregar tag al cache
            var cacheField = typeof(TagService).GetField("_tagCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cache = cacheField?.GetValue(service) as ConcurrentDictionary<int, Tag>;
            if (cache != null)
            {
                cache[tag.Id] = tag;
            }
        }

        /// <summary>
        /// Tests that ImportTagsAsync handles errors gracefully.
        /// </summary>
        [Fact]
        public async Task ImportTagsAsync_WithPartialFailures_ShouldContinueAndLogErrors()
        {
            // Arrange
            var tagsToImport = new List<Tag>
            {
                new Tag { Name = "GoodTag1" },
                new Tag { Name = "" }, // Invalid - empty name
                new Tag { Name = "GoodTag2" }
            };

            _mockRepository.Setup(x => x.GetByNameAsync("GoodTag1"))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.GetByNameAsync("GoodTag2"))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.ExistsByNameAsync(It.Is<string>(n => !string.IsNullOrEmpty(n))))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            var result = await _service.ImportTagsAsync(tagsToImport);

            // Assert
            result.Should().HaveCount(2, "Should import only valid tags");
            _mockLogger.Verify(x => x.Warning(
                "Tag import completed with {ErrorCount} errors",
                It.IsAny<int>()),
                Times.Once);
        }

        #endregion

        #region GetOrCreateTagsAsync Tests

        /// <summary>
        /// Tests that GetOrCreateTagsAsync returns or creates multiple tags.
        /// </summary>
        [Fact]
        public async Task GetOrCreateTagsAsync_WithMultipleNames_ShouldReturnOrCreateTags()
        {
            // Arrange
            var tagNames = new List<string> { "Tag1", "Tag2", "Tag1" }; // Duplicate

            _mockRepository.Setup(x => x.GetByNameAsync(It.IsAny<string>()))
                          .ReturnsAsync((Tag?)null);

            _mockRepository.Setup(x => x.ExistsByNameAsync(It.IsAny<string>()))
                          .ReturnsAsync(false);

            _mockRepository.Setup(x => x.InsertAsync(It.IsAny<Tag>()))
                          .ReturnsAsync(1);

            // Act
            var result = await _service.GetOrCreateTagsAsync(tagNames);

            // Assert
            result.Should().HaveCount(2, "Should deduplicate tag names");
            result.Select(t => t.Name).Should().Contain(new[] { "Tag1", "Tag2" });
        }

        #endregion

        #region Statistics and Utility Tests

        /// <summary>
        /// Tests that GetTotalTagCountAsync returns correct count.
        /// </summary>
        [Fact]
        public async Task GetTotalTagCountAsync_WhenCalled_ShouldReturnCacheCount()
        {
            // Arrange
            var tags = CreateTestTagsList(5);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tags);

            // Initialize cache
            await _service.GetAllTagsAsync();

            // Act
            var result = await _service.GetTotalTagCountAsync();

            // Assert
            result.Should().Be(5);
        }

        /// <summary>
        /// Tests that GetTagUsageStatisticsAsync returns usage statistics.
        /// </summary>
        [Fact]
        public async Task GetTagUsageStatisticsAsync_WhenCalled_ShouldReturnUsageDictionary()
        {
            // Arrange
            var tag1 = CreateTestTag(1, "Tag1");
            tag1.UsageCount = 10;
            var tag2 = CreateTestTag(2, "Tag2");
            tag2.UsageCount = 5;
            var popularTags = new List<Tag> { tag1, tag2 };

            _mockRepository.Setup(x => x.GetPopularTagsAsync(20))
                          .ReturnsAsync(popularTags);

            // Act
            var result = await _service.GetTagUsageStatisticsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainKey("Tag1").WhoseValue.Should().Be(10);
            result.Should().ContainKey("Tag2").WhoseValue.Should().Be(5);
        }

        /// <summary>
        /// Tests that GetSuggestedTagNamesAsync returns matching tag names.
        /// </summary>
        [Fact]
        public async Task GetSuggestedTagNamesAsync_WithPartialName_ShouldReturnSuggestions()
        {
            // Arrange
            var searchTerm = "pro";
            var matchingTags = new List<Tag>
            {
                CreateTestTag(1, "Programming"),
                CreateTestTag(2, "Productivity")
            };

            _mockRepository.Setup(x => x.SearchByNameAsync(searchTerm))
                          .ReturnsAsync(matchingTags);

            // Act
            var result = await _service.GetSuggestedTagNamesAsync(searchTerm);

            // Assert
            result.Should().Contain(new[] { "Programming", "Productivity" });
        }

        #endregion

        #region Cache Tests

        /// <summary>
        /// Tests that cache initialization is thread-safe.
        /// </summary>
        [Fact]
        public async Task EnsureCacheInitializedAsync_WhenCalledConcurrently_ShouldInitializeOnce()
        {
            // Arrange
            var tags = CreateTestTagsList(3);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(tags)
                          .Callback(() => Thread.Sleep(100)); // Simulate delay

            // Act - Call multiple times concurrently
            var tasks = new List<Task<List<Tag>>>();
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(_service.GetAllTagsAsync());
            }

            await Task.WhenAll(tasks);

            // Assert
            _mockRepository.Verify(x => x.GetAllAsync(), Times.Once,
                "Repository should be called only once despite concurrent access");
        }

        /// <summary>
        /// Tests that cache is cleared on error during initialization.
        /// </summary>
        [Fact]
        public async Task EnsureCacheInitializedAsync_WhenRepositoryThrows_ShouldClearCacheAndThrow()
        {
            // Arrange
            _mockRepository.Setup(x => x.GetAllAsync())
                          .ThrowsAsync(new InvalidOperationException("DB Error"));

            // Act
            Func<Task> act = async () => await _service.GetAllTagsAsync();

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();

            // Verify cache remains uninitialized
            var cacheField = typeof(TagService).GetField("_isCacheInitialized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isInitialized = (bool?)cacheField?.GetValue(_service);

            isInitialized.Should().BeFalse();
        }

        #endregion

        #region Edge Cases Tests

        /// <summary>
        /// Tests that TagExistsAsync returns correct existence check.
        /// </summary>
        [Theory]
        [InlineData("Existing", true)]
        [InlineData("NonExisting", false)]
        public async Task TagExistsAsync_WithVariousNames_ShouldReturnCorrectResult(string tagName, bool expected)
        {
            // Arrange
            _mockRepository.Setup(x => x.ExistsByNameAsync(tagName))
                          .ReturnsAsync(expected);

            // Act
            var result = await _service.TagExistsAsync(tagName);

            // Assert
            result.Should().Be(expected);
        }

        /// <summary>
        /// Tests that service handles null parameters gracefully in search methods.
        /// </summary>
        [Fact]
        public async Task SearchMethods_WithNullParameters_ShouldHandleGracefully()
        {
            // Arrange
            var allTags = CreateTestTagsList(2);

            _mockRepository.Setup(x => x.GetAllAsync())
                          .ReturnsAsync(allTags);

            // Act & Assert - Should not throw
            Func<Task> act = async () => await _service.SearchTagsAsync(null!);
            await act.Should().NotThrowAsync();
        }

        /// <summary>
        /// Tests that GetPopularTagsAsync respects limit parameter.
        /// </summary>
        [Fact]
        public async Task GetPopularTagsAsync_WithLimit_ShouldReturnLimitedResults()
        {
            // Arrange
            var limit = 5;
            var manyTags = CreateTestTagsList(10);

            _mockRepository.Setup(x => x.GetPopularTagsAsync(limit))
                          .ReturnsAsync(manyTags.Take(limit).ToList());

            // Act
            var result = await _service.GetPopularTagsAsync(limit);

            // Assert
            result.Should().HaveCount(limit);
        }

        #endregion
    }

}