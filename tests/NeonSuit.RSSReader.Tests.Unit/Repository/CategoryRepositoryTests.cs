using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Database;
using NeonSuit.RSSReader.Data.Repositories;
using Serilog;

namespace NeonSuit.RSSReader.Tests.Unit.Repository
{
    [CollectionDefinition("Database_Categories")]
    public class DatabaseCollectionCategories : ICollectionFixture<DatabaseFixture> { }

    [Collection("Database_Categories")]
    public class CategoryRepositoryTests : IDisposable
    {
        private readonly RssReaderDbContext _dbContext;
        private readonly CategoryRepository _repository;
        private readonly Mock<ILogger> _mockLogger;
        private bool _disposed;

        private const int DEFAULT_CATEGORY_ID = 1;
        private const string DEFAULT_CATEGORY_NAME = "Test Category";
        private const string DEFAULT_COLOR = "#0078D4";
        private const string DEFAULT_DESCRIPTION = "Test Description";
        private const int DEFAULT_SORT_ORDER = 1;
        private const int SEED_COUNT = 5;

        public CategoryRepositoryTests(DatabaseFixture fixture)
        {
            _mockLogger = new Mock<ILogger>();
            SetupMockLogger();

            _dbContext = fixture.Context;
            ClearTestData();

            _repository = new CategoryRepository(_dbContext, _mockLogger.Object);
        }

        private void SetupMockLogger()
        {
            _mockLogger.Setup(x => x.ForContext<It.IsAnyType>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.ForContext<CategoryRepository>())
                .Returns(_mockLogger.Object);

            _mockLogger.Setup(x => x.Debug(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Information(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Warning(It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();

            _mockLogger.Setup(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Verifiable();
        }

        #region Test Data Helpers

        #region Test Data Helpers

        private void ClearTestData()
        {
            // Limpiar en orden inverso de dependencias para evitar violaciones de FK
            // Orden de eliminación (de más dependiente a menos dependiente):
            // 1. Tablas de unión (ArticleTags, etc.)
            // 2. Artículos
            // 3. Feeds
            // 4. Tags, Rules, otras entidades independientes
            // 5. Categories

            try
            {
                // Deshabilitar temporalmente las restricciones FK
                _dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

                // Orden correcto basado en dependencias (revisar tu esquema de BD)
                _dbContext.Database.ExecuteSqlRaw("DELETE FROM ArticleTags");
                _dbContext.Database.ExecuteSqlRaw("DELETE FROM Tags");
                _dbContext.Database.ExecuteSqlRaw("DELETE FROM Articles");
                _dbContext.Database.ExecuteSqlRaw("DELETE FROM Feeds");
                _dbContext.Database.ExecuteSqlRaw("DELETE FROM Categories");
                // Agrega aquí otras tablas si existen

                // Rehabilitar restricciones FK
                _dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");

                _dbContext.ChangeTracker.Clear();
            }
            catch (Exception)
            {
                // Si algo falla, al menos intentar mantener la base de datos en estado consistente
                _dbContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
                _dbContext.ChangeTracker.Clear();
                throw;
            }
        }

        #endregion

        private async Task<Category> CreateTestCategory(
            int id = DEFAULT_CATEGORY_ID,
            string name = DEFAULT_CATEGORY_NAME,
            int? parentId = null)
        {
            var category = new Category
            {
                Id = id,
                Name = name,
                Description = $"{DEFAULT_DESCRIPTION} {id}",
                Color = DEFAULT_COLOR,
                SortOrder = DEFAULT_SORT_ORDER + id - 1,
                ParentCategoryId = parentId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();
            return category;
        }

        private async Task<List<Category>> SeedTestCategoriesAsync(int count = SEED_COUNT)
        {
            var categories = new List<Category>();

            for (int i = 1; i <= count; i++)
            {
                var category = new Category
                {
                    Name = $"Test Category {i}",
                    Description = $"Description for category {i}",
                    Color = $"#{i:000000}",
                    SortOrder = i,
                    CreatedAt = DateTime.UtcNow.AddHours(-i)
                };

                _dbContext.Categories.Add(category);
                categories.Add(category);
            }

            await _dbContext.SaveChangesAsync();
            return categories;
        }

        private void ClearEntityTracking() => _dbContext.ChangeTracker.Clear();

        #endregion

        #region Basic CRUD Tests

        /// <summary>
        /// Tests that InsertAsync adds a valid Category to the database.
        /// </summary>
        [Fact]
        public async Task InsertAsync_WithValidCategory_ShouldAddToDatabase()
        {
            var category = new Category
            {
                Name = DEFAULT_CATEGORY_NAME,
                Description = DEFAULT_DESCRIPTION,
                Color = DEFAULT_COLOR,
                SortOrder = DEFAULT_SORT_ORDER,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _repository.InsertAsync(category);

            result.Should().Be(1);
            category.Id.Should().BeGreaterThan(0);

            ClearEntityTracking();
            var retrieved = await _repository.GetByIdAsync(category.Id);
            retrieved.Should().NotBeNull();
            retrieved?.Name.Should().Be(DEFAULT_CATEGORY_NAME);
            retrieved?.Description.Should().Be(DEFAULT_DESCRIPTION);
        }

        /// <summary>
        /// Tests that GetByIdAsync returns an existing Category.
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_WithExistingCategory_ShouldReturnCategory()
        {
            var category = await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.GetByIdAsync(category.Id);

            result.Should().NotBeNull();
            result?.Id.Should().Be(category.Id);
            result?.Name.Should().Be(DEFAULT_CATEGORY_NAME);
        }

        /// <summary>
        /// Tests that GetByIdAsync returns null for a non-existent Category.
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_WithNonExistentCategory_ShouldReturnNull()
        {
            var result = await _repository.GetByIdAsync(999);
            result.Should().BeNull();
        }

        /// <summary>
        /// Tests that UpdateAsync updates an existing Category in the database.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_WithValidCategory_ShouldUpdateInDatabase()
        {
            var category = await CreateTestCategory();
            var updatedName = "Updated Category Name";

            category.Name = updatedName;

            var result = await _repository.UpdateAsync(category);

            result.Should().Be(1);
            ClearEntityTracking();

            var retrieved = await _repository.GetByIdAsync(category.Id);
            retrieved?.Name.Should().Be(updatedName);
        }

        /// <summary>
        /// Tests that DeleteAsync removes an existing Category from the database.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithExistingCategory_ShouldRemoveFromDatabase()
        {
            var category = await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.DeleteAsync(category.Id);

            result.Should().Be(1);
            var exists = await _repository.GetByIdAsync(category.Id);
            exists.Should().BeNull();
        }

        /// <summary>
        /// Tests that DeleteAsync returns 0 for a non-existent Category.
        /// </summary>
        [Fact]
        public async Task DeleteAsync_WithNonExistentCategory_ShouldReturnZero()
        {
            var result = await _repository.DeleteAsync(999);
            result.Should().Be(0);
        }

        #endregion

        #region Ordering and Sorting Tests

        /// <summary>
        /// Tests that GetAllOrderedAsync returns categories ordered by SortOrder and Name.
        /// </summary>
        [Fact]
        public async Task GetAllOrderedAsync_WithMultipleCategories_ShouldReturnOrderedList()
        {
            await SeedTestCategoriesAsync();
            ClearEntityTracking();

            var result = await _repository.GetAllOrderedAsync();

            result.Should().NotBeNull().And.HaveCount(SEED_COUNT);
            result.Should().BeInAscendingOrder(c => c.SortOrder);
            result.Select(c => c.SortOrder).Should().Equal(1, 2, 3, 4, 5);
        }

        /// <summary>
        /// Tests that GetNextSortOrderAsync returns the next available sort order.
        /// </summary>
        [Fact]
        public async Task GetNextSortOrderAsync_WithExistingCategories_ShouldReturnNextOrder()
        {
            await SeedTestCategoriesAsync(3);
            ClearEntityTracking();

            var result = await _repository.GetNextSortOrderAsync();

            result.Should().Be(4);
        }

        /// <summary>
        /// Tests that GetNextSortOrderAsync returns 1 for empty database.
        /// </summary>
        [Fact]
        public async Task GetNextSortOrderAsync_WithEmptyDatabase_ShouldReturnOne()
        {
            ClearTestData();

            var result = await _repository.GetNextSortOrderAsync();

            result.Should().Be(1);
        }

        /// <summary>
        /// Tests that GetMaxSortOrderAsync returns the maximum sort order value.
        /// </summary>
        [Fact]
        public async Task GetMaxSortOrderAsync_WithMultipleCategories_ShouldReturnMaxOrder()
        {
            await SeedTestCategoriesAsync(3);
            ClearEntityTracking();

            var result = await _repository.GetMaxSortOrderAsync();

            result.Should().Be(3);
        }

        #endregion

        #region Name-Based Operations Tests

        /// <summary>
        /// Tests that GetByNameAsync returns a Category by its exact name.
        /// </summary>
        [Fact]
        public async Task GetByNameAsync_WithExistingCategoryName_ShouldReturnCategory()
        {
            var category = await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.GetByNameAsync(DEFAULT_CATEGORY_NAME);

            result.Should().NotBeNull();
            result?.Id.Should().Be(category.Id);
            result?.Name.Should().Be(DEFAULT_CATEGORY_NAME);
        }

        /// <summary>
        /// Tests that GetByNameAsync returns null for a non-existent category name.
        /// </summary>
        [Fact]
        public async Task GetByNameAsync_WithNonExistentCategoryName_ShouldReturnNull()
        {
            var result = await _repository.GetByNameAsync("NonExistentCategory");
            result.Should().BeNull();
        }

        /// <summary>
        /// Tests that ExistsByNameAsync returns true for an existing category name.
        /// </summary>
        [Fact]
        public async Task ExistsByNameAsync_WithExistingCategoryName_ShouldReturnTrue()
        {
            await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.ExistsByNameAsync(DEFAULT_CATEGORY_NAME);

            result.Should().BeTrue();
        }

        /// <summary>
        /// Tests that ExistsByNameAsync returns false for a non-existent category name.
        /// </summary>
        [Fact]
        public async Task ExistsByNameAsync_WithNonExistentCategoryName_ShouldReturnFalse()
        {
            var result = await _repository.ExistsByNameAsync("NonExistentCategory");
            result.Should().BeFalse();
        }

        #endregion

        #region Hierarchical Category Tests

        /// <summary>
        /// Tests that GetRootCategoriesAsync returns only categories with no parent.
        /// </summary>
        [Fact]
        public async Task GetRootCategoriesAsync_WithHierarchicalCategories_ShouldReturnRootCategories()
        {
            var rootCategory = await CreateTestCategory(1, "Root Category");
            var childCategory = await CreateTestCategory(2, "Child Category", rootCategory.Id);
            ClearEntityTracking();

            var result = await _repository.GetRootCategoriesAsync();

            result.Should().NotBeNull().And.HaveCount(1);
            result.Should().OnlyContain(c => c.ParentCategoryId == null);
            result[0].Name.Should().Be("Root Category");
        }

        /// <summary>
        /// Tests that GetChildCategoriesAsync returns only categories with the specified parent.
        /// </summary>
        [Fact]
        public async Task GetChildCategoriesAsync_WithParentCategory_ShouldReturnChildren()
        {
            var parentId = 100;
            var parentCategory = await CreateTestCategory(parentId, "Parent Category");
            var child1 = await CreateTestCategory(101, "Child 1", parentId);
            var child2 = await CreateTestCategory(102, "Child 2", parentId);
            await CreateTestCategory(200, "Other Category");
            ClearEntityTracking();

            var result = await _repository.GetChildCategoriesAsync(parentId);

            result.Should().NotBeNull().And.HaveCount(2);
            result.Should().OnlyContain(c => c.ParentCategoryId == parentId);
            result.Select(c => c.Name).Should().Contain("Child 1", "Child 2");
        }

        /// <summary>
        /// Tests that GetChildCategoriesAsync returns empty list for category with no children.
        /// </summary>
        [Fact]
        public async Task GetChildCategoriesAsync_WithNoChildren_ShouldReturnEmptyList()
        {
            var category = await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.GetChildCategoriesAsync(category.Id);

            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Category Creation Tests

        /// <summary>
        /// Tests that CreateWithOrderAsync creates a new category with the next sort order.
        /// </summary>
        [Fact]
        public async Task CreateWithOrderAsync_WithValidData_ShouldCreateCategory()
        {
            // No necesitamos limpiar datos ya que el constructor ya lo hizo
            var initialCount = await _dbContext.Categories.CountAsync();
            var expectedSortOrder = initialCount + 1;

            var result = await _repository.CreateWithOrderAsync(
                "New Category",
                "#FF0000",
                "New Description");

            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Name.Should().Be("New Category");
            result.Color.Should().Be("#FF0000");
            result.Description.Should().Be("New Description");
            result.SortOrder.Should().Be(expectedSortOrder);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Tests that CreateWithOrderAsync throws when creating a duplicate category.
        /// </summary>
        [Fact]
        public async Task CreateWithOrderAsync_WithDuplicateName_ShouldThrowException()
        {
            await CreateTestCategory(name: "Duplicate Category");
            ClearEntityTracking();

            Func<Task> act = async () => await _repository.CreateWithOrderAsync("Duplicate Category");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already exists*");
        }

        /// <summary>
        /// Tests that CreateWithOrderAsync generates unique sort orders for multiple creations.
        /// </summary>
        [Fact]
        public async Task CreateWithOrderAsync_MultipleCreations_ShouldGenerateSequentialSortOrders()
        {
            // Usar nombres únicos para evitar conflictos con datos existentes
            var baseName = $"TestCategory_{DateTime.UtcNow.Ticks}";

            var category1 = await _repository.CreateWithOrderAsync($"{baseName}_1");
            var category2 = await _repository.CreateWithOrderAsync($"{baseName}_2");
            var category3 = await _repository.CreateWithOrderAsync($"{baseName}_3");

            // Obtener el orden relativo basado en el orden de creación
            var ordered = await _repository.GetAllOrderedAsync();
            var testCategories = ordered
                .Where(c => c.Name.StartsWith(baseName))
                .OrderBy(c => c.Id)
                .ToList();

            testCategories.Should().HaveCount(3);

            // Los IDs deben ser consecutivos y los SortOrders también
            testCategories[0].SortOrder.Should().BeLessThan(testCategories[1].SortOrder);
            testCategories[1].SortOrder.Should().BeLessThan(testCategories[2].SortOrder);

            // Verificar que los nombres están en el orden correcto
            testCategories.Select(c => c.Name).Should().ContainInOrder(
                $"{baseName}_1",
                $"{baseName}_2",
                $"{baseName}_3"
            );
        }

        #endregion

        #region Reordering Tests

        /// <summary>
        /// Tests that ReorderAsync updates category sort orders correctly.
        /// </summary>
        [Fact]
        public async Task ReorderAsync_WithCategoryIds_ShouldUpdateSortOrders()
        {
            var categories = await SeedTestCategoriesAsync(3);
            var categoryIds = categories.Select(c => c.Id).Reverse().ToList();

            var result = await _repository.ReorderAsync(categoryIds);

            result.Should().Be(3);
            ClearEntityTracking();

            var updatedCategories = await _repository.GetAllOrderedAsync();
            updatedCategories.Select(c => c.Id).Should().Equal(categoryIds);
            updatedCategories.Select(c => c.SortOrder).Should().Equal(0, 1, 2);
        }

        /// <summary>
        /// Tests that ReorderAsync handles empty list gracefully.
        /// </summary>
        [Fact]
        public async Task ReorderAsync_WithEmptyList_ShouldReturnZero()
        {
            await SeedTestCategoriesAsync(3);

            var result = await _repository.ReorderAsync(new List<int>());

            result.Should().Be(0);
        }

        /// <summary>
        /// Tests that ReorderAsync handles null list gracefully.
        /// </summary>
        [Fact]
        public async Task ReorderAsync_WithNullList_ShouldReturnZero()
        {
            await SeedTestCategoriesAsync(3);

            var result = await _repository.ReorderAsync(null!);

            result.Should().Be(0);
        }

        /// <summary>
        /// Tests that ReorderAsync handles non-existent category IDs gracefully.
        /// </summary>
        /// <summary>
        /// Tests that ReorderAsync handles non-existent category IDs gracefully.
        /// </summary>
        /// <summary>
        /// Tests that ReorderAsync handles non-existent category IDs gracefully.
        /// </summary>
        [Fact]
        public async Task ReorderAsync_WithNonExistentIds_ShouldUpdateOnlyExisting()
        {
            // Asegurarnos de empezar limpio
            ClearTestData();

            // Crear categorías directamente con orden específico usando un método modificado
            var category1 = new Category
            {
                Id = 1,
                Name = "Category 1",
                Description = "Test Description 1",
                Color = "#0078D4",
                SortOrder = 100, // Valor alto para que cambie
                CreatedAt = DateTime.UtcNow
            };

            var category2 = new Category
            {
                Id = 2,
                Name = "Category 2",
                Description = "Test Description 2",
                Color = "#0078D4",
                SortOrder = 200, // Valor alto para que cambie
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Categories.AddRange(category1, category2);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var categoryIds = new List<int> { category2.Id, 999, category1.Id }; // Orden invertido

            var result = await _repository.ReorderAsync(categoryIds);

            // Ambas categorías deberían cambiar (de 100/200 a 0/2)
            result.Should().Be(2, "Both categories should change their sort order");
            ClearEntityTracking();

            var updatedCategories = await _repository.GetAllOrderedAsync();

            // Verificar el nuevo orden
            updatedCategories.Should().HaveCount(2);
            updatedCategories[0].Id.Should().Be(category2.Id);
            updatedCategories[0].SortOrder.Should().Be(0);

            // category1 está en índice 2 de la lista original (por el 999 en el medio)
            updatedCategories[1].Id.Should().Be(category1.Id);
            updatedCategories[1].SortOrder.Should().Be(2);
        }

        #endregion

        #region Unread Count Tests

        /// <summary>
        /// Tests that GetUnreadCountsByCategoryAsync returns correct unread counts.
        /// </summary>
        [Fact]
        public async Task GetUnreadCountsByCategoryAsync_WithArticles_ShouldReturnCorrectCounts()
        {
            ClearTestData();

            var category1 = await CreateTestCategory(1, "Category 1");
            var category2 = await CreateTestCategory(2, "Category 2");

            var feed1 = new Feed
            {
                Id = 1,
                Title = "Feed 1",
                Url = "http://feed1.com/rss",
                CategoryId = category1.Id,
                LastUpdated = DateTime.UtcNow
            };

            var feed2 = new Feed
            {
                Id = 2,
                Title = "Feed 2",
                Url = "http://feed2.com/rss",
                CategoryId = category2.Id,
                LastUpdated = DateTime.UtcNow
            };

            _dbContext.Feeds.AddRange(feed1, feed2);
            await _dbContext.SaveChangesAsync();

            var articles = new[]
            {
                new Article { FeedId = 1, Title = "Article 1", Status = Core.Enums.ArticleStatus.Unread },
                new Article { FeedId = 1, Title = "Article 2", Status = Core.Enums.ArticleStatus.Unread },
                new Article { FeedId = 1, Title = "Article 3", Status = Core.Enums.ArticleStatus.Read },
                new Article { FeedId = 2, Title = "Article 4", Status = Core.Enums.ArticleStatus.Unread },
                new Article { FeedId = 2, Title = "Article 5", Status = Core.Enums.ArticleStatus.Read },
                new Article { FeedId = 2, Title = "Article 6", Status = Core.Enums.ArticleStatus.Read }
            };

            _dbContext.Articles.AddRange(articles);
            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetUnreadCountsByCategoryAsync();

            result.Should().NotBeNull().And.HaveCount(2);
            result[category1.Id].Should().Be(2);
            result[category2.Id].Should().Be(1);
        }

        /// <summary>
        /// Tests that GetUnreadCountsByCategoryAsync returns empty dictionary for no unread articles.
        /// </summary>
        [Fact]
        public async Task GetUnreadCountsByCategoryAsync_WithNoUnreadArticles_ShouldReturnEmptyDictionary()
        {
            ClearTestData();

            var category = await CreateTestCategory();
            var feed = new Feed
            {
                Id = 1,
                Title = "Test Feed",
                Url = "http://test.com/rss",
                CategoryId = category.Id
            };

            _dbContext.Feeds.Add(feed);
            _dbContext.Articles.Add(new Article
            {
                FeedId = 1,
                Title = "Read Article",
                Status = Core.Enums.ArticleStatus.Read
            });

            await _dbContext.SaveChangesAsync();
            ClearEntityTracking();

            var result = await _repository.GetUnreadCountsByCategoryAsync();

            result.Should().NotBeNull().And.BeEmpty();
        }

        #endregion

        #region Performance Tests

        /// <summary>
        /// Tests that GetAllOrderedAsync uses AsNoTracking for performance.
        /// </summary>
        [Fact]
        public async Task GetAllOrderedAsync_ShouldUseNoTrackingForPerformance()
        {
            await SeedTestCategoriesAsync(10);
            ClearEntityTracking();

            var result = await _repository.GetAllOrderedAsync();

            result.Should().NotBeNull().And.HaveCount(10);
            _dbContext.ChangeTracker.Entries<Category>().Should().BeEmpty();
        }

        /// <summary>
        /// Tests that GetByNameAsync uses AsNoTracking for performance.
        /// </summary>
        [Fact]
        public async Task GetByNameAsync_ShouldUseNoTrackingForPerformance()
        {
            await CreateTestCategory();
            ClearEntityTracking();

            var result = await _repository.GetByNameAsync(DEFAULT_CATEGORY_NAME);

            result.Should().NotBeNull();
            _dbContext.ChangeTracker.Entries<Category>().Should().BeEmpty();
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests that UpdateAsync handles concurrency scenarios.
        /// </summary>
        [Fact]
        public async Task UpdateAsync_WithModifiedEntity_ShouldPersistChanges()
        {
            var category = await CreateTestCategory();
            var originalName = category.Name;

            category.Name = "Modified Name";
            var result = await _repository.UpdateAsync(category);

            result.Should().Be(1);
            ClearEntityTracking();

            var retrieved = await _repository.GetByIdAsync(category.Id);
            retrieved?.Name.Should().Be("Modified Name");
            retrieved?.Name.Should().NotBe(originalName);
        }

        #endregion

        #region IDisposable Implementation

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _mockLogger?.Reset();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}