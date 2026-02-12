using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using NeonSuit.RSSReader.Core.Models;
using NeonSuit.RSSReader.Data.Logging;
using Serilog;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of IOpmlService for handling OPML import/export operations.
    /// Supports OPML 1.0 and 2.0 specifications with extended attributes.
    /// </summary>
    public class OpmlService : IOpmlService
    {
        private readonly IFeedService _feedService;
        private readonly ICategoryService _categoryService;
        private readonly ILogger _logger;
        private readonly OpmlStatistics _statistics;

        public OpmlService(IFeedService feedService, ICategoryService categoryService, ILogger logger)
        {
            _feedService = feedService ?? throw new ArgumentNullException(nameof(feedService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _logger = (logger ?? throw new ArgumentNullException(nameof(logger))).ForContext<OpmlService>()  ;
            _statistics = new OpmlStatistics()  ;
        }

        public async Task<OpmlImportResult> ImportAsync(Stream opmlStream, string defaultCategory = "Imported", bool overwriteExisting = false)
        {
            var result = new OpmlImportResult();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.Information("Starting OPML import with default category: {Category}", defaultCategory);

                // Validate OPML first
                var validation = await ValidateAsync(opmlStream);
                if (!validation.IsValid)
                {
                    result.Errors.Add($"Invalid OPML file: {validation.ErrorMessage}");
                    result.Success = false;
                    return result;
                }

                // Reset stream position after validation
                opmlStream.Position = 0;

                // Parse OPML
                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, CancellationToken.None);
                var body = doc.Root?.Element("body");

                if (body == null)
                {
                    result.Errors.Add("OPML file does not contain a body element");
                    result.Success = false;
                    return result;
                }

                result.TotalFeedsFound = validation.FeedCount;

                // Process outlines recursively
                await ProcessOutlinesAsync(body.Elements("outline"), null, defaultCategory,
                    result, overwriteExisting);

                result.Success = !result.Errors.Any();
                _statistics.TotalImports++;
                _statistics.TotalFeedsImported += result.FeedsImported;
                _statistics.LastImport = DateTime.UtcNow;

                if (result.Success)
                {
                    _logger.Information("OPML import completed: {Imported} feeds imported, {Skipped} skipped",
                        result.FeedsImported, result.FeedsSkipped);
                }
                else
                {
                    _statistics.FailedImports++;
                    _logger.Warning("OPML import completed with errors: {ErrorCount}", result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import OPML file");
                result.Errors.Add($"Import failed: {ex.Message}");
                result.Success = false;
                _statistics.FailedImports++;
            }
            finally
            {
                result.ImportDuration = DateTime.UtcNow - startTime;
            }

            return result;
        }

        public async Task<OpmlImportResult> ImportFromFileAsync(string filePath, string defaultCategory = "Imported", bool overwriteExisting = false)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("OPML file not found", filePath);

            await using var stream = File.OpenRead(filePath);
            return await ImportAsync(stream, defaultCategory, overwriteExisting);
        }

        public async Task<string> ExportAsync(bool includeCategories = true, bool includeInactive = false)
        {
            try
            {
                _logger.Debug("Exporting feeds to OPML format (Categories: {IncludeCategories})", includeCategories);

                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("opml",
                        new XAttribute("version", "2.0"),
                        new XElement("head",
                            new XElement("title", "NeonSuit RSS Reader Export"),
                            new XElement("dateCreated", DateTime.UtcNow.ToString("R")),
                            new XElement("ownerName", "NeonSuit User"),
                            new XElement("ownerEmail", ""),
                            new XElement("docs", "http://dev.opml.org/spec2.html")
                        ),
                        new XElement("body")
                    )
                );

                var body = doc.Root?.Element("body");
                if (body == null)
                    throw new InvalidOperationException("Failed to create OPML document structure");

                if (includeCategories)
                {
                    // Get categories with their feeds
                    var categories = await _categoryService.GetAllCategoriesWithFeedsAsync();
                    foreach (var category in categories)
                    {
                        var categoryOutline = new XElement("outline",
                            new XAttribute("text", category.Name),
                            new XAttribute("title", category.Name)
                        );

                        foreach (var feed in category.Feeds.Where(f => includeInactive || f.IsActive))
                        {
                            AddFeedToOutline(categoryOutline, feed);
                        }

                        if (categoryOutline.HasElements)
                            body.Add(categoryOutline);
                    }
                }
                else
                {
                    // Export all feeds without categories
                    var feeds = await _feedService.GetAllFeedsAsync();
                    foreach (var feed in feeds.Where(f => includeInactive || f.IsActive))
                    {
                        AddFeedToOutline(body, feed);
                    }
                }

                _statistics.TotalExports++;
                _statistics.LastExport = DateTime.UtcNow;

                _logger.Information("OPML export completed successfully");
                return doc.ToString();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export OPML");
                _statistics.FailedExports++;
                throw;
            }
        }

        public async Task ExportToFileAsync(string filePath, bool includeCategories = true, bool includeInactive = false)
        {
            try
            {
                var opmlContent = await ExportAsync(includeCategories, includeInactive);
                await File.WriteAllTextAsync(filePath, opmlContent, Encoding.UTF8);
                _logger.Information("OPML exported to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to write OPML file: {FilePath}", filePath);
                throw;
            }
        }

        public async Task<string> ExportCategoriesAsync(IEnumerable<int> categoryIds)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("opml",
                        new XAttribute("version", "2.0"),
                        new XElement("head",
                            new XElement("title", "NeonSuit Category Export"),
                            new XElement("dateCreated", DateTime.UtcNow.ToString("R"))
                        ),
                        new XElement("body")
                    )
                );

                var body = doc.Root?.Element("body");
                if (body == null)
                    throw new InvalidOperationException("Failed to create OPML document structure");

                foreach (var categoryId in categoryIds)
                {
                    var category = await _categoryService.GetCategoryWithFeedsAsync(categoryId);
                    if (category == null) continue;

                    var categoryOutline = new XElement("outline",
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

        public async Task<OpmlValidationResult> ValidateAsync(Stream opmlStream)
        {
            var result = new OpmlValidationResult();

            try
            {
                // Read just enough to validate
                var buffer = new byte[4096];
                await opmlStream.ReadAsync(buffer, 0, buffer.Length);
                var content = Encoding.UTF8.GetString(buffer);

                // Reset stream position
                opmlStream.Position = 0;

                // Quick check for OPML structure
                if (!content.Contains("<opml") || !content.Contains("</opml>"))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Not a valid OPML file";
                    return result;
                }

                // Proper parse
                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, CancellationToken.None);

                // Reset stream position again
                opmlStream.Position = 0;

                if (doc.Root?.Name != "opml")
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Root element must be 'opml'";
                    return result;
                }

                result.OpmlVersion = doc.Root.Attribute("version")?.Value ?? "1.0";

                // Count feeds
                result.FeedCount = doc.Descendants("outline")
                    .Count(o => o.Attribute("xmlUrl") != null || o.Attribute("url") != null);

                // Get categories
                result.DetectedCategories = doc.Descendants("outline")
                   .Where(o => o.Attribute("xmlUrl") == null && o.Attribute("url") == null)
                   .Select(o => o.Attribute("text")?.Value ?? o.Attribute("title")?.Value)
                   .Where(c => !string.IsNullOrEmpty(c))
                   .Distinct()
                   .Select(c => c!)
                   .ToList();

                result.IsValid = true;
                _logger.Debug("OPML validation passed: {FeedCount} feeds, {CategoryCount} categories",
                    result.FeedCount, result.DetectedCategories.Count);
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                _logger.Error(ex, "OPML validation failed");
            }

            return result;
        }

        public OpmlStatistics GetStatistics() => _statistics;

        // Private helper methods
        private async Task ProcessOutlinesAsync(IEnumerable<XElement> outlines, string? parentCategory,
            string defaultCategory, OpmlImportResult result, bool overwriteExisting)
        {
            foreach (var outline in outlines)
            {
                var outlineType = outline.Attribute("type")?.Value ?? "rss";
                var text = outline.Attribute("text")?.Value ?? outline.Attribute("title")?.Value ?? string.Empty;
                var xmlUrl = outline.Attribute("xmlUrl")?.Value ?? outline.Attribute("url")?.Value;
                var htmlUrl = outline.Attribute("htmlUrl")?.Value;
                var category = outline.Attribute("category")?.Value ?? parentCategory ?? defaultCategory;

                // If it's a feed
                if (!string.IsNullOrEmpty(xmlUrl) && (outlineType == "rss" || outlineType == "atom"))
                {
                    await ProcessFeedOutlineAsync(xmlUrl, text, htmlUrl, category, result, overwriteExisting);
                }
                // If it's a category folder
                else if (!string.IsNullOrEmpty(text) && outline.HasElements)
                {
                    // Process nested outlines with this as parent category
                    await ProcessOutlinesAsync(outline.Elements("outline"), text, defaultCategory, result, overwriteExisting);
                    result.CategoriesCreated++;
                }
            }
        }

        private async Task ProcessFeedOutlineAsync(string feedUrl, string title, string? siteUrl,
            string category, OpmlImportResult result, bool overwriteExisting)
        {
            try
            {
                // Check if feed already exists
                var existingFeed = await _feedService.GetFeedByUrlAsync(feedUrl);

                if (existingFeed != null && !overwriteExisting)
                {
                    result.FeedsSkipped++;
                    result.Warnings.Add($"Feed already exists: {title} ({feedUrl})");
                    _logger.Debug("Skipping existing feed: {Title}", title);
                    return;
                }

                // Get or create category
                var categoryModel = await _categoryService.GetOrCreateCategoryAsync(category);

                // Create or update feed
                var feed = existingFeed ?? new Feed
                {
                    Title = title,
                    Url = feedUrl,
                    WebsiteUrl = siteUrl ?? feedUrl,
                    CategoryId = categoryModel.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                if (existingFeed == null)
                {
                    var feedId = await _feedService.CreateFeedAsync(feed);
                    feed.Id = feedId;
                    result.FeedsImported++;

                    result.ImportedFeeds.Add(new ImportedFeedInfo
                    {
                        Title = title,
                        Url = feedUrl,
                        Category = category,
                        FeedId = feedId,
                        WasNew = true
                    });

                    _logger.Information("Imported new feed: {Title} to category {Category}", title, category);
                }
                else if (overwriteExisting)
                {
                    await _feedService.UpdateFeedAsync(feed);
                    result.FeedsImported++;

                    result.ImportedFeeds.Add(new ImportedFeedInfo
                    {
                        Title = title,
                        Url = feedUrl,
                        Category = category,
                        FeedId = existingFeed.Id,
                        WasNew = false
                    });

                    _logger.Information("Updated existing feed: {Title}", title);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to import feed '{title}': {ex.Message}");
                _logger.Error(ex, "Failed to import feed: {Title} ({Url})", title, feedUrl);
            }
        }

        private void AddFeedToOutline(XElement parent, Feed feed)
        {
            var outline = new XElement("outline",
                new XAttribute("type", "rss"),
                new XAttribute("text", feed.Title),
                new XAttribute("title", feed.Title),
                new XAttribute("xmlUrl", feed.Url),
                new XAttribute("htmlUrl", feed.WebsiteUrl ?? feed.Url),
                new XAttribute("description", feed.Description ?? "")
            );

            // Add optional attributes
            if (!string.IsNullOrEmpty(feed.Language))
                outline.Add(new XAttribute("language", feed.Language));

            if (feed.UpdateFrequency == FeedUpdateFrequency.Manual)
            {
                outline.Add(new XAttribute("updateFrequency", "Manual"));
            }
            else
            {
                outline.Add(new XAttribute("updateFrequency", (int)feed.UpdateFrequency));
            }

            parent.Add(outline);
        }
    }
}