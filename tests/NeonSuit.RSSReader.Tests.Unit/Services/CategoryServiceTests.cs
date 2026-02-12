using Moq;
using NeonSuit.RSSReader.Core.Interfaces.Repositories;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    /// <summary>
    /// Professional test suite for the CategoryService class.
    /// Tests all public methods with various scenarios including edge cases.
    /// Verifies category hierarchy, statistics, and repository coordination.
    /// </summary>
    public class CategoryServiceTests
    {
        private readonly Mock<ICategoryRepository> _mockCategoryRepository;
        private readonly Mock<IFeedRepository> _mockFeedRepository;
        private readonly Mock<ILogger> _mockLogger;
        private readonly CategoryService _categoryService;

        /// <summary>
        /// Initializes test dependencies before each test.
        /// Creates mock repositories and instantiates the CategoryService.
        /// </summary>
        public CategoryServiceTests()
        {
            _mockCategoryRepository = new Mock<ICategoryRepository>();
            _mockFeedRepository = new Mock<IFeedRepository>();
            _mockLogger = new Mock<ILogger>();

            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
               .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                       .Returns(_mockLogger.Object);

            _categoryService = new CategoryService(
                _mockCategoryRepository.Object,
                _mockFeedRepository.Object,
                _mockLogger.Object);
        }

        #region Test Data Setup

        /// <summary>
        /// Creates a test Category instance with realistic data.
        /// </summary>
        /// <param name="id">The category identifier.</param>
        /// <param name="parentId">Optional parent category identifier.</param>
        /// <param name="feedCount">Feed count for statistics.</param>
        /// <param name="unreadCount">Unread count for statistics.</param>
        /// <returns>A configured Category instance for testing.</returns>
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

        /// <summary>
        /// Creates a list of test categories with hierarchical relationships.
        /// </summary>
        /// <param name="count">Number of categories to create.</param>
        /// <param name="withHierarchy">Whether to create parent-child relationships.</param>
        /// <returns>A list of test categories.</returns>
        private List<Category> CreateTestCategories(int count, bool withHierarchy = false)
        {
            var categories = new List<Category>();

            for (int i = 1; i <= count; i++)
            {
                int? parentId = null;

                if (withHierarchy && i > 1)
                {
                    parentId = (i % 2) + 1; // Simple hierarchical pattern
                }

                categories.Add(CreateTestCategory(i, parentId));
            }

            return categories;
        }

        /// <summary>
        /// Creates a dictionary of feed counts by category ID.
        /// </summary>
        /// <param name="categoryIds">The category IDs to include.</param>
        /// <param name="baseCount">Base count to start from.</param>
        /// <returns>A dictionary with category ID keys and feed count values.</returns>
        private Dictionary<int, int> CreateFeedCounts(IEnumerable<int> categoryIds, int baseCount = 1)
        {
            return categoryIds.ToDictionary(
                id => id,
                id => baseCount + (id * 2)); // Varying counts
        }

        /// <summary>
        /// Creates a dictionary of unread counts by category ID.
        /// </summary>
        /// <param name="categoryIds">The category IDs to include.</param>
        /// <param name="baseCount">Base count to start from.</param>
        /// <returns>A dictionary with category ID keys and unread count values.</returns>
        private Dictionary<int, int> CreateUnreadCounts(IEnumerable<int> categoryIds, int baseCount = 0)
        {
            return categoryIds.ToDictionary(
                id => id,
                id => baseCount + id); // Varying counts
        }

        #endregion

        #region GetAllCategoriesAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetAllCategoriesAsync_WhenCalled_ReturnsCategoriesWithStatistics()
        {
            // Arrange
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

            // Act
            var result = await _categoryService.GetAllCategoriesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count);

            // Verify statistics are populated
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
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetAllCategoriesAsync_WhenNoCategoriesExist_ReturnsEmptyList()
        {
            // Arrange
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

            // Act
            var result = await _categoryService.GetAllCategoriesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task GetAllCategoriesAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Database connection failed");

            _mockCategoryRepository
                .Setup(repo => repo.GetAllOrderedAsync())
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _categoryService.GetAllCategoriesAsync());
        }

        #endregion

        #region GetCategoryByIdAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetCategoryByIdAsync_WithValidId_ReturnsCategoryWithStatistics()
        {
            // Arrange
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

            // Act
            var result = await _categoryService.GetCategoryByIdAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(categoryId, result.Id);
            Assert.Equal(feedCounts[categoryId], result.FeedCount);
            Assert.Equal(unreadCounts[categoryId], result.UnreadCount);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetCategoryByIdAsync_WithInvalidId_ReturnsNull()
        {
            // Arrange
            var categoryId = 999;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            // Act
            var result = await _categoryService.GetCategoryByIdAsync(categoryId);

            // Assert
            Assert.Null(result);
            _mockFeedRepository.Verify(repo => repo.GetCountByCategoryAsync(), Times.Never);
            _mockCategoryRepository.Verify(repo => repo.GetUnreadCountsByCategoryAsync(), Times.Never);
        }

        #endregion

        #region CreateCategoryAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task CreateCategoryAsync_WithValidName_CreatesAndReturnsCategory()
        {
            // Arrange - Datos de entrada que queremos probar
            var name = "New Category";
            var color = "#FF5733";
            var description = "Test description";

            // Creamos el objeto de prueba con el ID 1
            var createdCategory = CreateTestCategory(1);

            // Machacamos los valores por defecto del Factory con los que definimos arriba
            // para que el Assert no falle por diferencia de strings
            createdCategory.Name = name;
            createdCategory.Color = color;
            createdCategory.Description = description;

            // Configuramos el Mock para que cuando reciba esos datos exactos, devuelva nuestro objeto
            _mockCategoryRepository
                .Setup(repo => repo.CreateWithOrderAsync(name, color, description))
                .ReturnsAsync(createdCategory);

            // Act - Ejecutamos la lógica del servicio
            var result = await _categoryService.CreateCategoryAsync(name, color, description);

            // Assert - Verificaciones de Título de Oro
            Assert.NotNull(result);
            Assert.Equal(name, result.Name);
            Assert.Equal(color, result.Color);
            Assert.Equal(description, result.Description);

            // Verificamos que el repositorio se llamó una sola vez con los parámetros correctos
            _mockCategoryRepository.Verify(repo =>
                repo.CreateWithOrderAsync(name, color, description), Times.Once);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Validation")]
        [Trait("Type", "Exception")]
        public async Task CreateCategoryAsync_WithInvalidName_ThrowsArgumentException(string? invalidName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _categoryService.CreateCategoryAsync(invalidName ?? string.Empty));
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task CreateCategoryAsync_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var name = "Test Category";
            var expectedException = new InvalidOperationException("Database error");

            _mockCategoryRepository
                .Setup(repo => repo.CreateWithOrderAsync(name, null, null))
                .ThrowsAsync(expectedException);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _categoryService.CreateCategoryAsync(name));
        }

        #endregion

        #region UpdateCategoryAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task UpdateCategoryAsync_WithValidCategory_ReturnsTrue()
        {
            // Arrange
            var category = CreateTestCategory(1);
            category.Name = "Updated Name";

            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(category))
                .ReturnsAsync(1); // 1 row affected

            // Act
            var result = await _categoryService.UpdateCategoryAsync(category);

            // Assert
            Assert.True(result);
            _mockCategoryRepository.Verify(repo => repo.UpdateAsync(category), Times.Once);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task UpdateCategoryAsync_WhenNoRowsAffected_ReturnsFalse()
        {
            // Arrange
            var category = CreateTestCategory(1);

            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(category))
                .ReturnsAsync(0); // No rows affected

            // Act
            var result = await _categoryService.UpdateCategoryAsync(category);

            // Assert
            Assert.False(result);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Validation")]
        [Trait("Type", "Exception")]
        public async Task UpdateCategoryAsync_WithNullCategory_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _categoryService.UpdateCategoryAsync(null!));
        }

        #endregion

        #region DeleteCategoryAsync Tests

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task DeleteCategoryAsync_WithDifferentScenarios_ReturnsExpectedResult(
            int categoryId, bool repositoryResult)
        {
            // Arrange
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

            // Act
            var result = await _categoryService.DeleteCategoryAsync(categoryId);

            // Assert
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
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task ReorderCategoriesAsync_WithValidIds_ReturnsTrue()
        {
            // Arrange
            var categoryIds = new List<int> { 3, 1, 2 };

            _mockCategoryRepository
                .Setup(repo => repo.ReorderAsync(categoryIds))
                .ReturnsAsync(categoryIds.Count);

            // Act
            var result = await _categoryService.ReorderCategoriesAsync(categoryIds);

            // Assert
            Assert.True(result);
            _mockCategoryRepository.Verify(repo => repo.ReorderAsync(categoryIds), Times.Once);
        }

      

        [Theory]
        [InlineData(null)]
        [InlineData(new int[0])]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "EdgeCase")]
        [Trait("Type", "Unit")]
        public async Task ReorderCategoriesAsync_WithNullOrEmptyList_ReturnsFalse(int[]? categoryIds)
        {
            var list = categoryIds?.ToList() ?? new List<int>();
            // Act
            var result = await _categoryService.ReorderCategoriesAsync(list);

            // Assert
            Assert.False(result);
            _mockCategoryRepository.Verify(repo => repo.ReorderAsync(It.IsAny<List<int>>()), Times.Never);
        }

        #endregion

        #region GetFeedCountsAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetFeedCountsAsync_WhenCalled_ReturnsCountsFromRepository()
        {
            // Arrange
            var expectedCounts = CreateFeedCounts(new[] { 1, 2, 3 });

            _mockFeedRepository
                .Setup(repo => repo.GetCountByCategoryAsync())
                .ReturnsAsync(expectedCounts);

            // Act
            var result = await _categoryService.GetFeedCountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCounts.Count, result.Count);
            Assert.Equal(expectedCounts[1], result[1]);
            _mockFeedRepository.Verify(repo => repo.GetCountByCategoryAsync(), Times.Once);
        }

        #endregion

        #region GetUnreadCountsAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Statistics")]
        [Trait("Type", "Unit")]
        public async Task GetUnreadCountsAsync_WhenCalled_ReturnsCountsFromRepository()
        {
            // Arrange
            var expectedCounts = CreateUnreadCounts(new[] { 1, 2, 3 });

            _mockCategoryRepository
                .Setup(repo => repo.GetUnreadCountsByCategoryAsync())
                .ReturnsAsync(expectedCounts);

            // Act
            var result = await _categoryService.GetUnreadCountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedCounts.Count, result.Count);
            _mockCategoryRepository.Verify(repo => repo.GetUnreadCountsByCategoryAsync(), Times.Once);
        }

        #endregion

        #region CategoryExistsByNameAsync Tests

        [Theory]
        [InlineData("Existing", true)]
        [InlineData("NonExisting", false)]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Validation")]
        [Trait("Type", "Unit")]
        public async Task CategoryExistsByNameAsync_WhenCalled_ReturnsRepositoryResult(string name, bool exists)
        {
            // Arrange
            _mockCategoryRepository
                .Setup(repo => repo.ExistsByNameAsync(name))
                .ReturnsAsync(exists);

            // Act
            var result = await _categoryService.CategoryExistsByNameAsync(name);

            // Assert
            Assert.Equal(exists, result);
            _mockCategoryRepository.Verify(repo => repo.ExistsByNameAsync(name), Times.Once);
        }

        #endregion

        #region GetCategoryTreeAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Hierarchy")]
        [Trait("Type", "Unit")]
        public async Task GetCategoryTreeAsync_WithHierarchicalCategories_ReturnsTreeStructure()
        {
            // Arrange
            var categories = CreateTestCategories(5, withHierarchy: false); // Ponemos false para control total

            // Limpiamos y configuramos manualmente la estructura
            foreach (var c in categories) c.ParentCategoryId = null;

            // Relaciones:
            // 2 es raíz -> hijos 1 y 3
            // 3 es hijo de 2 -> hijo 4
            // 5 es raíz -> sin hijos
            categories.First(c => c.Id == 1).ParentCategoryId = 2;
            categories.First(c => c.Id == 3).ParentCategoryId = 2;
            categories.First(c => c.Id == 4).ParentCategoryId = 3;

            // Las categorías 2 y 5 se quedan con ParentCategoryId = null (son raíces)

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

            // Act
            var result = await _categoryService.GetCategoryTreeAsync();

            // Assert
            Assert.NotNull(result);
            // Ahora sí: 2 y 5 son las únicas con ParentId nulo
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Id == 2);
            Assert.Contains(result, c => c.Id == 5);

            var category2 = result.First(c => c.Id == 2);
            Assert.Equal(2, category2.Children.Count); // Hijos 1 y 3
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Hierarchy")]
        [Trait("Type", "EdgeCase")]
        public async Task GetCategoryTreeAsync_WithFlatCategories_ReturnsAllAsRoots()
        {
            // Arrange
            var categories = CreateTestCategories(3); // No hierarchy
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

            // Act
            var result = await _categoryService.GetCategoryTreeAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count); // All should be roots
        }

        #endregion

        #region GetAllCategoriesWithFeedsAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetAllCategoriesWithFeedsAsync_WhenCalled_ReturnsCategoriesWithFeeds()
        {
            // Arrange
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
                .Setup(repo => repo.GetFeedsGroupedByCategoryAsync())
                .ReturnsAsync(feedsByCategory);

            // Act
            var result = await _categoryService.GetAllCategoriesWithFeedsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(categories.Count, result.Count);

            Assert.Single(result[0].Feeds); // Category 1 has 1 feed
            Assert.Equal(2, result[1].Feeds.Count); // Category 2 has 2 feeds

            _mockCategoryRepository.Verify(repo => repo.GetAllAsync(), Times.Once);
            _mockFeedRepository.Verify(repo => repo.GetFeedsGroupedByCategoryAsync(), Times.Once);
        }

        #endregion

        #region GetCategoryWithFeedsAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "Unit")]
        public async Task GetCategoryWithFeedsAsync_WithValidId_ReturnsCategoryWithFeeds()
        {
            // Arrange
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
                .Setup(repo => repo.GetFeedsByCategoryAsync(categoryId))
                .ReturnsAsync(feeds);

            // Act
            var result = await _categoryService.GetCategoryWithFeedsAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(categoryId, result.Id);
            Assert.Equal(feeds.Count, result.Feeds.Count);
            Assert.Equal("Feed 1", result.Feeds[0].Title);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ReadOperations")]
        [Trait("Type", "EdgeCase")]
        public async Task GetCategoryWithFeedsAsync_WithInvalidId_ReturnsNull()
        {
            // Arrange
            var categoryId = 999;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            // Act
            var result = await _categoryService.GetCategoryWithFeedsAsync(categoryId);

            // Assert
            Assert.Null(result);
            _mockFeedRepository.Verify(repo => repo.GetFeedsByCategoryAsync(It.IsAny<int>()), Times.Never);
        }

        #endregion

        #region GetOrCreateCategoryAsync Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
        public async Task GetOrCreateCategoryAsync_WhenCategoryExists_ReturnsExistingCategory()
        {
            // Arrange
            var name = "Existing Category";
            var existingCategory = CreateTestCategory(1);
            existingCategory.Name = name;

            _mockCategoryRepository
                .Setup(repo => repo.GetByNameAsync(name))
                .ReturnsAsync(existingCategory);

            // Act
            var result = await _categoryService.GetOrCreateCategoryAsync(name);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingCategory.Id, result.Id);
            Assert.Equal(name, result.Name);
            _mockCategoryRepository.Verify(repo => repo.GetByNameAsync(name), Times.Once);
            _mockCategoryRepository.Verify(repo => repo.InsertAsync(It.IsAny<Category>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "WriteOperations")]
        [Trait("Type", "Unit")]
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

            _mockCategoryRepository
                .Setup(repo => repo.InsertAsync(It.IsAny<Category>()))
                .ReturnsAsync(newCategoryId);

            // Act
            var result = await _categoryService.GetOrCreateCategoryAsync(name);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(newCategoryId, result.Id);
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
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Validation")]
        [Trait("Type", "Exception")]
        public async Task GetOrCreateCategoryAsync_WithInvalidName_ThrowsArgumentException(string? invalidName)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _categoryService.GetOrCreateCategoryAsync(invalidName ?? string.Empty));
        }

        #endregion

        #region Integration-Style Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Integration")]
        [Trait("Type", "Integration")]
        public async Task CompleteCategoryWorkflow_CreateUpdateDelete_ExecutesSuccessfully()
        {
            // Arrange
            var categoryName = "Integration Test Category";
            var createdCategory = CreateTestCategory(1);
            createdCategory.Name = categoryName;

            // Setup creation
            _mockCategoryRepository
                .Setup(repo => repo.GetByNameAsync(categoryName))
                .ReturnsAsync((Category?)null);

            _mockCategoryRepository
                .Setup(repo => repo.GetMaxSortOrderAsync())
                .ReturnsAsync(5);

            _mockCategoryRepository
                .Setup(repo => repo.InsertAsync(It.IsAny<Category>()))
                .ReturnsAsync(1);

            // Setup update
            _mockCategoryRepository
                .Setup(repo => repo.UpdateAsync(It.IsAny<Category>()))
                .ReturnsAsync(1);

            // Setup deletion
            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(1))
                .ReturnsAsync(createdCategory);

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
        }

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "Integration")]
        [Trait("Type", "Integration")]
        public async Task CategoryHierarchyWorkflow_BuildsCorrectTreeStructure()
        {
            // Arrange
            var categories = new List<Category>
            {
                CreateTestCategory(1, null), // Root
                CreateTestCategory(2, 1),    // Child of 1
                CreateTestCategory(3, 1),    // Child of 1
                CreateTestCategory(4, 2),    // Child of 2 (grandchild of 1)
                CreateTestCategory(5, null)  // Another root
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

            // Act
            var tree = await _categoryService.GetCategoryTreeAsync();

            // Assert
            Assert.NotNull(tree);
            Assert.Equal(2, tree.Count); // Two roots (IDs 1 and 5)

            var root1 = tree.First(c => c.Id == 1);
            Assert.Equal(2, root1.Children.Count); // Should have children 2 and 3

            var child2 = root1.Children.First(c => c.Id == 2);
            Assert.Single(child2.Children); // Should have child 4
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        [Trait("Category", "CategoryService")]
        [Trait("Scope", "ErrorHandling")]
        [Trait("Type", "Exception")]
        public async Task AnyServiceMethod_WhenRepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var expectedException = new InvalidOperationException("Database connection failed");
            var categoryId = 1;

            _mockCategoryRepository
                .Setup(repo => repo.GetByIdAsync(categoryId))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _categoryService.GetCategoryByIdAsync(categoryId));

            Assert.Equal(expectedException.Message, exception.Message);
        }

        #endregion
    }
}