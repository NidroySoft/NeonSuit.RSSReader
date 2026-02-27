using System;

namespace NeonSuit.RSSReader.Core.DTOs.Opml
{
    /// <summary>
    /// Data Transfer Object for OPML operation statistics.
    /// </summary>
    public class OpmlStatisticsDto
    {
        /// <summary>
        /// Total number of import operations performed.
        /// </summary>
        public int TotalImports { get; set; }

        /// <summary>
        /// Total number of export operations performed.
        /// </summary>
        public int TotalExports { get; set; }

        /// <summary>
        /// Total number of feeds imported across all operations.
        /// </summary>
        public int TotalFeedsImported { get; set; }

        /// <summary>
        /// Timestamp of the last successful import.
        /// </summary>
        public DateTime LastImport { get; set; }

        /// <summary>
        /// Timestamp of the last successful export.
        /// </summary>
        public DateTime LastExport { get; set; }

        /// <summary>
        /// Number of failed import operations.
        /// </summary>
        public int FailedImports { get; set; }

        /// <summary>
        /// Number of failed export operations.
        /// </summary>
        public int FailedExports { get; set; }
    }
}