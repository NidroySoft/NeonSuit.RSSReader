using FluentAssertions;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    /// <summary>
    /// Integration tests for TagService following the same pattern as other integration tests.
    /// Uses DatabaseFixture and ServiceFactory with shared database context.
    /// </summary>
    [Collection("Integration Tests")]
    public class TagServiceIntegrationTests : IAsyncLifetime
    {
        private readonly DatabaseFixture _dbFixture;
        private readonly ITestOutputHelper _output;
        private readonly ServiceFactory _factory;
        private ITagService _tagService = null!;
        private List<Tag> _initialTags = null!;

        public TagServiceIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
        {
            _dbFixture = dbFixture;
            _output = output;
            _factory = new ServiceFactory(_dbFixture);
        }

        public async Task InitializeAsync()
        {
            // ✅ Usar el mismo patrón que ArticleIntegrationTests, CategoryIntegrationTests, etc.
            _tagService = _factory.CreateTagService(); // NO Fresh, usa el compartido

            await SetupTestDataAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task SetupTestDataAsync()
        {
            var tags = new[]
            {
                new Tag { Name = "Tecnología", Color = "#3498db", UsageCount = 5, IsPinned = true, IsVisible = true },
                new Tag { Name = "Noticias", Color = "#e74c3c", UsageCount = 3, IsVisible = true },
                new Tag { Name = "Favoritos", Color = "#f1c40f", IsVisible = false }
            };

            _initialTags = new List<Tag>();

            foreach (var tag in tags)
            {
                var id = await _tagService.CreateTagAsync(tag);
                tag.Id = id;
                _initialTags.Add(tag);
                _output.WriteLine($"Created test tag: {tag.Name} (ID: {id})");
            }

            var allTags = await _tagService.GetAllTagsAsync();
            _output.WriteLine($"Setup completed with {allTags.Count} initial tags");
        }

        #region GetTagAsync Tests

        [Fact]
        public async Task GetTagAsync_WithExistingId_ShouldReturnTag()
        {
            // Arrange
            var expectedTag = _initialTags.First(t => t.Name == "Tecnología");

            // Act
            var result = await _tagService.GetTagAsync(expectedTag.Id);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(expectedTag.Id);
            result.Name.Should().Be("Tecnología");
            result.Color.Should().Be("#3498db");
            result.IsPinned.Should().BeTrue();
            result.UsageCount.Should().Be(5);
            result.IsVisible.Should().BeTrue();
        }

        [Fact]
        public async Task GetTagAsync_WithNonExistentId_ShouldThrowKeyNotFoundException()
        {
            // Arrange
            var nonExistentId = 9999;

            // Act
            Func<Task> act = async () => await _tagService.GetTagAsync(nonExistentId);

            // Assert
            await act.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"Tag with ID {nonExistentId} not found.");
        }

        #endregion

        #region GetTagByNameAsync Tests

        [Fact]
        public async Task GetTagByNameAsync_WithExistingName_ShouldReturnTag()
        {
            // Arrange
            var expectedTag = _initialTags.First(t => t.Name == "Noticias");

            // Act
            var result = await _tagService.GetTagByNameAsync("Noticias");

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(expectedTag.Id);
            result.Name.Should().Be("Noticias");
            result.Color.Should().Be("#e74c3c");
        }

        [Fact]
        public async Task GetTagByNameAsync_WithNonExistentName_ShouldReturnNull()
        {
            // Act
            var result = await _tagService.GetTagByNameAsync("NonExistentTag");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetTagByNameAsync_WithDifferentCase_ShouldFindTag()
        {
            // Act
            var result = await _tagService.GetTagByNameAsync("teCNologíA");

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Tecnología");
        }

        #endregion

        #region GetAllTagsAsync Tests

        [Fact]
        public async Task GetAllTagsAsync_WhenCalled_ShouldReturnAllTagsOrderedByName()
        {
            // Act
            var result = await _tagService.GetAllTagsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result.Select(t => t.Name).Should().BeInAscendingOrder();

            result[0].Name.Should().Be("Favoritos");
            result[1].Name.Should().Be("Noticias");
            result[2].Name.Should().Be("Tecnología");
        }

        #endregion

        #region CreateTagAsync Tests

        [Fact]
        public async Task CreateTagAsync_WithValidTag_ShouldPersistAndRaiseEvent()
        {
            // Arrange
            var eventRaised = false;
            TagChangedEventArgs? eventArgs = null;

            _tagService.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            var newTag = new Tag
            {
                Name = "Proyectos Personales",
                Description = "Artículos sobre proyectos personales",
                Color = "#27ae60",
                Icon = "user-secret",
                IsVisible = true
            };

            // Act
            var tagId = await _tagService.CreateTagAsync(newTag);

            // Assert
            tagId.Should().BeGreaterThan(0);

            var tagFromDb = await _tagService.GetTagAsync(tagId);
            tagFromDb.Should().NotBeNull();
            tagFromDb!.Name.Should().Be("Proyectos Personales");
            tagFromDb.Description.Should().Be("Artículos sobre proyectos personales");
            tagFromDb.Color.Should().Be("#27ae60");
            tagFromDb.Icon.Should().Be("user-secret");
            tagFromDb.IsVisible.Should().BeTrue();
            tagFromDb.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            tagFromDb.UsageCount.Should().Be(0);
            tagFromDb.IsPinned.Should().BeFalse();

            // Verify appears in all tags list
            var allTags = await _tagService.GetAllTagsAsync();
            allTags.Should().Contain(t => t.Id == tagId);
            allTags.Should().HaveCount(4); // 3 initial + 1 new

            // Verify event was raised
            eventRaised.Should().BeTrue();
            eventArgs.Should().NotBeNull();
            eventArgs!.TagId.Should().Be(tagId);
            eventArgs.TagName.Should().Be("Proyectos Personales");
            eventArgs.ChangeType.Should().Be(TagChangeType.Created);
        }

        [Fact]
        public async Task CreateTagAsync_WithoutColor_ShouldApplyDefaultColor()
        {
            // Arrange
            var newTag = new Tag { Name = "NoColorTag" };

            // Act
            var tagId = await _tagService.CreateTagAsync(newTag);

            // Assert
            var savedTag = await _tagService.GetTagAsync(tagId);
            savedTag.Color.Should().Be("#3498db");
        }

        [Fact]
        public async Task CreateTagAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var duplicateTag = new Tag { Name = "Tecnología" };

            // Act
            Func<Task> act = async () => await _tagService.CreateTagAsync(duplicateTag);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag 'Tecnología' already exists.");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public async Task CreateTagAsync_WithInvalidName_ShouldThrowArgumentException(string? invalidName)
        {
            // Arrange
            var tag = new Tag { Name = invalidName ?? string.Empty };

            // Act
            Func<Task> act = async () => await _tagService.CreateTagAsync(tag);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("Tag name cannot be empty.");
        }

        #endregion

        #region UpdateTagAsync Tests

        [Fact]
        public async Task UpdateTagAsync_WithValidChanges_ShouldUpdateAndRaiseEvent()
        {
            // Arrange
            var tagToUpdate = _initialTags.First(t => t.Name == "Noticias");
            var updatedTag = new Tag
            {
                Id = tagToUpdate.Id,
                Name = "Noticias Actualizadas",
                Description = "Descripción actualizada",
                Color = "#ff0000",
                Icon = "new-icon",
                IsPinned = true,
                IsVisible = false
            };

            var eventRaised = false;
            TagChangedEventArgs? eventArgs = null;

            _tagService.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act
            await _tagService.UpdateTagAsync(updatedTag);

            // Assert
            var savedTag = await _tagService.GetTagAsync(tagToUpdate.Id);
            savedTag.Name.Should().Be("Noticias Actualizadas");
            savedTag.Description.Should().Be("Descripción actualizada");
            savedTag.Color.Should().Be("#ff0000");
            savedTag.Icon.Should().Be("new-icon");
            savedTag.IsPinned.Should().BeTrue();
            savedTag.IsVisible.Should().BeFalse();
            savedTag.CreatedAt.Should().Be(tagToUpdate.CreatedAt);

            eventRaised.Should().BeTrue();
            eventArgs!.TagId.Should().Be(tagToUpdate.Id);
            eventArgs.TagName.Should().Be("Noticias Actualizadas");
            eventArgs.ChangeType.Should().Be(TagChangeType.Updated);
        }

        [Fact]
        public async Task UpdateTagAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var tag2 = _initialTags.First(t => t.Name == "Noticias");
            var updateTag = new Tag
            {
                Id = tag2.Id,
                Name = "Tecnología"
            };

            // Act
            Func<Task> act = async () => await _tagService.UpdateTagAsync(updateTag);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Tag 'Tecnología' already exists.");
        }

        [Fact]
        public async Task UpdateTagAsync_WithSameName_ShouldUpdateSuccessfully()
        {
            // Arrange
            var tagToUpdate = _initialTags.First(t => t.Name == "Favoritos");
            var updatedTag = new Tag
            {
                Id = tagToUpdate.Id,
                Name = "Favoritos",
                Color = "#000000"
            };

            // Act
            await _tagService.UpdateTagAsync(updatedTag);

            // Assert
            var savedTag = await _tagService.GetTagAsync(tagToUpdate.Id);
            savedTag.Color.Should().Be("#000000");
            savedTag.Name.Should().Be("Favoritos");
        }

        #endregion

        #region DeleteTagAsync Tests

        [Fact]
        public async Task DeleteTagAsync_WithExistingId_ShouldDeleteAndRaiseEvent()
        {
            // Arrange
            var tagToDelete = _initialTags.First(t => t.Name == "Favoritos");
            var eventRaised = false;
            TagChangedEventArgs? eventArgs = null;

            _tagService.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act
            await _tagService.DeleteTagAsync(tagToDelete.Id);

            // Assert
            Func<Task> act = async () => await _tagService.GetTagAsync(tagToDelete.Id);
            await act.Should().ThrowAsync<KeyNotFoundException>();

            var allTags = await _tagService.GetAllTagsAsync();
            allTags.Should().HaveCount(2);
            allTags.Should().NotContain(t => t.Id == tagToDelete.Id);

            eventRaised.Should().BeTrue();
            eventArgs!.TagId.Should().Be(tagToDelete.Id);
            eventArgs.TagName.Should().Be("Favoritos");
            eventArgs.ChangeType.Should().Be(TagChangeType.Deleted);
        }

        #endregion

        #region GetOrCreateTagAsync Tests

        [Fact]
        public async Task GetOrCreateTagAsync_WithExistingName_ShouldReturnExistingTag()
        {
            // Act
            var result = await _tagService.GetOrCreateTagAsync("Tecnología");

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(_initialTags.First(t => t.Name == "Tecnología").Id);
            result.Name.Should().Be("Tecnología");
        }

        [Fact]
        public async Task GetOrCreateTagAsync_WithNewName_ShouldCreateAndReturnNewTag()
        {
            // Arrange
            var newTagName = "NuevoTag";

            // Act
            var result = await _tagService.GetOrCreateTagAsync(newTagName, "#ff00ff");

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Name.Should().Be(newTagName);
            result.Color.Should().Be("#ff00ff");

            var allTags = await _tagService.GetAllTagsAsync();
            allTags.Should().Contain(t => t.Name == newTagName);
            allTags.Should().HaveCount(4);
        }

        #endregion

        #region TogglePinStatusAsync Tests

        [Fact]
        public async Task TogglePinStatusAsync_WhenCalled_ShouldToggleAndRaiseEvent()
        {
            // Arrange
            var tag = _initialTags.First(t => t.Name == "Noticias");
            var initialPinStatus = tag.IsPinned;
            var eventRaised = false;
            TagChangedEventArgs? eventArgs = null;

            _tagService.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act
            await _tagService.TogglePinStatusAsync(tag.Id);

            // Assert
            var toggledTag = await _tagService.GetTagAsync(tag.Id);
            toggledTag.IsPinned.Should().Be(!initialPinStatus);
            eventRaised.Should().BeTrue();
            eventArgs!.ChangeType.Should().Be(initialPinStatus ? TagChangeType.Unpinned : TagChangeType.Pinned);
        }

        #endregion

        #region ToggleVisibilityAsync Tests

        [Fact]
        public async Task ToggleVisibilityAsync_WhenCalled_ShouldToggleAndRaiseEvent()
        {
            // Arrange
            var tag = _initialTags.First(t => t.Name == "Favoritos");
            var initialVisibility = tag.IsVisible;
            var eventRaised = false;
            TagChangedEventArgs? eventArgs = null;

            _tagService.OnTagChanged += (sender, args) =>
            {
                eventRaised = true;
                eventArgs = args;
            };

            // Act
            await _tagService.ToggleVisibilityAsync(tag.Id);

            // Assert
            var toggledTag = await _tagService.GetTagAsync(tag.Id);
            toggledTag.IsVisible.Should().Be(!initialVisibility);
            eventRaised.Should().BeTrue();
            eventArgs!.ChangeType.Should().Be(TagChangeType.VisibilityChanged);
        }

        #endregion

        #region UpdateTagUsageAsync Tests

        [Fact]
        public async Task UpdateTagUsageAsync_WhenCalled_ShouldIncrementUsageAndUpdateLastUsed()
        {
            // Arrange
            var tag = _initialTags.First(t => t.Name == "Tecnología");
            var initialUsage = tag.UsageCount;

            // Act
            await _tagService.UpdateTagUsageAsync(tag.Id);

            // Assert
            var updatedTag = await _tagService.GetTagAsync(tag.Id);
            updatedTag.UsageCount.Should().Be(initialUsage + 1);
            updatedTag.LastUsedAt.Should().NotBeNull();
            updatedTag.LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        #endregion

        #region SearchTagsAsync Tests

        [Fact]
        public async Task SearchTagsAsync_WithSearchTerm_ShouldReturnMatchingTags()
        {
            // Act
            var results = await _tagService.SearchTagsAsync("tec");

            // Assert
            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Tecnología");
        }

        [Fact]
        public async Task SearchTagsAsync_WithEmptyTerm_ShouldReturnAllTags()
        {
            // Act
            var results1 = await _tagService.SearchTagsAsync("");
            var results2 = await _tagService.SearchTagsAsync(" ");
            var results3 = await _tagService.SearchTagsAsync(null!);

            // Assert
            results1.Should().HaveCount(3);
            results2.Should().HaveCount(3);
            results3.Should().HaveCount(3);
        }

        #endregion

        #region GetPopularTagsAsync Tests

        [Fact]
        public async Task GetPopularTagsAsync_WhenCalled_ShouldReturnVisibleTagsOrderedByUsage()
        {
            // Act
            var results = await _tagService.GetPopularTagsAsync(10);

            // Assert
            results.Should().HaveCount(2); // Solo Tecnología y Noticias (Favoritos es invisible)
            results[0].Name.Should().Be("Tecnología");
            results[1].Name.Should().Be("Noticias");
            results.Should().NotContain(t => t.Name == "Favoritos");
        }

        [Fact]
        public async Task GetPopularTagsAsync_WithLimit_ShouldRespectLimit()
        {
            // Act
            var results = await _tagService.GetPopularTagsAsync(2);

            // Assert
            results.Should().HaveCount(2);
            results[0].Name.Should().Be("Tecnología");
            results[1].Name.Should().Be("Noticias");
        }

        #endregion

        #region GetVisibleTagsAsync Tests

        [Fact]
        public async Task GetVisibleTagsAsync_WhenCalled_ShouldReturnOnlyVisibleTags()
        {
            // Act
            var results = await _tagService.GetVisibleTagsAsync();

            // Assert
            results.Should().HaveCount(2);
            results.Should().NotContain(t => t.Name == "Favoritos");
            results.Select(t => t.Name).Should().BeInAscendingOrder();
        }

        #endregion

        #region GetPinnedTagsAsync Tests

        [Fact]
        public async Task GetPinnedTagsAsync_WhenCalled_ShouldReturnPinnedTags()
        {
            // Act
            var results = await _tagService.GetPinnedTagsAsync();

            // Assert
            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Tecnología");
        }

        #endregion

        #region GetOrCreateTagsAsync Tests

        [Fact]
        public async Task GetOrCreateTagsAsync_WithMixedNames_ShouldReturnOrCreateTags()
        {
            // Arrange
            var tagNames = new[] { "Tecnología", "NuevoTag1", "Noticias", "NuevoTag2" };

            // Act
            var results = await _tagService.GetOrCreateTagsAsync(tagNames);

            // Assert
            results.Should().HaveCount(4);
            results.Should().Contain(t => t.Name == "Tecnología");
            results.Should().Contain(t => t.Name == "Noticias");
            results.Should().Contain(t => t.Name == "NuevoTag1");
            results.Should().Contain(t => t.Name == "NuevoTag2");

            var allTags = await _tagService.GetAllTagsAsync();
            allTags.Should().HaveCount(5);
        }

        [Fact]
        public async Task GetOrCreateTagsAsync_WithDuplicateNames_ShouldDeduplicate()
        {
            // Arrange
            var tagNames = new[] { "Tecnología", "Tecnología", "Noticias", "Noticias" };

            // Act
            var results = await _tagService.GetOrCreateTagsAsync(tagNames);

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(t => t.Name == "Tecnología");
            results.Should().Contain(t => t.Name == "Noticias");
        }

        #endregion

        #region ImportTagsAsync Tests

        [Fact]
        public async Task ImportTagsAsync_WithNewTags_ShouldImportSuccessfully()
        {
            // Arrange
            var tagsToImport = new List<Tag>
            {
                new Tag { Name = "Importado1", Color = "#111111", Description = "Desc1" },
                new Tag { Name = "Importado2", Color = "#222222", Description = "Desc2" }
            };

            // Act
            var imported = await _tagService.ImportTagsAsync(tagsToImport);

            // Assert
            imported.Should().HaveCount(2);

            var allTags = await _tagService.GetAllTagsAsync();
            allTags.Should().HaveCount(5);
            allTags.Should().Contain(t => t.Name == "Importado1");
            allTags.Should().Contain(t => t.Name == "Importado2");
        }

        [Fact]
        public async Task ImportTagsAsync_WithExistingTags_ShouldUpdateThem()
        {
            // Arrange
            var tagsToImport = new List<Tag>
            {
                new Tag { Name = "Tecnología", Color = "#000000", Description = "Updated Description" }
            };

            // Act
            var imported = await _tagService.ImportTagsAsync(tagsToImport);

            // Assert
            imported.Should().HaveCount(1);

            var updatedTag = await _tagService.GetTagByNameAsync("Tecnología");
            updatedTag!.Color.Should().Be("#000000");
            updatedTag.Description.Should().Be("Updated Description");
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task GetTotalTagCountAsync_WhenCalled_ShouldReturnCorrectCount()
        {
            // Act
            var count = await _tagService.GetTotalTagCountAsync();

            // Assert
            count.Should().Be(3);
        }

        [Fact]
        public async Task GetTagUsageStatisticsAsync_WhenCalled_ShouldReturnUsageDictionaryForVisibleTags()
        {
            // Act
            var stats = await _tagService.GetTagUsageStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.Should().ContainKey("Tecnología").WhoseValue.Should().Be(5);
            stats.Should().ContainKey("Noticias").WhoseValue.Should().Be(3);
            stats.Should().NotContainKey("Favoritos"); // ✅ No debe incluir invisibles
            stats.Count.Should().Be(2);
        }

        [Fact]
        public async Task GetSuggestedTagNamesAsync_WithPartialName_ShouldReturnSuggestions()
        {
            // Act
            var suggestions = await _tagService.GetSuggestedTagNamesAsync("tec");

            // Assert
            suggestions.Should().Contain("Tecnología");
            suggestions.Should().HaveCount(1);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task TagExistsAsync_WithExistingName_ShouldReturnTrue()
        {
            // Act
            var exists = await _tagService.TagExistsAsync("Tecnología");

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task TagExistsAsync_WithNonExistingName_ShouldReturnFalse()
        {
            // Act
            var exists = await _tagService.TagExistsAsync("NonExistent");

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task GetTagsByColorAsync_WhenCalled_ShouldReturnTagsWithMatchingColor()
        {
            // Act
            var results = await _tagService.GetTagsByColorAsync("#3498db");

            // Assert
            results.Should().HaveCount(1);
            results.First().Name.Should().Be("Tecnología");
        }

        #endregion
    }
}