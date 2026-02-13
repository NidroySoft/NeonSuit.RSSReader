using FluentAssertions;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;

namespace NeonSuit.RSSReader.Tests.Integration.Services
{
    [Collection("Integration Tests")]
    public class CategoryIntegrationTests : IAsyncLifetime
    {
        private DatabaseFixture _dbFixture;
        private ServiceFactory _factory = null!;
        private ICategoryService _categoryService = null!;
        private IFeedService _feedService = null!;

        public CategoryIntegrationTests(DatabaseFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task InitializeAsync()
        {
            _factory = new ServiceFactory(_dbFixture);
            _categoryService = _factory.CreateCategoryService();
            _feedService = _factory.CreateFeedService();
            await Task.CompletedTask;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private const string TestFeed = "http://feeds.arstechnica.com/arstechnica/index";

        [Fact]
        public async Task GetAllCategoriesAsync_ShouldReturnAllCategoriesWithStats()
        {
            // Arrange
            await _categoryService.CreateCategoryAsync("Test Category 1");
            await _categoryService.CreateCategoryAsync("Test Category 2");

            // Act
            var categories = await _categoryService.GetAllCategoriesAsync();

            // Assert
            categories.Should().HaveCountGreaterThanOrEqualTo(2);
            categories.All(c => c.FeedCount >= 0).Should().BeTrue();
            categories.All(c => c.UnreadCount >= 0).Should().BeTrue();
        }

        [Fact]
        public async Task GetCategoryByIdAsync_WithExistingCategory_ShouldReturnCategory()
        {
            // Arrange
            var created = await _categoryService.CreateCategoryAsync("Find Me");

            // Act
            var category = await _categoryService.GetCategoryByIdAsync(created.Id);

            // Assert
            category.Should().NotBeNull();
            category!.Id.Should().Be(created.Id);
            category.Name.Should().Be("Find Me");
            category.FeedCount.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public async Task GetCategoryByIdAsync_WithNonExistingId_ShouldReturnNull()
        {
            // Act
            var category = await _categoryService.GetCategoryByIdAsync(99999);

            // Assert
            category.Should().BeNull();
        }

        [Fact]
        public async Task CreateCategoryAsync_WithValidName_ShouldCreateCategory()
        {
            // Act
            var category = await _categoryService.CreateCategoryAsync("New Category", "#ff0000", "Description");

            // Assert
            category.Should().NotBeNull();
            category.Id.Should().BeGreaterThan(0);
            category.Name.Should().Be("New Category");
            category.Color.Should().Be("#ff0000");
        }

        [Fact]
        public async Task CreateCategoryAsync_WithEmptyName_ShouldThrowArgumentException()
        {
            // Act
            Func<Task> act = () => _categoryService.CreateCategoryAsync("");

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task UpdateCategoryAsync_WithValidData_ShouldUpdateCategory()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("Original Name");
            category.Name = "Updated Name";
            category.Color = "#00ff00";

            // Act
            var result = await _categoryService.UpdateCategoryAsync(category);
            var updated = await _categoryService.GetCategoryByIdAsync(category.Id);

            // Assert
            result.Should().BeTrue();
            updated!.Name.Should().Be("Updated Name");
            updated.Color.Should().Be("#00ff00");
        }

        [Fact]
        public async Task DeleteCategoryAsync_WithExistingCategory_ShouldDeleteAndReturnTrue()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("To Delete");

            // Act
            var result = await _categoryService.DeleteCategoryAsync(category.Id);
            var deleted = await _categoryService.GetCategoryByIdAsync(category.Id);

            // Assert
            result.Should().BeTrue();
            deleted.Should().BeNull();
        }

        [Fact]
        public async Task DeleteCategoryAsync_WithNonExistingId_ShouldReturnFalse()
        {
            // Act
            var result = await _categoryService.DeleteCategoryAsync(99999);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task ReorderCategoriesAsync_ShouldReorderCategories()
        {
            // Arrange
            var cat1 = await _categoryService.CreateCategoryAsync("Category A");
            var cat2 = await _categoryService.CreateCategoryAsync("Category B");
            var cat3 = await _categoryService.CreateCategoryAsync("Category C");
            var newOrder = new List<int> { cat3.Id, cat1.Id, cat2.Id };

            // Act
            var result = await _categoryService.ReorderCategoriesAsync(newOrder);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ReorderCategoriesAsync_WithEmptyList_ShouldReturnFalse()
        {
            // Act
            var result = await _categoryService.ReorderCategoriesAsync(new List<int>());

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetFeedCountsAsync_ShouldReturnCountsByCategory()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("With Feeds");
            await _feedService.AddFeedAsync(TestFeed, category.Id);

            // Act
            var counts = await _categoryService.GetFeedCountsAsync();

            // Assert
            counts.Should().ContainKey(category.Id);
            counts[category.Id].Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetUnreadCountsAsync_ShouldReturnUnreadCountsByCategory()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("Unread Test");
            await _feedService.AddFeedAsync(TestFeed, category.Id);

            // Act
            var counts = await _categoryService.GetUnreadCountsAsync();

            // Assert
            counts.Should().ContainKey(category.Id);
        }

        [Fact]
        public async Task CategoryExistsByNameAsync_WithExistingName_ShouldReturnTrue()
        {
            // Arrange
            await _categoryService.CreateCategoryAsync("Existing Name");

            // Act
            var exists = await _categoryService.CategoryExistsByNameAsync("Existing Name");

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public async Task CategoryExistsByNameAsync_WithNonExistingName_ShouldReturnFalse()
        {
            // Act
            var exists = await _categoryService.CategoryExistsByNameAsync("Non Existing Name 12345");

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public async Task GetCategoryTreeAsync_ShouldReturnHierarchicalStructure()
        {
            // Arrange
            var parent = await _categoryService.CreateCategoryAsync("Parent");
            // Nota: Para crear categorías hijas necesitarías soporte en tu repositorio
            // Este test asume que puedes establecer ParentCategoryId

            // Act
            var tree = await _categoryService.GetCategoryTreeAsync();

            // Assert
            tree.Should().NotBeNull();
            // Las categorías raíz no tienen padre
            tree.Where(c => c.ParentCategoryId == null).Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetAllCategoriesWithFeedsAsync_ShouldReturnCategoriesWithTheirFeeds()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("With Feeds");
            await _feedService.AddFeedAsync(TestFeed, category.Id);

            // Act
            var categories = await _categoryService.GetAllCategoriesWithFeedsAsync();

            // Assert
            categories.Should().Contain(c => c.Id == category.Id);
            var foundCategory = categories.First(c => c.Id == category.Id);
            foundCategory.Feeds.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetCategoryWithFeedsAsync_WithExistingCategory_ShouldReturnCategoryWithFeeds()
        {
            // Arrange
            var category = await _categoryService.CreateCategoryAsync("Single With Feeds");
            await _feedService.AddFeedAsync(TestFeed, category.Id);

            // Act
            var result = await _categoryService.GetCategoryWithFeedsAsync(category.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Feeds.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GetOrCreateCategoryAsync_WithNewName_ShouldCreateCategory()
        {
            // Arrange
            var uniqueName = $"Test Category {Guid.NewGuid()}";

            // Act
            var category = await _categoryService.GetOrCreateCategoryAsync(uniqueName);

            // Assert
            category.Should().NotBeNull();
            category.Id.Should().BeGreaterThan(0);
            category.Name.Should().Be(uniqueName);
        }

        [Fact]
        public async Task GetOrCreateCategoryAsync_WithExistingName_ShouldReturnExisting()
        {
            // Arrange
            var existing = await _categoryService.CreateCategoryAsync("Existing For GetOrCreate");

            // Act
            var result = await _categoryService.GetOrCreateCategoryAsync("Existing For GetOrCreate");

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(existing.Id);
        }

        [Fact]
        public async Task GetOrCreateCategoryAsync_WithEmptyName_ShouldThrowArgumentException()
        {
            // Act
            Func<Task> act = () => _categoryService.GetOrCreateCategoryAsync("");

            // Assert
            await act.Should().ThrowAsync<ArgumentException>();
        }
    }
}