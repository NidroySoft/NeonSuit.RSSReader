using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.System
{
    /// <summary>
    /// Data Transfer Object for database integrity check results.
    /// </summary>
    public class IntegrityCheckResultDto
    {
        /// <summary>
        /// Whether the integrity check passed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// List of errors found during integrity check.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// List of warnings found during integrity check.
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Number of tables checked.
        /// </summary>
        public int TablesChecked { get; set; }

        /// <summary>
        /// Number of indexes checked.
        /// </summary>
        public int IndexesChecked { get; set; }

        /// <summary>
        /// Number of foreign key constraints checked.
        /// </summary>
        public int ForeignKeysChecked { get; set; }

        /// <summary>
        /// Number of orphaned records found.
        /// </summary>
        public int OrphanedRecordsFound { get; set; }
    }
}