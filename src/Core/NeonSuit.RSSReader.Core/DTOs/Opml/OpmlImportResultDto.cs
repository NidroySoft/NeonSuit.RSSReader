using System;
using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Opml
{
    /// <summary>
    /// Data Transfer Object for OPML import operation results.
    /// </summary>
    public class OpmlImportResultDto
    {
        /// <summary>
        /// Indicates whether the import operation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Total number of feed entries found in the OPML file.
        /// </summary>
        public int TotalFeedsFound { get; set; }

        /// <summary>
        /// Number of feeds successfully imported.
        /// </summary>
        public int FeedsImported { get; set; }

        /// <summary>
        /// Number of feeds skipped (duplicates or invalid URLs).
        /// </summary>
        public int FeedsSkipped { get; set; }

        /// <summary>
        /// Number of new categories created during import.
        /// </summary>
        public int CategoriesCreated { get; set; }

        /// <summary>
        /// List of non-critical issues encountered during import.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// List of critical errors that caused import failures.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// Total duration of the import operation.
        /// </summary>
        public TimeSpan ImportDuration { get; set; }

        /// <summary>
        /// Detailed information about each successfully imported feed.
        /// </summary>
        public List<ImportedFeedInfoDto> ImportedFeeds { get; set; } = new();
    }
}