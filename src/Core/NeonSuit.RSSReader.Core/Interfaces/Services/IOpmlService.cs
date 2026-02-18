using NeonSuit.RSSReader.Core.Models;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service for importing and exporting RSS feeds in OPML format.
    /// OPML (Outline Processor Markup Language) is the standard format
    /// for RSS feed lists exchange between applications.
    /// </summary>
    public interface IOpmlService
    {
        /// <summary>
        /// Imports feeds from an OPML file stream.
        /// </summary>
        /// <param name="opmlStream">Stream containing OPML data.</param>
        /// <param name="defaultCategory">Default category for uncategorized feeds.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing feeds with same URL.</param>
        /// <returns>Import result with statistics.</returns>
        Task<OpmlImportResult> ImportAsync(Stream opmlStream, string defaultCategory = "Imported", bool overwriteExisting = false);

        /// <summary>
        /// Imports feeds from an OPML file.
        /// </summary>
        /// <param name="filePath">Path to the OPML file.</param>
        /// <param name="defaultCategory">Default category for uncategorized feeds.</param>
        /// <param name="overwriteExisting">Whether to overwrite existing feeds with same URL.</param>
        /// <returns>Import result with statistics.</returns>
        Task<OpmlImportResult> ImportFromFileAsync(string filePath, string defaultCategory = "Imported", bool overwriteExisting = false);

        /// <summary>
        /// Exports all feeds to OPML format.
        /// </summary>
        /// <param name="includeCategories">Whether to include category structure.</param>
        /// <param name="includeInactive">Whether to include inactive feeds.</param>
        /// <returns>OPML document as string.</returns>
        Task<string> ExportAsync(bool includeCategories = true, bool includeInactive = false);

        /// <summary>
        /// Exports feeds to an OPML file.
        /// </summary>
        /// <param name="filePath">Path where to save the OPML file.</param>
        /// <param name="includeCategories">Whether to include category structure.</param>
        /// <param name="includeInactive">Whether to include inactive feeds.</param>
        Task ExportToFileAsync(string filePath, bool includeCategories = true, bool includeInactive = false);

        /// <summary>
        /// Exports specific categories to OPML format.
        /// </summary>
        /// <param name="categoryIds">IDs of categories to export.</param>
        Task<string> ExportCategoriesAsync(IEnumerable<int> categoryIds);

        /// <summary>
        /// Validates an OPML file for correct structure.
        /// </summary>
        /// <param name="opmlStream">Stream containing OPML data.</param>
        /// <returns>Validation result with error messages if any.</returns>
        Task<OpmlValidationResult> ValidateAsync(Stream opmlStream);

        /// <summary>
        /// Gets statistics about OPML import/export operations.
        /// </summary>
        OpmlStatistics GetStatistics();
    }

    /// <summary>
    /// Result of an OPML import operation.
    /// </summary>
    public class OpmlImportResult
    {
        public bool Success { get; set; }
        public int TotalFeedsFound { get; set; }
        public int FeedsImported { get; set; }
        public int FeedsSkipped { get; set; }
        public int CategoriesCreated { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan ImportDuration { get; set; }
        public List<ImportedFeedInfo> ImportedFeeds { get; set; } = new List<ImportedFeedInfo>();
    }

    /// <summary>
    /// Information about an imported feed.
    /// </summary>
    public class ImportedFeedInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Category { get; set; }
        public int FeedId { get; set; }
        public bool WasNew { get; set; }
    }

    /// <summary>
    /// Result of OPML validation.
    /// </summary>
    public class OpmlValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public int FeedCount { get; set; }
        public List<string> DetectedCategories { get; set; } = new List<string>();
        public string? OpmlVersion { get; set; }
    }

    /// <summary>
    /// Statistics about OPML operations.
    /// </summary>
    public class OpmlStatistics
    {
        public int TotalImports { get; set; }
        public int TotalExports { get; set; }
        public int TotalFeedsImported { get; set; }
        public DateTime LastImport { get; set; }
        public DateTime LastExport { get; set; }
        public int FailedImports { get; set; }
        public int FailedExports { get; set; }
    }
}