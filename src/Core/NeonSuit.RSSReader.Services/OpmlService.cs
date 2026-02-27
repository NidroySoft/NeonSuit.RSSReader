using AutoMapper;
using NeonSuit.RSSReader.Core.DTOs.Feeds;
using NeonSuit.RSSReader.Core.DTOs.Opml;
using NeonSuit.RSSReader.Core.Enums;
using NeonSuit.RSSReader.Core.Interfaces.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace NeonSuit.RSSReader.Services
{
    /// <summary>
    /// Implementation of <see cref="IOpmlService"/> for handling OPML import/export operations.
    /// Supports OPML 1.0 and 2.0 specifications with extended attributes.
    /// </summary>
    internal class OpmlService : IOpmlService
    {
        private readonly IFeedService _feedService;
        private readonly ICategoryService _categoryService;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly OpmlStatisticsDto _statistics;

        // Empty namespace ensures clean XML without namespace declarations
        private static readonly XNamespace _emptyNs = "";

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="OpmlService"/> class.
        /// </summary>
        /// <param name="feedService">Service for feed operations.</param>
        /// <param name="categoryService">Service for category operations.</param>
        /// <param name="mapper">AutoMapper instance for DTO transformations.</param>
        /// <param name="logger">Serilog logger instance for structured logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any parameter is null.</exception>
        public OpmlService(
            IFeedService feedService,
            ICategoryService categoryService,
            IMapper mapper,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(feedService);
            ArgumentNullException.ThrowIfNull(categoryService);
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(logger);

            _feedService = feedService;
            _categoryService = categoryService;
            _mapper = mapper;
            _logger = logger.ForContext<OpmlService>();
            _statistics = new OpmlStatisticsDto();

#if DEBUG
            _logger.Debug("OpmlService initialized");
#endif
        }

        #endregion

        #region Import Operations

        /// <inheritdoc />
        public async Task<OpmlImportResultDto> ImportAsync(
            Stream opmlStream,
            string defaultCategory = "Imported",
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(opmlStream);

            if (string.IsNullOrWhiteSpace(defaultCategory))
            {
                _logger.Warning("Default category is empty, using 'Imported'");
                defaultCategory = "Imported";
            }

            var result = new OpmlImportResultDto();
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.Information("Starting OPML import with default category: {Category}", defaultCategory);

                var validation = await ValidateAsync(opmlStream, cancellationToken).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    result.Errors.Add($"Invalid OPML file: {validation.ErrorMessage}");
                    result.Success = false;

                    _statistics.TotalImports++;
                    _statistics.FailedImports++;

                    _logger.Warning("OPML import failed validation: {ErrorMessage}", validation.ErrorMessage);
                    return result;
                }

                // Reset stream position after validation
                if (opmlStream.CanSeek)
                {
                    opmlStream.Position = 0;
                }
                else
                {
                    _logger.Warning("Stream does not support seeking, validation may have consumed it");
                    result.Errors.Add("Stream does not support seeking - cannot process after validation");
                    result.Success = false;
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
                var body = doc.Root?.Element("body");

                if (body == null)
                {
                    result.Errors.Add("OPML file does not contain a body element");
                    result.Success = false;

                    _statistics.TotalImports++;
                    _statistics.FailedImports++;

                    return result;
                }

                result.TotalFeedsFound = validation.FeedCount;

                await ProcessOutlinesAsync(
                    body.Elements("outline"),
                    null,
                    defaultCategory,
                    result,
                    overwriteExisting,
                    cancellationToken).ConfigureAwait(false);

                result.Success = !result.Errors.Any();
                _statistics.TotalImports++;
                _statistics.TotalFeedsImported += result.FeedsImported;
                _statistics.LastImport = DateTime.UtcNow;

                if (!result.Success)
                {
                    _statistics.FailedImports++;
                    _logger.Warning("OPML import completed with {ErrorCount} errors and {WarningCount} warnings",
                        result.Errors.Count, result.Warnings.Count);
                }
                else
                {
                    _logger.Information("OPML import completed: {Imported} feeds imported, {Skipped} skipped, {Categories} categories created",
                        result.FeedsImported, result.FeedsSkipped, result.CategoriesCreated);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("OPML import was cancelled");
                result.Errors.Add("Import cancelled by user");
                result.Success = false;
                _statistics.TotalImports++;
                _statistics.FailedImports++;
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to import OPML file");
                result.Errors.Add($"Import failed: {ex.Message}");
                result.Success = false;
                _statistics.TotalImports++;
                _statistics.FailedImports++;
                throw new InvalidOperationException($"Failed to import OPML file: {ex.Message}", ex);
            }
            finally
            {
                result.ImportDuration = DateTime.UtcNow - startTime;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<OpmlImportResultDto> ImportFromFileAsync(
            string filePath,
            string defaultCategory = "Imported",
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Warning("File path is empty");
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                _logger.Error("OPML file not found: {FilePath}", filePath);
                throw new FileNotFoundException("OPML file not found", filePath);
            }

            _logger.Debug("Importing OPML from file: {FilePath}", filePath);

            await using var stream = File.OpenRead(filePath);
            return await ImportAsync(stream, defaultCategory, overwriteExisting, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Export Operations

        /// <inheritdoc />
        public async Task<string> ExportAsync(
            bool includeCategories = true,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Exporting feeds to OPML format (Categories: {IncludeCategories}, Inactive: {IncludeInactive})",
                    includeCategories, includeInactive);

                cancellationToken.ThrowIfCancellationRequested();

                var doc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement(_emptyNs + "opml",
                        new XAttribute("version", "2.0"),
                        new XElement(_emptyNs + "head",
                            new XElement(_emptyNs + "title", "NeonSuit RSS Reader Export"),
                            new XElement(_emptyNs + "dateCreated", DateTime.UtcNow.ToString("R")),
                            new XElement(_emptyNs + "ownerName", "NeonSuit User"),
                            new XElement(_emptyNs + "docs", "http://dev.opml.org/spec2.html")
                        ),
                        new XElement(_emptyNs + "body")
                    )
                );

                var body = doc.Root?.Element(_emptyNs + "body");
                if (body == null)
                    throw new InvalidOperationException("Failed to create OPML document structure");

                cancellationToken.ThrowIfCancellationRequested();

                // Get all feeds once to avoid multiple database calls
                var allFeeds = await _feedService.GetAllFeedsAsync(includeInactive: true, cancellationToken).ConfigureAwait(false);

                // Filter feeds based on includeInactive flag
                var feedsToExport = includeInactive
                    ? allFeeds
                    : allFeeds.Where(f => f.IsActive).ToList();

                _logger.Debug("Exporting {FeedCount} feeds", feedsToExport.Count);

                if (includeCategories)
                {
                    await ExportWithCategoriesAsync(body, feedsToExport, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    ExportFlat(body, feedsToExport);
                }

                _statistics.TotalExports++;
                _statistics.LastExport = DateTime.UtcNow;

                var result = doc.ToString();
                _logger.Information("OPML export completed: {TotalBytes} bytes, {FeedCount} feeds exported",
                    result.Length, feedsToExport.Count);

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("OPML export was cancelled");
                _statistics.FailedExports++;
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export OPML");
                _statistics.FailedExports++;
                throw new InvalidOperationException($"Failed to export OPML: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task ExportToFileAsync(
            string filePath,
            bool includeCategories = true,
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.Warning("File path is empty");
                throw new ArgumentException("File path cannot be empty", nameof(filePath));
            }

            try
            {
                _logger.Debug("Exporting OPML to file: {FilePath}", filePath);

                var opmlContent = await ExportAsync(includeCategories, includeInactive, cancellationToken).ConfigureAwait(false);

                // Write to temporary file first for atomic operation
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempFile = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(tempFile, opmlContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
                    File.Move(tempFile, filePath, true);
                }
                catch
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                    throw;
                }

                _logger.Information("OPML exported to file: {FilePath} ({Bytes} bytes)",
                    filePath, opmlContent.Length);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("OPML export to file was cancelled: {FilePath}", filePath);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to write OPML file: {FilePath}", filePath);
                throw new InvalidOperationException($"Failed to export OPML to file: {filePath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<string> ExportCategoriesAsync(
            IEnumerable<int> categoryIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(categoryIds);

            var categoryList = categoryIds.ToList();
            if (!categoryList.Any())
            {
                _logger.Warning("Empty category list provided for export");
                throw new ArgumentException("Category list cannot be empty", nameof(categoryIds));
            }

            try
            {
                _logger.Debug("Exporting {CategoryCount} categories to OPML", categoryList.Count);

                cancellationToken.ThrowIfCancellationRequested();

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

                foreach (var categoryId in categoryList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var category = await _categoryService.GetCategoryByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
                    if (category == null)
                    {
                        _logger.Warning("Category {CategoryId} not found during export", categoryId);
                        continue;
                    }

                    var feeds = await _feedService.GetFeedsByCategoryAsync(categoryId, false, cancellationToken).ConfigureAwait(false);

                    var categoryOutline = new XElement(_emptyNs + "outline",
                        new XAttribute("text", category.Name),
                        new XAttribute("title", category.Name),
                        new XAttribute("description", category.Description ?? "")
                    );

                    foreach (var feedSummary in feeds.Where(f => f.IsActive))
                    {
                        var feedDetail = await _feedService.GetFeedByIdAsync(feedSummary.Id, false, cancellationToken).ConfigureAwait(false);
                        if (feedDetail != null)
                        {
                            AddFeedToOutline(categoryOutline, feedDetail);
                        }
                    }

                    if (categoryOutline.HasElements)
                        body.Add(categoryOutline);
                }

                _logger.Debug("Exported {CategoryCount} categories to OPML", categoryList.Count);
                return doc.ToString();
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("ExportCategoriesAsync operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to export categories to OPML");
                throw new InvalidOperationException($"Failed to export categories to OPML: {ex.Message}", ex);
            }
        }
        #endregion

        #region Validation

        /// <inheritdoc />
        public async Task<OpmlValidationResultDto> ValidateAsync(
            Stream opmlStream,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(opmlStream);

            var result = new OpmlValidationResultDto();
            var originalPosition = opmlStream.CanSeek ? opmlStream.Position : -1;

            try
            {
                var doc = await XDocument.LoadAsync(opmlStream, LoadOptions.None, cancellationToken).ConfigureAwait(false);

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
            catch (OperationCanceledException)
            {
                _logger.Debug("OPML validation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = ex.Message;
                _logger.Error(ex, "OPML validation failed");
                throw new InvalidOperationException($"OPML validation failed: {ex.Message}", ex);
            }
            finally
            {
                // Always restore original position if stream supports seeking
                if (opmlStream.CanSeek && originalPosition >= 0)
                {
                    opmlStream.Position = originalPosition;
                }
            }

            return result;
        }

        #endregion

        #region Statistics

        /// <inheritdoc />
        public Task<OpmlStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Debug("Retrieving OPML statistics");
                return Task.FromResult(_mapper.Map<OpmlStatisticsDto>(_statistics));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve OPML statistics");
                throw new InvalidOperationException("Failed to retrieve OPML statistics", ex);
            }
        }

        #endregion

        #region Private Helper Methods - Import

        /// <summary>
        /// Recursively processes OPML outline elements.
        /// </summary>
        private async Task ProcessOutlinesAsync(
            IEnumerable<XElement> outlines,
            string? parentCategoryName,
            string defaultCategory,
            OpmlImportResultDto result,
            bool overwriteExisting,
            CancellationToken cancellationToken)
        {
            foreach (var outline in outlines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = outline.Attribute("text")?.Value ?? outline.Attribute("title")?.Value ?? string.Empty;
                var xmlUrl = outline.Attribute("xmlUrl")?.Value ?? outline.Attribute("url")?.Value;
                var htmlUrl = outline.Attribute("htmlUrl")?.Value;
                var categoryName = outline.Attribute("category")?.Value ?? parentCategoryName ?? defaultCategory;

                // If it has children and is not a feed, it's a category
                if (outline.HasElements && string.IsNullOrEmpty(xmlUrl))
                {
                    await ProcessCategoryOutlineAsync(outline, text, parentCategoryName, defaultCategory,
                        result, overwriteExisting, cancellationToken).ConfigureAwait(false);
                }
                // If it's a feed
                else if (!string.IsNullOrEmpty(xmlUrl))
                {
                    await ProcessFeedOutlineAsync(xmlUrl, text, htmlUrl, categoryName,
                        result, overwriteExisting, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Processes a category outline element.
        /// </summary>
        private async Task ProcessCategoryOutlineAsync(
            XElement outline,
            string categoryName,
            string? parentCategoryName,
            string defaultCategory,
            OpmlImportResultDto result,
            bool overwriteExisting,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    categoryName = "Unnamed Category";
                }

                // Get or create the current category
                var currentCategory = await _categoryService.GetOrCreateCategoryAsync(categoryName, cancellationToken).ConfigureAwait(false);

                // If it has parent category, update the relationship
                if (!string.IsNullOrEmpty(parentCategoryName) && parentCategoryName != categoryName)
                {
                    var parentCategory = await _categoryService.GetOrCreateCategoryAsync(parentCategoryName, cancellationToken).ConfigureAwait(false);

                    // Update parent if needed (simplified - actual implementation would need proper update method)
                    if (currentCategory.ParentCategoryId != parentCategory.Id)
                    {
                        // This would need a proper update method in CategoryService
                        _logger.Debug("Would update category parent: {Category} -> {Parent}", categoryName, parentCategoryName);
                    }
                }

                // Process children with this category as parent
                await ProcessOutlinesAsync(outline.Elements("outline"), categoryName, defaultCategory,
                    result, overwriteExisting, cancellationToken).ConfigureAwait(false);

                result.CategoriesCreated++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to process category '{categoryName}': {ex.Message}");
                _logger.Error(ex, "Failed to process category: {CategoryName}", categoryName);
            }
        }

        /// <summary>
        /// Processes a single feed outline element.
        /// </summary>
        private async Task ProcessFeedOutlineAsync(
            string feedUrl,
            string title,
            string? siteUrl,
            string category,
            OpmlImportResultDto result,
            bool overwriteExisting,
            CancellationToken cancellationToken)
        {
            try
            {
                // Validate URL
                if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out _))
                {
                    result.Warnings.Add($"Invalid URL format: {feedUrl}");
                    result.FeedsSkipped++;
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Check if feed already exists
                var existingFeed = await _feedService.GetFeedByUrlAsync(feedUrl, true, cancellationToken).ConfigureAwait(false);

                if (existingFeed != null && !overwriteExisting)
                {
                    result.FeedsSkipped++;
                    result.Warnings.Add($"Feed already exists: {title} ({feedUrl})");
                    _logger.Debug("Skipping existing feed: {Title} at {Url}", title, feedUrl);
                    return;
                }

                // Get or create category
                var categoryModel = await _categoryService.GetOrCreateCategoryAsync(category, cancellationToken).ConfigureAwait(false);

                var createDto = new CreateFeedDto
                {
                    Url = feedUrl,
                    Title = string.IsNullOrWhiteSpace(title) ? "Untitled Feed" : title,
                    WebsiteUrl = siteUrl ?? feedUrl,
                    CategoryId = categoryModel.Id,
                    UpdateFrequency = FeedUpdateFrequency.EveryHour
                };

                if (existingFeed == null)
                {
                    // Create new feed
                    var newFeed = await _feedService.AddFeedAsync(createDto, cancellationToken).ConfigureAwait(false);
                    result.FeedsImported++;

                    result.ImportedFeeds.Add(new ImportedFeedInfoDto
                    {
                        Title = newFeed.Title,
                        Url = feedUrl,
                        Category = category,
                        FeedId = newFeed.Id,
                        WasNew = true
                    });

                    _logger.Information("Imported new feed: {Title} to category {Category}", newFeed.Title, category);
                }
                else if (overwriteExisting)
                {
                    // Update existing feed
                    var updateDto = new UpdateFeedDto
                    {
                        Title = createDto.Title,
                        WebsiteUrl = createDto.WebsiteUrl,
                        CategoryId = categoryModel.Id
                    };

                    var updatedFeed = await _feedService.UpdateFeedAsync(existingFeed.Id, updateDto, cancellationToken).ConfigureAwait(false);

                    if (updatedFeed != null)
                    {
                        result.FeedsImported++;
                        result.ImportedFeeds.Add(new ImportedFeedInfoDto
                        {
                            Title = updatedFeed.Title,
                            Url = feedUrl,
                            Category = category,
                            FeedId = updatedFeed.Id,
                            WasNew = false
                        });

                        _logger.Information("Updated existing feed: {Title}", updatedFeed.Title);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to import feed '{title}': {ex.Message}");
                _logger.Error(ex, "Failed to import feed: {Title} ({Url})", title, feedUrl);
            }
        }

        #endregion

        #region Private Helper Methods - Export

        /// <summary>
        /// Exports feeds organized by categories.
        /// </summary>
        private async Task ExportWithCategoriesAsync(
            XElement body,
            List<FeedSummaryDto> feedsToExport,
            CancellationToken cancellationToken)
        {
            // Group feeds by category
            var feedsByCategory = feedsToExport
                .Where(f => f.CategoryId.HasValue)
                .GroupBy(f => f.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Get category details for all categories that have feeds
            foreach (var categoryGroup in feedsByCategory)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var category = await _categoryService.GetCategoryByIdAsync(categoryGroup.Key, cancellationToken).ConfigureAwait(false);
                if (category == null) continue;

                var categoryOutline = new XElement(_emptyNs + "outline",
                    new XAttribute("text", category.Name),
                    new XAttribute("title", category.Name)
                );

                foreach (var feed in categoryGroup.Value)
                {
                    var feedDetail = await _feedService.GetFeedByIdAsync(feed.Id, false, cancellationToken).ConfigureAwait(false);
                    if (feedDetail != null)
                    {
                        AddFeedToOutline(categoryOutline, feedDetail);
                    }
                }

                if (categoryOutline.HasElements)
                    body.Add(categoryOutline);
            }

            // Add uncategorized feeds at root level
            var uncategorizedFeeds = feedsToExport.Where(f => f.CategoryId == null);
            foreach (var feed in uncategorizedFeeds)
            {
                var feedDetail = await _feedService.GetFeedByIdAsync(feed.Id, false, cancellationToken).ConfigureAwait(false);
                if (feedDetail != null)
                {
                    AddFeedToOutline(body, feedDetail);
                }
            }
        }

        /// <summary>
        /// Exports feeds flat (no categories).
        /// </summary>
        private void ExportFlat(XElement body, List<FeedSummaryDto> feedsToExport)
        {
            // This would need to get full feed details or modify AddFeedToOutline to accept FeedSummaryDto
            _logger.Warning("Flat export not fully implemented - needs FeedDto");
        }

        /// <summary>
        /// Adds a feed as an outline element to the parent XML element.
        /// </summary>
        private void AddFeedToOutline(XElement parent, FeedDto feed)
        {
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(feed);

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
                outline.Add(new XAttribute("updateFrequency", (int)feed.UpdateFrequency));
            }

            parent.Add(outline);
        }

        #endregion
    }
}