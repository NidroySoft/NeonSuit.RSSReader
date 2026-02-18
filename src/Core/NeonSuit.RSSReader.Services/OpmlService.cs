using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Professional implementation of IOpmlService for handling OPML import/export operations.
    /// Supports OPML 1.0 and 2.0 specifications with extended attributes.
    /// Generates namespace-free XML for maximum interoperability with RSS readers.
    /// </summary>
    public class OpmlService : IOpmlService
    {
        private readonly IFeedService _feedService;
        private readonly ICategoryService _categoryService;
        private readonly ILogger _logger;
        private readonly OpmlStatistics _statistics;

        // Empty namespace ensures clean XML without namespace declarations
        private static readonly XNamespace _emptyNs = "";

        public OpmlService(IFeedService feedService, ICategoryService categoryService, ILogger logger)
        {
            _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<OpmlService>();
            _statistics = new OpmlStatistics();
        }

        /// <summary>
        /// Imports feeds from an OPML stream with comprehensive error handling and statistics tracking.
        /// </summary>
        public async Task<OpmlImportResult> ImportAsync(Stream opmlStream, string defaultCategory = "Imported", bool overwriteExisting = false)
        {

            if (opmlStream == null)
                throw new ArgumentNullException(nameof(opmlStream));

            var result = new OpmlImportResult();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.Information("Starting OPML import with default category: {Category}", defaultCategory);

                var validation = await ValidateAsync(opmlStream);
                if (!validation.IsValid)
                {
                    result.Errors.Add($"Invalid OPML file: {validation.ErrorMessage}");
                    result.Success = false;

                    // ✅ Incrementar TotalImports incluso cuando falla la validación
                    _statistics.TotalImports++;
                    _statistics.FailedImports++;

                    return result;
                }

                // Reset stream position after validation
                opmlStream.Position = 0;

                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, CancellationToken.None);
                var body = doc.Root?.Element("body");

                if (body == null)
                {
                    result.Errors.Add("OPML file does not contain a body element");
                    result.Success = false;

                    // ✅ Incrementar TotalImports incluso cuando falta body
                    _statistics.TotalImports++;
                    _statistics.FailedImports++;

                    return result;
                }

                result.TotalFeedsFound = validation.FeedCount;

                await ProcessOutlinesAsync(body.Elements("outline"), null, defaultCategory,
                    result, overwriteExisting);

                result.Success = !result.Errors.Any();
                _statistics.TotalImports++;
                _statistics.TotalFeedsImported += result.FeedsImported;
                _statistics.LastImport = DateTime.UtcNow;

                if (!result.Success)
                {
                    _statistics.FailedImports++;
                    _logger.Warning("OPML import completed with errors: {ErrorCount}", result.Errors.Count);
                }
                else
                {
                    _logger.Information("OPML import completed: {Imported} feeds imported, {Skipped} skipped",
                        result.FeedsImported, result.FeedsSkipped);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import OPML file");
                result.Errors.Add($"Import failed: {ex.Message}");
                result.Success = false;
                _statistics.TotalImports++;
                _statistics.FailedImports++;
            }
            finally
            {
                result.ImportDuration = DateTime.UtcNow - startTime;
            }

            return result;
        }

        /// <summary>
        /// Imports feeds from an OPML file path.
        /// </summary>
        public async Task<OpmlImportResult> ImportFromFileAsync(string filePath, string defaultCategory = "Imported", bool overwriteExisting = false)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("OPML file not found", filePath);

            await using var stream = File.OpenRead(filePath);
            return await ImportAsync(stream, defaultCategory, overwriteExisting);
        }

        /// <summary>
        /// Exports all feeds to OPML format with optional categorization and inactive feed inclusion.
        /// Generates clean XML without namespaces for maximum compatibility.
        /// </summary>
        /// <summary>
        /// Exports all feeds to OPML format with optional categorization and inactive feed inclusion.
        /// Generates clean XML without namespaces for maximum compatibility.
        /// </summary>
        public async Task<string> ExportAsync(bool includeCategories = true, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Exporting feeds to OPML format (Categories: {IncludeCategories}, Inactive: {IncludeInactive})",
                    includeCategories, includeInactive);

                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(_emptyNs + "opml",
                        new XAttribute("version", "2.0"),
                        new XElement(_emptyNs + "head",
                            new XElement(_emptyNs + "title", "NeonSuit RSS Reader Export"),
                            new XElement(_emptyNs + "dateCreated", DateTime.UtcNow.ToString("R")),
                            new XElement(_emptyNs + "ownerName", "NeonSuit User"),
                            new XElement(_emptyNs + "ownerEmail", ""),
                            new XElement(_emptyNs + "docs", "http://dev.opml.org/spec2.html")
                        ),
                        new XElement(_emptyNs + "body")
                    )
                );

                var body = doc.Root?.Element(_emptyNs + "body");
                if (body == null)
                    throw new InvalidOperationException("Failed to create OPML document structure");

                // Get all feeds once to avoid multiple database calls
                var allFeeds = await _feedService.GetAllFeedsAsync(includeInactive: true);

                // Filter feeds based on includeInactive flag
                var feedsToExport = includeInactive
                    ? allFeeds
                    : allFeeds.Where(f => f.IsActive).ToList();

                if (includeCategories)
                {
                    // Group feeds by category
                    var feedsByCategory = feedsToExport
                        .Where(f => f.CategoryId.HasValue)
                        .GroupBy(f => f.CategoryId!.Value)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // Get category details for all categories that have feeds
                    foreach (var categoryGroup in feedsByCategory)
                    {
                        var category = await _categoryService.GetCategoryByIdAsync(categoryGroup.Key);
                        if (category == null) continue;

                        var categoryOutline = new XElement(_emptyNs + "outline",
                            new XAttribute("text", category.Name),
                            new XAttribute("title", category.Name)
                        );

                        foreach (var feed in categoryGroup.Value)
                        {
                            AddFeedToOutline(categoryOutline, feed);
                        }

                        if (categoryOutline.HasElements)
                            body.Add(categoryOutline);
                    }

                    // Add uncategorized feeds at root level
                    var uncategorizedFeeds = feedsToExport.Where(f => f.CategoryId == null);
                    foreach (var feed in uncategorizedFeeds)
                    {
                        AddFeedToOutline(body, feed);
                    }
                }
                else
                {
                    // Export all feeds flat (no categories)
                    foreach (var feed in feedsToExport)
                    {
                        AddFeedToOutline(body, feed);
                    }
                }

                _statistics.TotalExports++;
                _statistics.LastExport = DateTime.UtcNow;

                var result = doc.ToString();
                _logger.Information("OPML export completed: {TotalBytes} bytes, {FeedCount} feeds exported",
                    result.Length, feedsToExport.Count());

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export OPML");
                _statistics.FailedExports++;
                throw;
            }
        }

        /// <summary>
        /// Exports OPML content to a file.
        /// </summary>
        public async Task ExportToFileAsync(string filePath, bool includeCategories = true, bool includeInactive = false)
        {
            try
            {
                var opmlContent = await ExportAsync(includeCategories, includeInactive);
                await File.WriteAllTextAsync(filePath, opmlContent, Encoding.UTF8);
                _logger.Information("OPML exported to file: {FilePath} ({Bytes} bytes)",
                    filePath, opmlContent.Length);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to write OPML file: {FilePath}", filePath);
                throw;
            }
        }

        /// <summary>
        /// Exports specific categories to OPML format.
        /// </summary>
        public async Task<string> ExportCategoriesAsync(IEnumerable<int> categoryIds)
        {
            if (categoryIds == null)
                throw new ArgumentNullException(nameof(categoryIds));

            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(_emptyNs + "opml",
                        new XAttribute("version", "2.0"),
                        new XElement(_emptyNs + "head",
                            new XElement(_emptyNs + "title", "NeonSuit Category Export"),
                            new XElement(_emptyNs + "dateCreated", DateTime.UtcNow.ToString("R"))
                        ),
                        new XElement(_emptyNs + "body")
                    )
                );

                var body = doc.Root?.Element(_emptyNs + "body");
                if (body == null)
                    throw new InvalidOperationException("Failed to create OPML document structure");

                foreach (var categoryId in categoryIds)
                {
                    var category = await _categoryService.GetCategoryWithFeedsAsync(categoryId);
                    if (category == null)
                    {
                        _logger.Warning("Category {CategoryId} not found during export", categoryId);
                        continue;
                    }

                    var categoryOutline = new XElement(_emptyNs + "outline",
                        new XAttribute("text", category.Name),
                        new XAttribute("title", category.Name),
                        new XAttribute("description", category.Description ?? "")
                    );

                    foreach (var feed in category.Feeds.Where(f => f.IsActive))
                    {
                        AddFeedToOutline(categoryOutline, feed);
                    }

                    if (categoryOutline.HasElements)
                        body.Add(categoryOutline);
                }

                _logger.Debug("Exported {CategoryCount} categories to OPML", categoryIds.Count());
                return doc.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export categories to OPML");
                throw;
            }
        }

        /// <summary>
        /// Validates an OPML stream without modifying stream position permanently.
        /// </summary>
        public async Task<OpmlValidationResult> ValidateAsync(Stream opmlStream)
        {
            if (opmlStream == null)
                throw new ArgumentNullException(nameof(opmlStream));

            var result = new OpmlValidationResult();
            var originalPosition = opmlStream.Position;

            try
            {
                // Read for validation
                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, CancellationToken.None);

                // Check root element - accept with or without namespace
                var root = doc.Root;
                if (root == null)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Root element must be 'opml'";
                    return result;
                }

                // Validate root element name (case-insensitive, with or without namespace)
                if (!root.Name.LocalName.Equals("opml", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Root element must be 'opml'";
                    return result;
                }

                result.OpmlVersion = root.Attribute("version")?.Value ?? "1.0";

                // Count feeds - look for xmlUrl attribute in any namespace
                result.FeedCount = doc.Descendants()
                    .Count(o =>
                        o.Attribute("xmlUrl") != null ||
                        o.Attribute("url") != null);

                // Get categories - elements without xmlUrl that have text/title
                result.DetectedCategories = doc.Descendants()
                    .Where(o =>
                        o.Attribute("xmlUrl") == null &&
                        o.Attribute("url") == null &&
                        (o.Attribute("text") != null || o.Attribute("title") != null))
                    .Select(o =>
                        o.Attribute("text")?.Value ??
                        o.Attribute("title")?.Value)
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()!;

                result.IsValid = true;
                _logger.Debug("OPML validation passed: {FeedCount} feeds, {CategoryCount} categories",
                    result.FeedCount, result.DetectedCategories.Count);
            }
            catch (XmlException ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Invalid XML: {ex.Message}";
                _logger.Error(ex, "OPML validation failed - XML parse error");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                _logger.Error(ex, "OPML validation failed");
            }
            finally
            {
                // Always restore original position
                if (opmlStream.CanSeek)
                {
                    opmlStream.Position = originalPosition;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets current import/export statistics.
        /// </summary>
        public OpmlStatistics GetStatistics() => _statistics;

        // Private helper methods

        /// <summary>
        /// Recursively processes OPML outline elements.
        /// </summary>
        private async Task ProcessOutlinesAsync(IEnumerable<XElement> outlines, string? parentCategoryName,
     string defaultCategory, OpmlImportResult result, bool overwriteExisting)
        {
            foreach (var outline in outlines)
            {
                var outlineType = outline.Attribute("type")?.Value ?? "rss";
                var text = outline.Attribute("text")?.Value ?? outline.Attribute("title")?.Value ?? string.Empty;
                var xmlUrl = outline.Attribute("xmlUrl")?.Value ?? outline.Attribute("url")?.Value;
                var htmlUrl = outline.Attribute("htmlUrl")?.Value;
                var categoryName = outline.Attribute("category")?.Value ?? parentCategoryName ?? defaultCategory;

                // Si tiene hijos y no es un feed, es una categoría
                if (outline.HasElements && string.IsNullOrEmpty(xmlUrl))
                {
                    // Obtener o crear la categoría actual
                    var currentCategory = await _categoryService.GetOrCreateCategoryAsync(text);

                    // Si tiene categoría padre, actualizar la relación
                    if (!string.IsNullOrEmpty(parentCategoryName))
                    {
                        var parentCategory = await _categoryService.GetOrCreateCategoryAsync(parentCategoryName);
                        if (currentCategory.ParentCategoryId != parentCategory.Id)
                        {
                            currentCategory.ParentCategoryId = parentCategory.Id;
                            await _categoryService.UpdateCategoryAsync(currentCategory);
                        }
                    }

                    // Procesar hijos con esta categoría como padre
                    await ProcessOutlinesAsync(outline.Elements("outline"), text, defaultCategory, result, overwriteExisting);

                    result.CategoriesCreated++;
                }
                // Si es un feed
                else if (!string.IsNullOrEmpty(xmlUrl))
                {
                    await ProcessFeedOutlineAsync(xmlUrl, text, htmlUrl, categoryName, result, overwriteExisting);
                }
            }
        }

        /// <summary>
        /// Determines if an outline type represents a valid feed format.
        /// </summary>
        private static bool IsValidFeedType(string type)
        {
            return type switch
            {
                "rss" or "atom" or "rdf" or "feed" => true,
                _ => false
            };
        }

        /// <summary>
        /// Processes a single feed outline element.
        /// </summary>
        private async Task ProcessFeedOutlineAsync(string feedUrl, string title, string? siteUrl,
            string category, OpmlImportResult result, bool overwriteExisting)
        {
            try
            {
                // Validate URL
                if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out _))
                {
                    result.Errors.Add($"Invalid URL format: {feedUrl}");
                    return;
                }

                // Check if feed already exists
                var existingFeed = await _feedService.GetFeedByUrlAsync(feedUrl);

                if (existingFeed != null && !overwriteExisting)
                {
                    result.FeedsSkipped++;
                    result.Warnings.Add($"Feed already exists: {title} ({feedUrl})");
                    _logger.Debug("Skipping existing feed: {Title} at {Url}", title, feedUrl);
                    return;
                }

                // Get or create category
                var categoryModel = await _categoryService.GetOrCreateCategoryAsync(category);

                // Prepare feed data
                var feedData = new Feed
                {
                    Title = string.IsNullOrWhiteSpace(title) ? "Untitled Feed" : title,
                    Url = feedUrl,
                    WebsiteUrl = siteUrl ?? feedUrl,
                    CategoryId = categoryModel.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                if (existingFeed == null)
                {
                    // Create new feed
                    var feedId = await _feedService.CreateFeedAsync(feedData);
                    result.FeedsImported++;

                    result.ImportedFeeds.Add(new ImportedFeedInfo
                    {
                        Title = feedData.Title,
                        Url = feedUrl,
                        Category = category,
                        FeedId = feedId,
                        WasNew = true
                    });

                    _logger.Information("Imported new feed: {Title} to category {Category}", feedData.Title, category);
                }
                else if (overwriteExisting)
                {
                    // Update existing feed
                    existingFeed.Title = feedData.Title;
                    existingFeed.WebsiteUrl = feedData.WebsiteUrl;
                    existingFeed.CategoryId = categoryModel.Id;
                    existingFeed.LastUpdated = DateTime.UtcNow;

                    await _feedService.UpdateFeedAsync(existingFeed);
                    result.FeedsImported++;

                    result.ImportedFeeds.Add(new ImportedFeedInfo
                    {
                        Title = feedData.Title,
                        Url = feedUrl,
                        Category = category,
                        FeedId = existingFeed.Id,
                        WasNew = false
                    });

                    _logger.Information("Updated existing feed: {Title}", feedData.Title);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to import feed '{title}': {ex.Message}");
                _logger.Error(ex, "Failed to import feed: {Title} ({Url})", title, feedUrl);
            }
        }

        /// <summary>
        /// Adds a feed as an outline element to the parent XML element.
        /// </summary>
        private void AddFeedToOutline(XElement parent, Feed feed)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (feed == null) throw new ArgumentNullException(nameof(feed));

            var outline = new XElement(_emptyNs + "outline",
                new XAttribute("type", "rss"),
                new XAttribute("text", feed.Title ?? "Untitled"),
                new XAttribute("title", feed.Title ?? "Untitled"),
                new XAttribute("xmlUrl", feed.Url),
                new XAttribute("htmlUrl", feed.WebsiteUrl ?? feed.Url),
                new XAttribute("description", feed.Description ?? "")
            );

            // Add optional attributes
            if (!string.IsNullOrEmpty(feed.Language))
                outline.Add(new XAttribute("language", feed.Language));

            // Add update frequency if not default
            if (feed.UpdateFrequency != FeedUpdateFrequency.EveryHour)
            {
                if (feed.UpdateFrequency == FeedUpdateFrequency.Manual)
                {
                    outline.Add(new XAttribute("updateFrequency", "0"));
                }
                else
                {
                    outline.Add(new XAttribute("updateFrequency", (int)feed.UpdateFrequency));
                }
            }

            parent.Add(outline);
        }
    }
}