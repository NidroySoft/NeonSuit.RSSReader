using FluentAssertions;
using Moq;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Services;
using System.Text;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Tests.Unit.Services
{
    public class OpmlServiceTests
    {
        private readonly Mock<IFeedService> _mockFeedService;
        private readonly Mock<ICategoryService> _mockCategoryService;
        private readonly Mock<Serilog.ILogger> _mockLogger;
        private readonly OpmlService _service;

        public OpmlServiceTests()
        {
            _mockFeedService = new Mock<IFeedService>();
            _mockCategoryService = new Mock<ICategoryService>();
            _mockLogger = new Mock<Serilog.ILogger>();

            _mockLogger.Setup(x => x.ForContext<OpmlService>())
                .Returns(_mockLogger.Object);

            _service = new OpmlService(_mockFeedService.Object, _mockCategoryService.Object, _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullFeedService_ShouldThrowArgumentNullException()
        {
            Action act = () => new OpmlService(null!, _mockCategoryService.Object, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("feedService");
        }

        [Fact]
        public void Constructor_WithNullCategoryService_ShouldThrowArgumentNullException()
        {
            Action act = () => new OpmlService(_mockFeedService.Object, null!, _mockLogger.Object);
            act.Should().Throw<ArgumentNullException>().WithParameterName("categoryService");
        }

        #endregion

        #region ImportAsync Tests

        [Fact]
        public async Task ImportAsync_WithValidOpml_ShouldImportFeedsSuccessfully()
        {
            // Arrange
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
        <opml version='2.0'>
            <head><title>Test OPML</title></head>
            <body>
                <outline text='Technology' title='Technology'>
                    <outline type='rss' text='Tech News' title='Tech News' xmlUrl='https://tech.com/rss'/>
                </outline>
            </body>
        </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            var category = new Category { Id = 1, Name = "Technology" };

            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Technology"))
                .ReturnsAsync(category);

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync("https://tech.com/rss", It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.IsAny<Feed>()))
                .ReturnsAsync(100);

            // Act
            var result = await _service.ImportAsync(stream, "Imported", false);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(1);
            result.FeedsSkipped.Should().Be(0);
            result.Errors.Should().BeEmpty();
            result.ImportedFeeds.Should().HaveCount(1);

            // ✅ Verificar que se llamó 2 veces (una para la categoría, otra para el feed)
            _mockCategoryService.Verify(x => x.GetOrCreateCategoryAsync("Technology"), Times.Exactly(2));
            _mockFeedService.Verify(x => x.CreateFeedAsync(It.IsAny<Feed>()), Times.Once);
        }

        [Fact]
        public async Task ImportAsync_WithExistingFeedAndNoOverwrite_ShouldSkipFeed()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <body>
                        <outline type='rss' text='Existing Feed' xmlUrl='https://existing.com/rss'/>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            var existingFeed = new Feed { Id = 1, Title = "Existing Feed", Url = "https://existing.com/rss" };

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(existingFeed);

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.FeedsImported.Should().Be(0);
            result.FeedsSkipped.Should().Be(1);
            result.Warnings.Should().Contain(w => w.Contains("Feed already exists"));
            _mockFeedService.Verify(x => x.UpdateFeedAsync(It.IsAny<Feed>()), Times.Never);
        }

        [Fact]
        public async Task ImportAsync_WithExistingFeedAndOverwrite_ShouldUpdateFeed()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <body>
                        <outline type='rss' text='Updated Feed' xmlUrl='https://existing.com/rss'/>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

            var existingFeed = new Feed { Id = 1, Title = "Old Title", Url = "https://existing.com/rss" };

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(existingFeed);

            _mockFeedService
                .Setup(x => x.UpdateFeedAsync(It.IsAny<Feed>()))
                .ReturnsAsync(true);

            var result = await _service.ImportAsync(stream, "Imported", true);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(1);
            result.ImportedFeeds.Should().Contain(f => !f.WasNew);
            _mockFeedService.Verify(x => x.UpdateFeedAsync(It.IsAny<Feed>()), Times.Once);
        }

        [Fact]
        public async Task ImportAsync_WithInvalidOpml_ShouldReturnErrorResult()
        {
            var invalidContent = "Not XML content";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidContent));

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Contains("Invalid OPML file"));
        }

        [Fact]
        public async Task ImportAsync_WhenFeedServiceThrows_ShouldCaptureErrorAndContinue()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <body>
                        <outline type='rss' text='Feed 1' xmlUrl='https://feed1.com/rss'/>
                        <outline type='rss' text='Feed 2' xmlUrl='https://feed2.com/rss'/>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync("https://feed1.com/rss", It.IsAny<bool>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync("https://feed2.com/rss", It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.Is<Feed>(f => f.Url == "https://feed2.com/rss")))
                .ReturnsAsync(2);

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Failed to import feed 'Feed 1'"));
            result.FeedsImported.Should().Be(1);
        }

        #endregion

        #region ImportFromFileAsync Tests

        [Fact]
        public async Task ImportFromFileAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
        {
            var nonExistentPath = @"C:\nonexistent\file.opml";

            Func<Task> act = async () => await _service.ImportFromFileAsync(nonExistentPath);

            var exceptionAssertion = await act.Should().ThrowAsync<FileNotFoundException>();
            exceptionAssertion.WithMessage("OPML file not found");
            exceptionAssertion.Where(ex => ex.FileName == nonExistentPath);
        }

        [Fact]
        public async Task ImportFromFileAsync_WithValidFile_ShouldImportSuccessfully()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                    <opml version='2.0'>
                        <body>
                            <outline type='rss' text='File Feed' xmlUrl='https://file.com/rss'/>
                        </body>
                    </opml>";

                await File.WriteAllTextAsync(tempFile, opmlContent, Encoding.UTF8);

                _mockCategoryService
                    .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                    .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

                _mockFeedService
                    .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                    .ReturnsAsync((Feed?)null);

                _mockFeedService
                    .Setup(x => x.CreateFeedAsync(It.IsAny<Feed>()))
                    .ReturnsAsync(1);

                var result = await _service.ImportFromFileAsync(tempFile, "Imported", false);

                result.Should().NotBeNull();
                result.Success.Should().BeTrue();
                result.FeedsImported.Should().Be(1);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        #endregion

        #region ExportAsync Tests
        [Fact]
        public async Task ExportAsync_WithIncludeCategories_ShouldReturnValidOpmlWithCategories()
        {
            // Arrange
            var feed = new Feed
            {
                Id = 100,
                Title = "Tech News",
                Url = "https://tech.com/rss",
                WebsiteUrl = "https://tech.com",
                Description = "Latest tech news",
                Language = "en",
                UpdateFrequency = FeedUpdateFrequency.EveryHour,
                IsActive = true,
                CategoryId = 1  // ✅ ¡Esto es crucial!
            };

            var category = new Category
            {
                Id = 1,
                Name = "Technology",
                Description = "Tech news"
                // No necesitas Feeds aquí porque el servicio usa feedsToExport
            };

            var categories = new List<Category> { category };
            var feeds = new List<Feed> { feed };

            _mockCategoryService
                .Setup(x => x.GetCategoryByIdAsync(1))
                .ReturnsAsync(category);

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            // Act
            var result = await _service.ExportAsync(includeCategories: true, includeInactive: false);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("<opml version=\"2.0\">");
            result.Should().Contain("Technology");
            result.Should().Contain("Tech News");
            result.Should().Contain("https://tech.com/rss");

            var doc = XDocument.Parse(result);
            var categoryOutline = doc.Descendants("outline")
                .FirstOrDefault(o => o.Attribute("text")?.Value == "Technology");
            categoryOutline.Should().NotBeNull();

            var feedOutline = categoryOutline?.Elements("outline").FirstOrDefault();
            feedOutline?.Attribute("text")?.Value.Should().Be("Tech News");
        }

        [Fact]
        public async Task ExportAsync_WithoutCategories_ShouldReturnFlatOpmlStructure()
        {
            var feeds = new List<Feed>
            {
                new Feed
                {
                    Id = 1,
                    Title = "News Feed",
                    Url = "https://news.com/rss",
                    IsActive = true
                }
            };

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var result = await _service.ExportAsync(includeCategories: false, includeInactive: false);

            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("News Feed");
            result.Should().Contain("https://news.com/rss");

            var doc = XDocument.Parse(result);
            var body = doc.Root?.Element("body");
            body.Should().NotBeNull();

            var outlines = body?.Elements("outline");
            outlines.Should().HaveCount(1);
            outlines?.First().Attribute("xmlUrl")?.Value.Should().Be("https://news.com/rss");
        }

        [Fact]
        public async Task ExportAsync_WithIncludeInactive_ShouldIncludeInactiveFeeds()
        {
            var feeds = new List<Feed>
            {
                new Feed { Id = 1, Title = "Active Feed", Url = "https://active.com/rss", IsActive = true },
                new Feed { Id = 2, Title = "Inactive Feed", Url = "https://inactive.com/rss", IsActive = false }
            };

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var result = await _service.ExportAsync(includeCategories: false, includeInactive: true);

            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Active Feed");
            result.Should().Contain("Inactive Feed");
        }

        [Fact]
        public async Task ExportAsync_WithoutIncludeInactive_ShouldExcludeInactiveFeeds()
        {
            var feeds = new List<Feed>
            {
                new Feed { Id = 1, Title = "Active Feed", Url = "https://active.com/rss", IsActive = true },
                new Feed { Id = 2, Title = "Inactive Feed", Url = "https://inactive.com/rss", IsActive = false }
            };

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var result = await _service.ExportAsync(includeCategories: false, includeInactive: false);

            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Active Feed");
            result.Should().NotContain("Inactive Feed");
        }

        [Fact]
        public async Task ExportAsync_WhenCategoryServiceThrows_ShouldPropagateException()
        {
            // Arrange
            var feeds = new List<Feed>
    {
        new Feed
        {
            Id = 1,
            Title = "Test Feed",
            Url = "https://test.com/rss",
            CategoryId = 1,  // ← Asegurar que tiene categoría
            IsActive = true
        }
    };

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var expectedException = new Exception("Database error");
            _mockCategoryService
                .Setup(x => x.GetCategoryByIdAsync(1))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                _service.ExportAsync(includeCategories: true));

            Assert.Equal("Database error", exception.Message);
        }

        #endregion

        #region ExportToFileAsync Tests

        [Fact]
        public async Task ExportToFileAsync_ShouldWriteOpmlContentToFile()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var feeds = new List<Feed>
                {
                    new Feed { Id = 1, Title = "Test Feed", Url = "https://test.com/rss", IsActive = true }
                };

                _mockFeedService
                    .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                    .ReturnsAsync(feeds);

                await _service.ExportToFileAsync(tempFile, includeCategories: false);

                var fileContent = await File.ReadAllTextAsync(tempFile, Encoding.UTF8);
                fileContent.Should().NotBeNullOrEmpty();
                fileContent.Should().Contain("Test Feed");
                fileContent.Should().Contain("https://test.com/rss");
                fileContent.Should().Contain("<opml");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ExportToFileAsync_WhenFileWriteFails_ShouldPropagateException()
        {
            var invalidPath = @"C:\invalid\path\file.opml";

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(new List<Feed>
                {
                    new Feed
                    {
                        Id = 1,
                        Title = "Test Feed",
                        Url = "https://test.com/rss",
                        IsActive = true
                    }
                });

            Func<Task> act = async () => await _service.ExportToFileAsync(invalidPath);

            await act.Should().ThrowAsync<Exception>();
        }

        #endregion

        #region ExportCategoriesAsync Tests

        [Fact]
        public async Task ExportCategoriesAsync_WithCategoryIds_ShouldReturnCategorySpecificOpml()
        {
            var categoryIds = new List<int> { 1, 3 };

            var category1 = new Category
            {
                Id = 1,
                Name = "Category 1",
                Description = "First category",
                Feeds = new List<Feed>
                {
                    new Feed { Id = 10, Title = "Feed 1.1", Url = "https://feed11.com/rss", IsActive = true },
                    new Feed { Id = 11, Title = "Feed 1.2", Url = "https://feed12.com/rss", IsActive = true }
                }
            };

            var category3 = new Category
            {
                Id = 3,
                Name = "Category 3",
                Description = "Third category",
                Feeds = new List<Feed>
                {
                    new Feed { Id = 30, Title = "Feed 3.1", Url = "https://feed31.com/rss", IsActive = true }
                }
            };

            _mockCategoryService
                .Setup(x => x.GetCategoryWithFeedsAsync(1))
                .ReturnsAsync(category1);

            _mockCategoryService
                .Setup(x => x.GetCategoryWithFeedsAsync(2))
                .ReturnsAsync((Category?)null);

            _mockCategoryService
                .Setup(x => x.GetCategoryWithFeedsAsync(3))
                .ReturnsAsync(category3);

            var result = await _service.ExportCategoriesAsync(categoryIds);

            result.Should().NotBeNullOrEmpty();

            var doc = XDocument.Parse(result);
            var outlines = doc.Root?.Element("body")?.Elements("outline");
            outlines.Should().HaveCount(2);
            outlines.Should().Contain(o => (string?)o.Attribute("text") == "Category 1");
            outlines.Should().Contain(o => (string?)o.Attribute("text") == "Category 3");
            outlines.Should().NotContain(o => (string?)o.Attribute("text") == "Category 2");
        }

        [Fact]
        public async Task ExportCategoriesAsync_WithNonExistentCategory_ShouldSkipCategory()
        {
            var categoryIds = new List<int> { 999 };

            _mockCategoryService
                .Setup(x => x.GetCategoryWithFeedsAsync(999))
                .ReturnsAsync((Category?)null);

            var result = await _service.ExportCategoriesAsync(categoryIds);

            result.Should().NotBeNullOrEmpty();

            var doc = XDocument.Parse(result);
            var body = doc.Root?.Element("body");
            body?.Should().NotBeNull();
            body?.HasElements.Should().BeFalse();
        }

        #endregion

        #region ValidateAsync Tests

        [Fact]
        public async Task ValidateAsync_WithValidOpml_ShouldReturnValidResult()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <head><title>Test</title></head>
                    <body>
                        <outline text='Category'>
                            <outline type='rss' text='Feed 1' xmlUrl='https://feed1.com/rss'/>
                            <outline type='rss' text='Feed 2' xmlUrl='https://feed2.com/rss'/>
                        </outline>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            var result = await _service.ValidateAsync(stream);

            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.FeedCount.Should().Be(2);
            result.DetectedCategories.Should().Contain("Category");
            result.OpmlVersion.Should().Be("2.0");
            result.ErrorMessage.Should().BeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateAsync_WithNonOpmlXml_ShouldReturnInvalidResult()
        {
            // Arrange
            var nonOpmlContent = @"<?xml version='1.0'?>
        <notopml>
            <item>Test</item>
        </notopml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(nonOpmlContent));

            // Act
            var result = await _service.ValidateAsync(stream);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();

            // ✅ Usar el mensaje real que devuelve el servicio
            result.ErrorMessage.Should().Contain("Root element must be 'opml'");
        }

        [Fact]
        public async Task ValidateAsync_WithMalformedXml_ShouldReturnInvalidResult()
        {
            var malformedContent = "This is not XML at all";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformedContent));

            var result = await _service.ValidateAsync(stream);

            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.ErrorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task ValidateAsync_ShouldCorrectlyCountFeedsAndCategories()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='1.0'>
                    <body>
                        <outline text='Tech'>
                            <outline type='rss' text='Feed A' xmlUrl='https://a.com/rss'/>
                            <outline type='rss' text='Feed B' xmlUrl='https://b.com/rss'/>
                        </outline>
                        <outline text='News'>
                            <outline type='rss' text='Feed C' xmlUrl='https://c.com/rss'/>
                        </outline>
                        <outline text='Empty Category'>
                            <outline text='Subcategory'/>
                        </outline>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            var result = await _service.ValidateAsync(stream);

            result.Should().NotBeNull();
            result.FeedCount.Should().Be(3);
            result.DetectedCategories.Should().Contain("Tech");
            result.DetectedCategories.Should().Contain("News");
            result.DetectedCategories.Should().Contain("Empty Category");
            result.DetectedCategories.Should().HaveCount(4);
            result.OpmlVersion.Should().Be("1.0");
        }

        #endregion

        #region GetStatistics Tests

        [Fact]
        public async Task GetStatistics_AfterSuccessfulImport_ShouldReflectUpdatedStats()
        {
            // Arrange
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
        <opml version='2.0'>
            <body>
                <outline type='rss' text='Feed 1' xmlUrl='https://feed1.com/rss'/>
                <outline type='rss' text='Feed 2' xmlUrl='https://feed2.com/rss'/>
            </body>
        </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            // Configurar mocks para categoría
            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

            // Configurar mocks para feeds
            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);  // Los feeds no existen

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.IsAny<Feed>()))
                .ReturnsAsync(1);  // Devuelve ID 1 para el primer feed

            _mockFeedService
                .Setup(x => x.UpdateFeedAsync(It.IsAny<Feed>()))
                .ReturnsAsync(true);

            // Obtener estadísticas iniciales
            var initialStats = _service.GetStatistics();
            var initialImports = initialStats.TotalImports;
            var initialFeedsImported = initialStats.TotalFeedsImported;

            // Act - Realizar importación
            var importResult = await _service.ImportAsync(stream, "Imported", false);

            // Obtener estadísticas actualizadas
            var updatedStats = _service.GetStatistics();

            // Assert
            importResult.Success.Should().BeTrue();
            updatedStats.Should().NotBeNull();
            updatedStats.TotalImports.Should().Be(initialImports + 1);
            updatedStats.TotalFeedsImported.Should().Be(initialFeedsImported + 2);
            updatedStats.LastImport.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            updatedStats.FailedImports.Should().Be(initialStats.FailedImports);
        }

        [Fact]
        public async Task GetStatistics_AfterFailedImport_ShouldIncrementFailedImports()
        {
            var invalidContent = "Not valid OPML";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(invalidContent));

            var initialFailedImports = _service.GetStatistics().FailedImports;

            var result = await _service.ImportAsync(stream, "Imported", false);
            var updatedStats = _service.GetStatistics();

            result.Success.Should().BeFalse();
            updatedStats.FailedImports.Should().Be(initialFailedImports + 1);
            updatedStats.TotalImports.Should().Be(initialFailedImports + 1);
        }

        [Fact]
        public async Task GetStatistics_AfterExport_ShouldUpdateExportStats()
        {
            // Arrange
            var feeds = new List<Feed>(); // Lista vacía pero NO null

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);  // ✅ Mock necesario

            _mockCategoryService
                .Setup(x => x.GetAllCategoriesWithFeedsAsync())
                .ReturnsAsync(new List<Category>());

            var initialTotalExports = _service.GetStatistics().TotalExports;

            // Act
            await _service.ExportAsync();

            var updatedStats = _service.GetStatistics();

            // Assert
            updatedStats.TotalExports.Should().Be(initialTotalExports + 1);
            updatedStats.LastExport.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        #endregion

        #region Integration-style Tests

        [Fact]
        public async Task ImportThenExport_ShouldProduceConsistentResults()
        {
            // Arrange
            var originalOpml = @"<?xml version='1.0' encoding='UTF-8'?>
        <opml version='2.0'>
            <head>
                <title>Original Export</title>
                <dateCreated>2024-01-01T00:00:00Z</dateCreated>
            </head>
            <body>
                <outline text='Technology' title='Technology'>
                    <outline type='rss' text='TechCrunch' xmlUrl='https://techcrunch.com/feed'/>
                </outline>
            </body>
        </opml>";

            using var importStream = new MemoryStream(Encoding.UTF8.GetBytes(originalOpml));

            var category = new Category { Id = 1, Name = "Technology" };
            var feed = new Feed
            {
                Id = 100,
                Title = "TechCrunch",
                Url = "https://techcrunch.com/feed",
                WebsiteUrl = "https://techcrunch.com",
                IsActive = true,
                CategoryId = 1
            };

            // Setup para import
            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Technology"))
                .ReturnsAsync(category);

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.IsAny<Feed>()))
                .Callback<Feed>(f => f.Id = 100)
                .ReturnsAsync(100);

            // Setup para export
            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(new List<Feed> { feed });

            _mockCategoryService
                .Setup(x => x.GetCategoryByIdAsync(1))
                .ReturnsAsync(category);

            // Act - Import
            var importResult = await _service.ImportAsync(importStream, "Imported", false);

            // Act - Export
            var exportResult = await _service.ExportAsync(includeCategories: true);

            // Assert
            importResult.Success.Should().BeTrue();
            exportResult.Should().NotBeNullOrEmpty();
            exportResult.Should().Contain("Technology");
            exportResult.Should().Contain("TechCrunch");
            exportResult.Should().Contain("https://techcrunch.com/feed");

            var exportDoc = XDocument.Parse(exportResult);
            exportDoc.Root?.Name.Should().Be(XName.Get("opml"));
            exportDoc.Root?.Attribute("version")?.Value.Should().Be("2.0");

            var outlines = exportDoc.Descendants("outline").ToList();
            outlines.Should().HaveCount(2);

            var categoryOutline = outlines.First(o => o.Attribute("text")?.Value == "Technology");
            categoryOutline.Should().NotBeNull();

            var feedOutline = outlines.First(o => o.Attribute("xmlUrl")?.Value == "https://techcrunch.com/feed");
            feedOutline.Should().NotBeNull();
            feedOutline?.Attribute("text")?.Value.Should().Be("TechCrunch");
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task ImportAsync_WithEmptyOpml_ShouldReturnZeroImports()
        {
            var emptyOpml = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <head><title>Empty</title></head>
                    <body>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(emptyOpml));

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(0);
            result.TotalFeedsFound.Should().Be(0);
            result.CategoriesCreated.Should().Be(0);
        }

        [Fact]
        public async Task ImportAsync_WithMissingTitle_ShouldUseTextAttribute()
        {
            var opmlContent = @"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <body>
                        <outline type='rss' text='Feed Text' xmlUrl='https://feed.com/rss'/>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.Is<Feed>(f => f.Title == "Feed Text")))
                .ReturnsAsync(1);

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(1);
        }

        [Theory]
        [InlineData("xmlUrl", "https://feed.com/rss")]
        [InlineData("url", "https://feed.com/atom")]
        public async Task ImportAsync_WithDifferentUrlAttributes_ShouldHandleBoth(string attributeName, string feedUrl)
        {
            var opmlContent = $@"<?xml version='1.0' encoding='UTF-8'?>
                <opml version='2.0'>
                    <body>
                        <outline type='rss' text='Test Feed' {attributeName}='{feedUrl}'/>
                    </body>
                </opml>";

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(opmlContent));

            _mockCategoryService
                .Setup(x => x.GetOrCreateCategoryAsync("Imported"))
                .ReturnsAsync(new Category { Id = 1, Name = "Imported" });

            _mockFeedService
                .Setup(x => x.GetFeedByUrlAsync(feedUrl, It.IsAny<bool>()))
                .ReturnsAsync((Feed?)null);

            _mockFeedService
                .Setup(x => x.CreateFeedAsync(It.Is<Feed>(f => f.Url == feedUrl)))
                .ReturnsAsync(1);

            var result = await _service.ImportAsync(stream, "Imported", false);

            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(1);
        }

        [Fact]
        public async Task ExportAsync_WithFeedsHavingNullProperties_ShouldExportWithoutErrors()
        {
            var feeds = new List<Feed>
            {
                new Feed
                {
                    Id = 1,
                    Title = "Feed with nulls",
                    Url = "https://feed.com/rss",
                    WebsiteUrl = null!,
                    Description = null,
                    Language = "",
                    IsActive = true
                }
            };

            _mockFeedService
                .Setup(x => x.GetAllFeedsAsync(It.IsAny<bool>()))
                .ReturnsAsync(feeds);

            var result = await _service.ExportAsync(includeCategories: false);

            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("Feed with nulls");
            result.Should().Contain("https://feed.com/rss");

            var doc = XDocument.Parse(result);
            doc.Should().NotBeNull();
        }

        #endregion
    }
}