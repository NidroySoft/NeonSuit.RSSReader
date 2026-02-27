using NeonSuit.RSSReader.Core.DTOs.Opml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NeonSuit.RSSReader.Core.Interfaces.Services
{
    /// <summary>
    /// Service interface for importing and exporting RSS feeds in OPML format.
    /// OPML (Outline Processor Markup Language) is the standard format for RSS feed lists exchange between applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This service provides comprehensive OPML import/export functionality with the following features:
    /// <list type="bullet">
    /// <item>Import feeds from OPML files with automatic category creation</item>
    /// <item>Export feeds to OPML format with optional category structure preservation</item>
    /// <item>Validation of OPML files before import to prevent corruption</item>
    /// <item>Statistics tracking for monitoring import/export operations</item>
    /// <item>Support for both streams and file paths for flexible integration</item>
    /// </list>
    /// </para>
    /// <para>
    /// All methods return DTOs instead of entities to maintain separation of concerns.
    /// </para>
    /// </remarks>
    public interface IOpmlService
    {
        #region Import Operations

        /// <summary>
        /// Imports feeds from an OPML file stream.
        /// </summary>
        /// <param name="opmlStream">Stream containing OPML data.</param>
        /// <param name="defaultCategory">Default category for uncategorized feeds. Default: "Imported".</param>
        /// <param name="overwriteExisting">Whether to overwrite existing feeds with same URL. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>An <see cref="OpmlImportResultDto"/> containing import statistics and details.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="opmlStream"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="defaultCategory"/> is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the OPML format is invalid or cannot be parsed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<OpmlImportResultDto> ImportAsync(Stream opmlStream, string defaultCategory = "Imported", bool overwriteExisting = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Imports feeds from an OPML file.
        /// </summary>
        /// <param name="filePath">Path to the OPML file.</param>
        /// <param name="defaultCategory">Default category for uncategorized feeds. Default: "Imported".</param>
        /// <param name="overwriteExisting">Whether to overwrite existing feeds with same URL. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>An <see cref="OpmlImportResultDto"/> containing import statistics and details.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown if the specified file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if read access to the file is denied.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the OPML format is invalid or cannot be parsed.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<OpmlImportResultDto> ImportFromFileAsync(string filePath, string defaultCategory = "Imported", bool overwriteExisting = false, CancellationToken cancellationToken = default);

        #endregion

        #region Export Operations

        /// <summary>
        /// Exports all feeds to OPML format.
        /// </summary>
        /// <param name="includeCategories">Whether to include category structure in the OPML outline. Default: true.</param>
        /// <param name="includeInactive">Whether to include inactive feeds in the export. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>OPML document as a string.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<string> ExportAsync(bool includeCategories = true, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports feeds to an OPML file.
        /// </summary>
        /// <param name="filePath">Path where to save the OPML file.</param>
        /// <param name="includeCategories">Whether to include category structure. Default: true.</param>
        /// <param name="includeInactive">Whether to include inactive feeds. Default: false.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if write access to the directory is denied.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory path doesn't exist.</exception>
        /// <exception cref="IOException">Thrown if an I/O error occurs while creating the file.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task ExportToFileAsync(string filePath, bool includeCategories = true, bool includeInactive = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exports specific categories to OPML format.
        /// </summary>
        /// <param name="categoryIds">IDs of categories to export.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>OPML document as a string containing only feeds from the specified categories.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="categoryIds"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="categoryIds"/> is empty.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<string> ExportCategoriesAsync(IEnumerable<int> categoryIds, CancellationToken cancellationToken = default);

        #endregion

        #region Validation

        /// <summary>
        /// Validates an OPML file for correct structure and content.
        /// </summary>
        /// <param name="opmlStream">Stream containing OPML data.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>An <see cref="OpmlValidationResultDto"/> with validation details and detected feeds.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="opmlStream"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<OpmlValidationResultDto> ValidateAsync(Stream opmlStream, CancellationToken cancellationToken = default);

        #endregion

        #region Statistics

        /// <summary>
        /// Gets statistics about OPML import/export operations.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>An <see cref="OpmlStatisticsDto"/> object containing operation metrics.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled via the cancellation token.</exception>
        Task<OpmlStatisticsDto> GetStatisticsAsync(CancellationToken cancellationToken = default);

        #endregion
    }
}