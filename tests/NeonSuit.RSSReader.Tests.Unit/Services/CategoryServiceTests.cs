using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    public class CategoryServiceTests
    {
        private readonly Mock<ICategoryRepository> _mockCategoryRepository;
        private readonly Mock<IFeedRepository> _mockFeedRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly CategoryService _categoryService;

        public CategoryServiceTests()
        {
            _mockCategoryRepository = new Mock<ICategoryRepository>();
            _mockFeedRepository = new Mock<IFeedRepository>();
            _mockLogger = new Mock<ILogger>();

            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _categoryService = new CategoryService(
                _mockCategoryRepository.Object,
                _mockFeedRepository.Object,
                _mockLogger.Object);
        }

        #region Test Data Setup

        private Category CreateTestCategory(
            int id = 1,
            int? parentId = null,
            int feedCount = 0,
            int unreadCount = 0)
        {
            return new Category
            {
                Id = id,
                Name = $"Test Category {id}",
                Color = id % 2 == 0 ? "#FF0000" : "#00FF00",
                Description = $"Description for category {id}",
                SortOrder = id,
                ParentCategoryId = parentId,
                FeedCount = feedCount,
                UnreadCount = unreadCount,
                CreatedAt = DateTime.UtcNow.AddDays(-id),
                Feeds = new List<Feed>(),
                Children = new List<Category>(),
                Parent = null
            };
        }

        private List<Category> CreateTestCategories(int count, bool withHierarchy = false)
        {
            var categories = new List<Category>();

            for (int i = 1; i <= count; i++)
            {
                int? parentId = null;

                if (withHierarchy && i > 1)
                {
                    parentId = (i % 2) + 1;
                }

                categories.Add(CreateTestCategory(i, parentId));
            }

            return categories;
        }

        private Dictionary<int, int> CreateFeedCounts(IEnumerable<int> categoryIds, int baseCount = 1)
        {
            return categoryIds.ToDictionary(
                id => id,
                id => baseCount + (id * 2));
        }

        private Dictionary<int, int> CreateUnreadCounts(IEnumerable<int> categoryIds, int baseCount = 0)
        {
            return categoryIds.ToDictionary(
                id => id,
                id => baseCount + id);
        }

        #endregion

        #region GetAllCategoriesAsync Tests

        [Fact]
        public async Task GetAllCategoriesAsync_WhenCalled_ReturnsCategoriesWithStatistics()
        {
            var categories = CreateTestCategories(3);
            var categoryIds = categories.Select(c => c.Id).ToList();
            var feedCounts = CreateFeedCounts(categoryIds);
            var unreadCounts = CreateUnreadCounts(categoryIds);

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ReturnsAsync(categories);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(feedCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(unreadCounts);

            var result = await _categoryService.GetAllCategoriesAsync();

            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count);

            foreach (var category in result)
            {
                Assert.Equal(feedCounts[category.Id], category.FeedCount);
                Assert.Equal(unreadCounts[category.Id], category.UnreadCount);
            }

            _mockCategoryRepository.Verify(repo => repo.GetAllOrderedAsync(), Times.Once);
            _mockFeedRepository.Verify(repo => repo.GetCountByCategoryAsync(), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.GetUnreadCountsByCategoryAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllCategoriesAsync_WhenNoCategoriesExist_ReturnsEmptyList()
        {
            var emptyCategories = new List<Category>();
            var emptyCounts = new Dictionary<int, int>();

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ReturnsAsync(emptyCategories);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(emptyCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(emptyCounts);

            var result = await _categoryService.GetAllCategoriesAsync();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllCategoriesAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ThrowsAsync(expectedException);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _categoryService.GetAllCategoriesAsync());
        }

        #endregion

        #region GetCategoryByIdAsync Tests

        [Fact]
        public async Task GetCategoryByIdAsync_WithValidId_ReturnsCategoryWithStatistics()
        {
            var categoryId = 1;
            var category = CreateTestCategory(categoryId);
            var feedCounts = CreateFeedCounts(new[] { categoryId });
            var unreadCounts = CreateUnreadCounts(new[] { categoryId });

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(feedCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(unreadCounts);

            var result = await _categoryService.GetCategoryByIdAsync(categoryId);

            Assert.NotNull(result);
            Assert.Equal(categoryId, result.Id);
            Assert.Equal(feedCounts[categoryId], result.FeedCount);
            Assert.Equal(unreadCounts[categoryId], result.UnreadCount);
        }

        [Fact]
        public async Task GetCategoryByIdAsync_WithInvalidId_ReturnsNull()
        {
            var categoryId = 999;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            var result = await _categoryService.GetCategoryByIdAsync(categoryId);

            Assert.Null(result);
            _mockFeedRepository.Verify(repo => repo.GetCountByCategoryAsync(), Times.Never);
            _mockCategoryRepository.Verify(repo => repo.GetUnreadCountsByCategoryAsync(), Times.Never);
        }

        #endregion

        #region CreateCategoryAsync Tests

        [Fact]
        public async Task CreateCategoryAsync_WithValidName_CreatesAndReturnsCategory()
        {
            var name = "New Category";
            var color = "#FF5733";
            var description = "Test description";
            var createdCategory = CreateTestCategory(1);
            createdCategory.Name = name;
            createdCategory.Color = color;
            createdCategory.Description = description;

            _mockCategoryRepository
                .Setup(repo => repo.CreateWithOrderAsync(name, color, description))
                .ReturnsAsync(createdCategory);

            var result = await _categoryService.CreateCategoryAsync(name, color, description);

            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal(color, result.Color);
            Assert.Equal(description, result.Description);
            _mockCategoryRepository.Verify(repo => repo.CreateWithOrderAsync(name, color, description), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task CreateCategoryAsync_WithInvalidName_ThrowsArgumentException(string? invalidName)
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _categoryService.CreateCategoryAsync(invalidName ?? string.Empty));
        }

        [Fact]
        public async Task CreateCategoryAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            var name = "Test Category";
            var expectedException = new InvalidOperationException("Database error");

            _mockCategoryRepository
                .Setup(repo => repo.CreateWithOrderAsync(name, null, null))
                .ThrowsAsync(expectedException);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _categoryService.CreateCategoryAsync(name));
        }

        #endregion

        #region UpdateCategoryAsync Tests

        [Fact]
        public async Task UpdateCategoryAsync_WithValidCategory_ReturnsTrue()
        {
            var category = CreateTestCategory(1);
            category.Name = "Updated Name";

            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(category))
                .ReturnsAsync(1);

            var result = await _categoryService.UpdateCategoryAsync(category);

            Assert.True(result);
            _mockCategoryRepository.Verify(repo => repo.UpdateAsync(category), Times.Once);
        }

        [Fact]
        public async Task UpdateCategoryAsync_WhenNoRowsAffected_ReturnsFalse()
        {
            var category = CreateTestCategory(1);

            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(category))
                .ReturnsAsync(0);

            var result = await _categoryService.UpdateCategoryAsync(category);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateCategoryAsync_WithNullCategory_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _categoryService.UpdateCategoryAsync(null!));
        }

        #endregion

        #region DeleteCategoryAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        public async Task DeleteCategoryAsync_WithDifferentScenarios_ReturnsExpectedResult(
            int categoryId, bool repositoryResult)
        {
            var category = repositoryResult ? CreateTestCategory(categoryId) : null;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            if (repositoryResult)
            {
                _mockCategoryRepository
                    .Setup(repo => repo.DeleteAsync(categoryId))
                    .ReturnsAsync(1);
            }

            var result = await _categoryService.DeleteCategoryAsync(categoryId);

            Assert.Equal(repositoryResult, result);
            _mockCategoryRepository.Verify(repo => repo.GetByIdAsync(categoryId), Times.Once);

            if (repositoryResult)
            {
                _mockCategoryRepository.Verify(repo => repo.DeleteAsync(categoryId), Times.Once);
            }
            else
            {
                _mockCategoryRepository.Verify(repo => repo.DeleteAsync(It.IsAny<int>()), Times.Never);
            }
        }

        #endregion

        #region ReorderCategoriesAsync Tests

        [Fact]
        public async Task ReorderCategoriesAsync_WithValidIds_ReturnsTrue()
        {
            var categoryIds = new List<int> { 3, 1, 2 };

            _mockCategoryRepository
                .Setup(repo => repo.ReorderAsync(categoryIds))
                .ReturnsAsync(categoryIds.Count);

            var result = await _categoryService.ReorderCategoriesAsync(categoryIds);

            Assert.True(result);
            _mockCategoryRepository.Verify(repo => repo.ReorderAsync(categoryIds), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new int[0])]
        public async Task ReorderCategoriesAsync_WithNullOrEmptyList_ReturnsFalse(int[]? categoryIds)
        {
            var list = categoryIds?.ToList() ?? new List<int>();

            var result = await _categoryService.ReorderCategoriesAsync(list);

            Assert.False(result);
            _mockCategoryRepository.Verify(repo => repo.ReorderAsync(It.IsAny<List<int>>()), Times.Never);
        }

        #endregion

        #region GetFeedCountsAsync Tests

        [Fact]
        public async Task GetFeedCountsAsync_WhenCalled_ReturnsCountsFromRepository()
        {
            var expectedCounts = CreateFeedCounts(new[] { 1, 2, 3 });

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(expectedCounts);

            var result = await _categoryService.GetFeedCountsAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedCounts.Count, result.Count);
            Assert.Equal(expectedCounts[1], result[1]);
            _mockFeedRepository.Verify(repo => repo.GetCountByCategoryAsync(), Times.Once);
        }

        #endregion

        #region GetUnreadCountsAsync Tests

        [Fact]
        public async Task GetUnreadCountsAsync_WhenCalled_ReturnsCountsFromRepository()
        {
            var expectedCounts = CreateUnreadCounts(new[] { 1, 2, 3 });

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(expectedCounts);

            var result = await _categoryService.GetUnreadCountsAsync();

            Assert.NotNull(result);
            Assert.Equal(expectedCounts.Count, result.Count);
            _mockCategoryRepository.Verify(repo => repo.GetUnreadCountsByCategoryAsync(), Times.Once);
        }

        #endregion

        #region CategoryExistsByNameAsync Tests

        [Theory]
        [InlineData("Existing", true)]
        [InlineData("NonExisting", false)]
        public async Task CategoryExistsByNameAsync_WhenCalled_ReturnsRepositoryResult(string name, bool exists)
        {
            _mockCategoryRepository
                .Setup(repo => repo.ExistsByNameAsync(name))
                .ReturnsAsync(exists);

            var result = await _categoryService.CategoryExistsByNameAsync(name);

            Assert.Equal(exists, result);
            _mockCategoryRepository.Verify(repo => repo.ExistsByNameAsync(name), Times.Once);
        }

        #endregion

        #region GetCategoryTreeAsync Tests

        [Fact]
        public async Task GetCategoryTreeAsync_WithHierarchicalCategories_ReturnsTreeStructure()
        {
            var categories = CreateTestCategories(5, withHierarchy: false);

            foreach (var c in categories) c.ParentCategoryId = null;

            categories.First(c => c.Id == 1).ParentCategoryId = 2;
            categories.First(c => c.Id == 3).ParentCategoryId = 2;
            categories.First(c => c.Id == 4).ParentCategoryId = 3;

            var feedCounts = CreateFeedCounts(categories.Select(c => c.Id));
            var unreadCounts = CreateUnreadCounts(categories.Select(c => c.Id));

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ReturnsAsync(categories);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(feedCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(unreadCounts);

            var result = await _categoryService.GetCategoryTreeAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Id == 2);
            Assert.Contains(result, c => c.Id == 5);

            var category2 = result.First(c => c.Id == 2);
            Assert.Equal(2, category2.Children.Count);
        }

        [Fact]
        public async Task GetCategoryTreeAsync_WithFlatCategories_ReturnsAllAsRoots()
        {
            var categories = CreateTestCategories(3);
            var feedCounts = CreateFeedCounts(categories.Select(c => c.Id));
            var unreadCounts = CreateUnreadCounts(categories.Select(c => c.Id));

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ReturnsAsync(categories);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(feedCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(unreadCounts);

            var result = await _categoryService.GetCategoryTreeAsync();

            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count);
        }

        #endregion

        #region GetAllCategoriesWithFeedsAsync Tests

        [Fact]
        public async Task GetAllCategoriesWithFeedsAsync_WhenCalled_ReturnsCategoriesWithFeeds()
        {
            var categories = CreateTestCategories(2);
            var feedsByCategory = new Dictionary<int, List<Feed>>
            {
                [1] = new List<Feed> { new Feed { Id = 1, Title = "Feed 1" } },
                [2] = new List<Feed> {
                    new Feed { Id = 2, Title = "Feed 2" },
                    new Feed { Id = 3, Title = "Feed 3" }
                }
            };

            _mockCategoryRepository
                .Setup(repo => repo.GetAllAsync())
                .ReturnsAsync(categories);

            _mockFeedRepository
                .Setup(repo => repo.GetFeedsGroupedByCategoryAsync(It.IsAny<bool>()))
                .ReturnsAsync(feedsByCategory);

            var result = await _categoryService.GetAllCategoriesWithFeedsAsync();

            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count);
            Assert.Single(result[0].Feeds);
            Assert.Equal(2, result[1].Feeds.Count);

            _mockCategoryRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
            _mockFeedRepository.Verify(repo => repo.GetFeedsGroupedByCategoryAsync(It.IsAny<bool>()), Times.Once);
        }

        #endregion

        #region GetCategoryWithFeedsAsync Tests

        [Fact]
        public async Task GetCategoryWithFeedsAsync_WithValidId_ReturnsCategoryWithFeeds()
        {
            var categoryId = 1;
            var category = CreateTestCategory(categoryId);
            var feeds = new List<Feed>
            {
                new Feed { Id = 1, Title = "Feed 1", CategoryId = categoryId },
                new Feed { Id = 2, Title = "Feed 2", CategoryId = categoryId }
            };

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            _mockFeedRepository
                .Setup(repo => repo.GetFeedsByCategoryAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var result = await _categoryService.GetCategoryWithFeedsAsync(categoryId);

            Assert.NotNull(result);
            Assert.Equal(categoryId, result.Id);
            Assert.Equal(feeds.Count, result.Feeds.Count);
            Assert.Equal("Feed 1", result.Feeds[0].Title);
        }

        [Fact]
        public async Task GetCategoryWithFeedsAsync_WithInvalidId_ReturnsNull()
        {
            var categoryId = 999;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            var result = await _categoryService.GetCategoryWithFeedsAsync(categoryId);

            Assert.Null(result);
            _mockFeedRepository.Verify(repo => repo.GetFeedsByCategoryAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        }

        #endregion

        #region GetOrCreateCategoryAsync Tests

        [Fact]
        public async Task GetOrCreateCategoryAsync_WhenCategoryExists_ReturnsExistingCategory()
        {
            var name = "Existing Category";
            var existingCategory = CreateTestCategory(1);
            existingCategory.Name = name;

            _mockCategoryRepository
                .Setup(repo => repo.GetByNameAsync(name))
                .ReturnsAsync(existingCategory);

            var result = await _categoryService.GetOrCreateCategoryAsync(name);

            Assert.NotNull(result);
            Assert.Equal(existingCategory.Id, result.Id);
            Assert.Equal(name, result.Name);
            _mockCategoryRepository.Verify(repo => repo.GetByNameAsync(name), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.InsertAsync(It.IsAny<Category>()), Times.Never);
        }

        [Fact]
        public async Task GetOrCreateCategoryAsync_WhenCategoryDoesNotExist_CreatesNewCategory()
        {
            // Arrange
            var name = "New Category";
            var newCategoryId = 10;
            var maxSortOrder = 5;

            _mockCategoryRepository
                .Setup(repo => repo.GetByNameAsync(name))
                .ReturnsAsync((Category?)null);

            _mockCategoryRepository
                .Setup(repo => repo.GetMaxSortOrderAsync())
                .ReturnsAsync(maxSortOrder);

            // ✅ Configurar InsertAsync con Callback para asignar el ID
            _mockCategoryRepository
                .Setup(repo => repo.InsertAsync(It.IsAny<Category>()))
                .Callback<Category>(c => c.Id = newCategoryId)  // Asignar ID en el callback
                .ReturnsAsync(1);  // Devuelve 1 (filas afectadas)

            // Act
            var result = await _categoryService.GetOrCreateCategoryAsync(name);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(newCategoryId, result.Id);  // Ahora será 10
            Assert.Equal(name, result.Name.Trim());
            Assert.Equal(maxSortOrder + 1, result.SortOrder);
            Assert.NotNull(result.Color);

            _mockCategoryRepository.Verify(repo => repo.GetByNameAsync(name), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.InsertAsync(It.IsAny<Category>()), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task GetOrCreateCategoryAsync_WithInvalidName_ThrowsArgumentException(string? invalidName)
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _categoryService.GetOrCreateCategoryAsync(invalidName ?? string.Empty));
        }

        #endregion

        #region Integration-Style Tests

        [Fact]
        public async Task CompleteCategoryWorkflow_CreateUpdateDelete_ExecutesSuccessfully()
        {
            // Arrange
            var categoryName = "Integration Test Category";
            var createdCategory = CreateTestCategory(1);
            createdCategory.Name = categoryName;
            createdCategory.Id = 1;

            // Setup creation
            _mockCategoryRepository
                .Setup(repo => repo.GetByNameAsync(categoryName))
                .ReturnsAsync((Category?)null);

            _mockCategoryRepository
                .Setup(repo => repo.GetMaxSortOrderAsync())
                .ReturnsAsync(5);

            _mockCategoryRepository
                .Setup(repo => repo.InsertAsync(It.IsAny<Category>()))
                .Callback<Category>(c => c.Id = 1)
                .ReturnsAsync(1);

            // Setup update
            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(It.IsAny<Category>()))
                .ReturnsAsync(1);

            // Setup get for deletion - necesitamos mockear GetByIdAsync
            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(createdCategory);

            // Setup delete
            _mockCategoryRepository
                .Setup(repo => repo.DeleteAsync(1))
                .ReturnsAsync(1);

            // Act - Simulate complete workflow
            var category = await _categoryService.GetOrCreateCategoryAsync(categoryName);
            category.Description = "Updated description";
            var updateResult = await _categoryService.UpdateCategoryAsync(category);
            var deleteResult = await _categoryService.DeleteCategoryAsync(category.Id);

            // Assert
            Assert.NotNull(category);
            Assert.Equal(categoryName, category.Name.Trim());
            Assert.True(updateResult);
            Assert.True(deleteResult);

            // Verificaciones adicionales
            _mockCategoryRepository.Verify(repo => repo.GetByNameAsync(categoryName), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.GetMaxSortOrderAsync(), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.InsertAsync(It.IsAny<Category>()), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Category>()), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.GetByIdAsync(1), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.DeleteAsync(1), Times.Once);
        }

        [Fact]
        public async Task CategoryHierarchyWorkflow_BuildsCorrectTreeStructure()
        {
            var categories = new List<Category>
            {
                CreateTestCategory(1, null),
                CreateTestCategory(2, 1),
                CreateTestCategory(3, 1),
                CreateTestCategory(4, 2),
                CreateTestCategory(5, null)
            };

            var feedCounts = CreateFeedCounts(categories.Select(c => c.Id));
            var unreadCounts = CreateUnreadCounts(categories.Select(c => c.Id));

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ReturnsAsync(categories);

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(feedCounts);

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(unreadCounts);

            var tree = await _categoryService.GetCategoryTreeAsync();

            Assert.NotNull(tree);
            Assert.Equal(2, tree.Count);

            var root1 = tree.First(c => c.Id == 1);
            Assert.Equal(2, root1.Children.Count);

            var child2 = root1.Children.First(c => c.Id == 2);
            Assert.Single(child2.Children);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task AnyServiceMethod_WhenRepositoryThrowsException_PropagatesException()
        {
            var expectedException = new InvalidOperationException("Database connection failed");
            var categoryId = 1;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ThrowsAsync(expectedException);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _categoryService.GetCategoryByIdAsync(categoryId));

            Assert.Equal(expectedException.Message, exception.Message);
        }

        #endregion
    }
}