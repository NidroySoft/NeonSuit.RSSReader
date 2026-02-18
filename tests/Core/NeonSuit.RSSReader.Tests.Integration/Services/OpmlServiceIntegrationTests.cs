using FluentAssertions;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Repositories;
using NeonSuit.RSSReader.Services;
using NeonSuit.RSSReader.Services.FeedParser;
using NeonSuit.RSSReader.Tests.Integration.Factories;
using NeonSuit.RSSReader.Tests.Integration.Fixtures;
using Serilog;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace NeonSuit.RSSReader.Tests.Integration.Services;

[Collection("Integration Tests")]
public class OpmlServiceIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _dbFixture;
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private readonly ServiceFactory _factory;

    private IOpmlService _opmlService = null!;
    private IFeedService _feedService = null!;
    private ICategoryService _categoryService = null!;

    private Category _testCategory = null!;
    private Feed _testFeed1 = null!;
    private Feed _testFeed2 = null!;
    private Feed _testFeedInactive = null!;

    public OpmlServiceIntegrationTests(DatabaseFixture dbFixture, ITestOutputHelper output)
    {
        _dbFixture = dbFixture;
        _output = output;
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TestOutput(output)
            .CreateLogger()
            .ForContext<OpmlServiceIntegrationTests>();

        _factory = new ServiceFactory(_dbFixture);
    }

    public async Task InitializeAsync()
    {
        var dbContext = _dbFixture.CreateNewDbContext();

        var feedRepo = new FeedRepository(dbContext, _logger);
        var articleRepo = new ArticleRepository(dbContext, _logger);
        var categoryRepo = new CategoryRepository(dbContext, _logger);
        var parser = new RssFeedParser(_logger);

        _feedService = new FeedService(feedRepo, articleRepo, parser, _logger);
        _categoryService = new CategoryService(categoryRepo, feedRepo, _logger);
        _opmlService = new OpmlService(_feedService, _categoryService, _logger);

        await SetupTestDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Test Data Setup

    private async Task SetupTestDataAsync()
    {
        _testCategory = await _categoryService.CreateCategoryAsync(
            "Test Category", "#FF5733", "Test Description");

        _testFeed1 = new Feed
        {
            Title = "TechCrunch",
            Url = "https://techcrunch.com/feed/",
            WebsiteUrl = "https://techcrunch.com",
            Description = "Tech news",
            CategoryId = _testCategory.Id,
            IsActive = true,
            UpdateFrequency = Core.Enums.FeedUpdateFrequency.EveryHour,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        _testFeed2 = new Feed
        {
            Title = "BBC News",
            Url = "http://feeds.bbci.co.uk/news/rss.xml",
            WebsiteUrl = "https://www.bbc.com/news",
            Description = "World news",
            CategoryId = _testCategory.Id,
            IsActive = true,
            UpdateFrequency = Core.Enums.FeedUpdateFrequency.Every3Hours,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        _testFeedInactive = new Feed
        {
            Title = "Inactive Feed",
            Url = "https://example.com/inactive.xml",
            WebsiteUrl = "https://example.com",
            Description = "This feed is inactive",
            CategoryId = null,
            IsActive = false,
            UpdateFrequency = Core.Enums.FeedUpdateFrequency.Daily,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        var id1 = await _feedService.CreateFeedAsync(_testFeed1);
        var id2 = await _feedService.CreateFeedAsync(_testFeed2);
        var id3 = await _feedService.CreateFeedAsync(_testFeedInactive);

        // ✅ Verificar incluyendo inactivos
        var createdInactive = await _feedService.GetFeedByUrlAsync(_testFeedInactive.Url, true);
        if (createdInactive == null)
        {
            throw new InvalidOperationException("Failed to create inactive feed in test setup");
        }

        // ✅ Verificar activos (opcional)
        var createdActive1 = await _feedService.GetFeedByUrlAsync(_testFeed1.Url, true);
        var createdActive2 = await _feedService.GetFeedByUrlAsync(_testFeed2.Url, true);
    }

    #endregion

    #region Helper Methods

    private async Task<Category?> GetCategoryByName(string name)
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return categories.FirstOrDefault(c => c.Name == name);
    }

    private bool HasAttributeWithValue(XElement element, string attributeName, string value)
    {
        var attr = element.Attribute(attributeName);
        return attr != null && attr.Value == value;
    }

    private string CreateSampleOpml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <head>
    <title>Sample OPML</title>
  </head>
  <body>
    <outline text=""Technology"" title=""Technology"">
      <outline type=""rss"" text=""TechCrunch"" title=""TechCrunch"" xmlUrl=""https://techcrunch.com/feed/"" htmlUrl=""https://techcrunch.com""/>
      <outline type=""rss"" text=""Wired"" title=""Wired"" xmlUrl=""https://www.wired.com/feed/rss"" htmlUrl=""https://www.wired.com""/>
    </outline>
    <outline text=""News"" title=""News"">
      <outline type=""rss"" text=""BBC"" title=""BBC"" xmlUrl=""http://feeds.bbci.co.uk/news/rss.xml"" htmlUrl=""https://www.bbc.com/news""/>
    </outline>
    <outline type=""rss"" text=""Uncategorized Feed"" title=""Uncategorized Feed"" xmlUrl=""https://example.com/feed.xml"" htmlUrl=""https://example.com""/>
  </body>
</opml>";
    }

    private string CreateSampleOpmlWithExisting(string existingUrl)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <head>
    <title>Sample OPML with Existing</title>
  </head>
  <body>
    <outline text=""Test Category"" title=""Test Category"">
      <outline type=""rss"" text=""TechCrunch"" title=""TechCrunch"" xmlUrl=""{existingUrl}"" htmlUrl=""https://techcrunch.com""/>
    </outline>
    <outline type=""rss"" text=""New Feed"" title=""New Feed"" xmlUrl=""https://newsite.com/feed.xml"" htmlUrl=""https://newsite.com""/>
  </body>
</opml>";
    }

    private string CreateSampleOpmlWithOverwrite(string url)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <head>
    <title>Sample OPML for Overwrite</title>
  </head>
  <body>
    <outline text=""Updated Category"" title=""Updated Category"">
      <outline type=""rss"" text=""Updated Title"" title=""Updated Title"" xmlUrl=""{url}"" htmlUrl=""https://updated.com""/>
    </outline>
  </body>
</opml>";
    }

    private string CreateNestedCategoriesOpml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <head>
    <title>Nested Categories</title>
  </head>
  <body>
    <outline text=""Root"" title=""Root"">
      <outline text=""Child 1"" title=""Child 1"">
        <outline text=""Grandchild"" title=""Grandchild"">
          <outline type=""rss"" text=""Deep Feed"" title=""Deep Feed"" xmlUrl=""https://deep.com/feed.xml"" htmlUrl=""https://deep.com""/>
        </outline>
      </outline>
      <outline text=""Child 2"" title=""Child 2"">
        <outline type=""rss"" text=""Child Feed"" title=""Child Feed"" xmlUrl=""https://child.com/feed.xml"" htmlUrl=""https://child.com""/>
      </outline>
    </outline>
  </body>
</opml>";
    }

    private string CreateInvalidOpml()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<not-opml>
  <not-body>
    <invalid>This is not valid OPML</invalid>
  </not-body>
</not-opml>";
    }

    private string CreateOpmlWithoutBody()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <head>
    <title>No Body OPML</title>
  </head>
</opml>";
    }

    private Stream CreateStreamFromString(string content)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    #endregion

    #region OPML Export Tests

    [Fact]
    public async Task ExportAsync_WithCategories_ShouldGenerateValidOpml()
    {
        var result = await _opmlService.ExportAsync(includeCategories: true, includeInactive: false);

        result.Should().NotBeNullOrEmpty();

        var doc = XDocument.Parse(result);
        doc.Root?.Name.LocalName.Should().Be("opml");
        doc.Root?.Attribute("version")?.Value.Should().Be("2.0");

        var body = doc.Root?.Element("body");
        body.Should().NotBeNull();

        var categoryOutlines = body?.Elements("outline")
            .Where(o => o.Attribute("xmlUrl") == null)
            .ToList();

        categoryOutlines.Should().ContainSingle();
        categoryOutlines![0].Attribute("text")?.Value.Should().Be("Test Category");

        var feedOutlines = categoryOutlines[0].Elements("outline").ToList();
        feedOutlines.Should().HaveCount(2);
        feedOutlines.Select(o => o.Attribute("text")?.Value)
            .Should().Contain(new[] { "TechCrunch", "BBC News" });

        var allFeeds = body?.Descendants("outline")
            .Where(o => o.Attribute("xmlUrl") != null)
            .ToList();
        allFeeds.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExportAsync_WithoutCategories_ShouldGenerateFlatOpml()
    {
        var result = await _opmlService.ExportAsync(includeCategories: false, includeInactive: false);

        result.Should().NotBeNullOrEmpty();

        var doc = XDocument.Parse(result);
        var body = doc.Root?.Element("body");
        body.Should().NotBeNull();

        var feedOutlines = body?.Elements("outline")
            .Where(o => o.Attribute("xmlUrl") != null)
            .ToList();

        feedOutlines.Should().HaveCount(2);
        feedOutlines?.All(o => o.Attribute("type")?.Value == "rss").Should().BeTrue();

        var categoryOutlines = body?.Elements("outline")
            .Where(o => o.Attribute("xmlUrl") == null)
            .ToList();
        categoryOutlines.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_WithInactiveFeeds_ShouldIncludeThemWhenRequested()
    {
        var result = await _opmlService.ExportAsync(includeCategories: true, includeInactive: true);

        result.Should().NotBeNullOrEmpty();

        var doc = XDocument.Parse(result);
        var body = doc.Root?.Element("body");
        body.Should().NotBeNull();

        var inactiveFeedOutline = body?.Descendants("outline")
            .FirstOrDefault(o => o.Attribute("text")?.Value == "Inactive Feed");

        inactiveFeedOutline.Should().NotBeNull();
        inactiveFeedOutline?.Attribute("xmlUrl")?.Value.Should().Be(_testFeedInactive.Url);
    }

    [Fact]
    public async Task ExportCategoriesAsync_WithSpecificCategoryIds_ShouldExportOnlyThoseCategories()
    {
        var category2 = await _categoryService.CreateCategoryAsync("Second Category", "#00FF00", "Another category");
        var feed3 = new Feed
        {
            Title = "Feed 3",
            Url = "https://feed3.com/rss",
            WebsiteUrl = "https://feed3.com",
            CategoryId = category2.Id,
            IsActive = true
        };
        await _feedService.CreateFeedAsync(feed3);

        var result = await _opmlService.ExportCategoriesAsync(new[] { _testCategory.Id });

        result.Should().NotBeNullOrEmpty();

        var doc = XDocument.Parse(result);
        var body = doc.Root?.Element("body");
        body.Should().NotBeNull();

        var categoryOutlines = body?.Elements("outline")
            .Where(o => o.Attribute("xmlUrl") == null)
            .ToList();

        categoryOutlines.Should().ContainSingle();
        categoryOutlines![0].Attribute("text")?.Value.Should().Be("Test Category");
        categoryOutlines.Any(o => o.Attribute("text")?.Value == "Second Category").Should().BeFalse();
    }

    [Fact]
    public async Task ExportToFileAsync_ShouldCreateFileWithOpmlContent()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            await _opmlService.ExportToFileAsync(tempFile, includeCategories: true, includeInactive: false);

            File.Exists(tempFile).Should().BeTrue();

            var fileContent = await File.ReadAllTextAsync(tempFile);
            fileContent.Should().NotBeNullOrEmpty();

            var doc = XDocument.Parse(fileContent);
            doc.Root?.Name.LocalName.Should().Be("opml");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    #endregion

    #region OPML Import Tests

    [Fact]
    public async Task ImportAsync_WithValidOpmlStream_ShouldImportFeeds()
    {
        using var stream = CreateStreamFromString(CreateSampleOpml());

        var result = await _opmlService.ImportAsync(stream, defaultCategory: "Imported", overwriteExisting: false);

        result.Success.Should().BeTrue();
        result.TotalFeedsFound.Should().Be(4); // ✅ Cambiar a 4
        result.FeedsImported.Should().Be(2);   // ✅ Solo los nuevos
        result.FeedsSkipped.Should().Be(2);    // ✅ Los existentes
        result.CategoriesCreated.Should().BeGreaterThan(0);
        result.Errors.Should().BeEmpty();
        result.ImportedFeeds.Should().HaveCount(2); // ✅ Solo los nuevos

        var techCategory = await GetCategoryByName("Technology");
        techCategory.Should().NotBeNull();

        var newsCategory = await GetCategoryByName("News");
        newsCategory.Should().NotBeNull();

        // Verificar que los feeds nuevos se importaron
        var wiredFeed = await _feedService.GetFeedByUrlAsync("https://www.wired.com/feed/rss");
        wiredFeed.Should().NotBeNull();
        wiredFeed?.CategoryId.Should().Be(techCategory?.Id);

        var uncategorizedFeed = await _feedService.GetFeedByUrlAsync("https://example.com/feed.xml");
        uncategorizedFeed.Should().NotBeNull();
        uncategorizedFeed?.CategoryId.Should().Be(4); // Categoría "Imported" tiene ID 4

        // Verificar que los feeds existentes NO cambiaron
        var techCrunchFeed = await _feedService.GetFeedByUrlAsync("https://techcrunch.com/feed/");
        techCrunchFeed.Should().NotBeNull();
        techCrunchFeed?.Title.Should().Be("TechCrunch"); // Título original

        var bbcFeed = await _feedService.GetFeedByUrlAsync("http://feeds.bbci.co.uk/news/rss.xml");
        bbcFeed.Should().NotBeNull();
        bbcFeed?.Title.Should().Be("BBC News"); // Título original
    }

    [Fact]
    public async Task ImportAsync_WithExistingFeeds_ShouldSkipWhenNotOverwrite()
    {
        using var stream = CreateStreamFromString(CreateSampleOpmlWithExisting(_testFeed1.Url));

        var result = await _opmlService.ImportAsync(stream, defaultCategory: "Imported", overwriteExisting: false);

        result.Success.Should().BeTrue();
        result.TotalFeedsFound.Should().Be(2);
        result.FeedsImported.Should().Be(1);
        result.FeedsSkipped.Should().Be(1);
        result.Warnings.Should().Contain(w => w.Contains("already exists"));

        var existingFeed = await _feedService.GetFeedByUrlAsync(_testFeed1.Url);
        existingFeed?.Title.Should().Be("TechCrunch");

        var newFeed = await _feedService.GetFeedByUrlAsync("https://newsite.com/feed.xml");
        newFeed.Should().NotBeNull();
    }

    [Fact]
    public async Task ImportAsync_WithExistingFeeds_ShouldOverwriteWhenRequested()
    {
        using var stream = CreateStreamFromString(CreateSampleOpmlWithOverwrite(_testFeed1.Url));

        var result = await _opmlService.ImportAsync(stream, defaultCategory: "Imported", overwriteExisting: true);

        result.Success.Should().BeTrue();
        result.TotalFeedsFound.Should().Be(1);
        result.FeedsImported.Should().Be(1);
        result.FeedsSkipped.Should().Be(0);

        var existingFeed = await _feedService.GetFeedByUrlAsync(_testFeed1.Url);
        existingFeed?.Title.Should().Be("Updated Title");
        existingFeed?.WebsiteUrl.Should().Be("https://updated.com");

        var updatedCategory = await GetCategoryByName("Updated Category");
        updatedCategory.Should().NotBeNull();
        existingFeed?.CategoryId.Should().Be(updatedCategory?.Id);
    }

    [Fact]
    public async Task ImportFromFileAsync_WithValidFile_ShouldImportFeeds()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, CreateSampleOpml());

        try
        {
            // Act
            var result = await _opmlService.ImportFromFileAsync(tempFile, defaultCategory: "Imported", overwriteExisting: false);

            // Assert
            result.Success.Should().BeTrue();
            result.FeedsImported.Should().Be(2); // ✅ Solo los nuevos
            result.FeedsSkipped.Should().Be(2);   // ✅ Los existentes
            result.TotalFeedsFound.Should().Be(4);

            // Verificar que los nuevos feeds existen
            var wiredFeed = await _feedService.GetFeedByUrlAsync("https://www.wired.com/feed/rss");
            wiredFeed.Should().NotBeNull();

            var uncategorizedFeed = await _feedService.GetFeedByUrlAsync("https://example.com/feed.xml");
            uncategorizedFeed.Should().NotBeNull();
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
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".opml");

        Func<Task> act = async () => await _opmlService.ImportFromFileAsync(nonExistentFile);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    #endregion

    #region OPML Validation Tests

    [Fact]
    public async Task ValidateAsync_WithValidOpml_ShouldReturnValid()
    {
        using var stream = CreateStreamFromString(CreateSampleOpml());

        var result = await _opmlService.ValidateAsync(stream);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.FeedCount.Should().Be(4); // ✅ Cambiar a 4
        result.DetectedCategories.Should().HaveCount(2);
        result.DetectedCategories.Should().Contain(new[] { "Technology", "News" });
        result.OpmlVersion.Should().Be("2.0");
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidXml_ShouldReturnInvalid()
    {
        using var stream = CreateStreamFromString("This is not XML at all");

        var result = await _opmlService.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithOpmlWithoutRoot_ShouldReturnInvalid()
    {
        using var stream = CreateStreamFromString(CreateInvalidOpml());

        var result = await _opmlService.ValidateAsync(stream);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Root element");
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetStatistics_AfterImport_ShouldUpdateStats()
    {
        // Arrange
        var opmlService = _factory.CreateFreshOpmlService();

        // Act
        using var stream = CreateStreamFromString(CreateSampleOpml());
        await opmlService.ImportAsync(stream);

        var afterImportStats = opmlService.GetStatistics();

        // Assert
        afterImportStats.TotalImports.Should().Be(1);
        afterImportStats.TotalFeedsImported.Should().Be(4);
        afterImportStats.LastImport.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        afterImportStats.FailedImports.Should().Be(0);
    }
    [Fact]
    public async Task GetStatistics_AfterExport_ShouldUpdateStats()
    {
        // Arrange
        var opmlService = _factory.CreateFreshOpmlService();

        // Act
        await opmlService.ExportAsync();

        var afterExportStats = opmlService.GetStatistics();

        // Assert
        afterExportStats.TotalExports.Should().Be(1); // Primera exportación
        afterExportStats.LastExport.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStatistics_AfterFailedImport_ShouldNotUpdateFailedCount()
    {
        // Arrange
        var opmlService = _factory.CreateFreshOpmlService();

        // Act
        using var stream = CreateStreamFromString("Invalid content");
        var result = await opmlService.ImportAsync(stream);

        var afterFailedStats = opmlService.GetStatistics();

        // Assert
        result.Success.Should().BeFalse();
        afterFailedStats.TotalImports.Should().Be(1);
        afterFailedStats.FailedImports.Should().Be(1); // ✅ Comportamiento actualizado
        afterFailedStats.TotalFeedsImported.Should().Be(0);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ImportAsync_WithEmptyStream_ShouldReturnError()
    {
        using var emptyStream = new MemoryStream();

        var result = await _opmlService.ImportAsync(emptyStream);

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ImportAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        Func<Task> act = async () => await _opmlService.ImportAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ImportAsync_WithOpmlWithoutBody_ShouldReturnError()
    {
        using var stream = CreateStreamFromString(CreateOpmlWithoutBody());

        var result = await _opmlService.ImportAsync(stream);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("body element"));
    }

    [Fact]
    public async Task ExportCategoriesAsync_WithEmptyCategoryList_ShouldReturnEmptyOpml()
    {
        var result = await _opmlService.ExportCategoriesAsync(Array.Empty<int>());

        result.Should().NotBeNullOrEmpty();

        var doc = XDocument.Parse(result);
        var body = doc.Root?.Element("body");
        body?.Elements().Should().BeEmpty();
    }

    #endregion

    #region Complex OPML Structure Tests

    [Fact]
    public async Task ImportAsync_WithNestedCategories_ShouldPreserveHierarchy()
    {
        using var stream = CreateStreamFromString(CreateNestedCategoriesOpml());

        var result = await _opmlService.ImportAsync(stream);

        result.Success.Should().BeTrue();
        result.FeedsImported.Should().Be(2);

        var rootCategory = await GetCategoryByName("Root");
        rootCategory.Should().NotBeNull();

        var child1 = await GetCategoryByName("Child 1");
        child1.Should().NotBeNull();
        child1?.ParentCategoryId.Should().Be(rootCategory?.Id);

        var grandchild = await GetCategoryByName("Grandchild");
        grandchild.Should().NotBeNull();
        grandchild?.ParentCategoryId.Should().Be(child1?.Id);

        var child2 = await GetCategoryByName("Child 2");
        child2.Should().NotBeNull();
        child2?.ParentCategoryId.Should().Be(rootCategory?.Id);

        var deepFeed = await _feedService.GetFeedByUrlAsync("https://deep.com/feed.xml");
        deepFeed?.CategoryId.Should().Be(grandchild?.Id);

        var childFeed = await _feedService.GetFeedByUrlAsync("https://child.com/feed.xml");
        childFeed?.CategoryId.Should().Be(child2?.Id);
    }

    [Fact]
    public async Task ImportAsync_WithMultipleFeedFormats_ShouldHandleAll()
    {
        var opmlWithFormats = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<opml version=""2.0"">
  <body>
    <outline type=""rss"" text=""RSS Feed"" xmlUrl=""https://example.com/rss""/>
    <outline type=""atom"" text=""Atom Feed"" xmlUrl=""https://example.com/atom""/>
    <outline type=""rdf"" text=""RDF Feed"" xmlUrl=""https://example.com/rdf""/>
    <outline type=""feed"" text=""Generic Feed"" xmlUrl=""https://example.com/feed""/>
  </body>
</opml>";

        using var stream = CreateStreamFromString(opmlWithFormats);

        var result = await _opmlService.ImportAsync(stream);

        result.Success.Should().BeTrue();
        result.FeedsImported.Should().Be(4);
        result.Errors.Should().BeEmpty();
    }

    #endregion
}