namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Represents the comprehensive results of a database cleanup operation.
    /// Aggregates results from all sub-operations including article deletion,
    /// orphan removal, vacuum, and index rebuild.
    /// </summary>
    public class CleanupResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CleanupResult"/> class.
        /// </summary>
        public CleanupResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            OrphanCleanup = new OrphanRemovalResult();
            ImageCacheCleanup = new ImageCacheCleanupResult();
            VacuumResult = new VacuumResult();
        }

        #region Status Properties

        /// <summary>
        /// Gets or sets a value indicating whether the cleanup operation completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the cleanup was skipped 
        /// (e.g., if auto-cleanup is disabled).
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the cleanup operation was performed.
        /// </summary>
        public DateTime PerformedAt { get; set; }

        /// <summary>
        /// Gets or sets the total duration of the cleanup operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        #endregion

        #region Operation Results

        /// <summary>
        /// Gets or sets the detailed results of the article cleanup sub-operation.
        /// Contains information about deleted articles and retention statistics.
        /// </summary>
        public ArticleDeletionResult? ArticleCleanup { get; set; }

        /// <summary>
        /// Gets or sets the detailed results of the orphan record removal sub-operation.
        /// Contains counts of removed orphaned records from junction tables.
        /// </summary>
        public OrphanRemovalResult OrphanCleanup { get; set; }

        /// <summary>
        /// Gets or sets the results of the image cache cleanup sub-operation.
        /// Contains information about deleted images and space reclaimed.
        /// </summary>
        public ImageCacheCleanupResult ImageCacheCleanup { get; set; }

        /// <summary>
        /// Gets or sets the results of the database vacuum sub-operation.
        /// Contains space statistics before and after the vacuum.
        /// </summary>
        public VacuumResult VacuumResult { get; set; }

        #endregion

        #region Aggregated Statistics

        /// <summary>
        /// Gets the total number of articles deleted during the cleanup.
        /// </summary>
        public int TotalArticlesDeleted =>
            (ArticleCleanup?.ArticlesDeleted ?? 0) + OrphanCleanup.OrphanedArticlesRemoved;

        /// <summary>
        /// Gets the total number of records deleted (articles + orphans + tags).
        /// </summary>
        public int TotalRecordsDeleted =>
            (ArticleCleanup?.ArticlesDeleted ?? 0) + OrphanCleanup.TotalRecordsRemoved;

        /// <summary>
        /// Gets or sets the total space freed in bytes across all operations.
        /// </summary>
        public long SpaceFreedBytes { get; set; }

        /// <summary>
        /// Gets the total space freed in megabytes.
        /// </summary>
        public double SpaceFreedMB => SpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the total space freed in a human-readable format.
        /// </summary>
        public string SpaceFreedFormatted => FormatBytes(SpaceFreedBytes);

        #endregion

        #region Error Handling

        /// <summary>
        /// Gets the collection of error messages encountered during the cleanup.
        /// </summary>
        public List<string> Errors { get; }

        /// <summary>
        /// Gets the collection of warning messages encountered during the cleanup.
        /// </summary>
        public List<string> Warnings { get; }

        /// <summary>
        /// Gets a value indicating whether any errors occurred during cleanup.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        /// <summary>
        /// Gets a value indicating whether any warnings occurred during cleanup.
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Adds an error message to the result and sets Success to false.
        /// </summary>
        /// <param name="error">The error message to add.</param>
        public void AddError(string error)
        {
            Errors.Add(error);
            Success = false;
        }

        /// <summary>
        /// Adds a warning message to the result.
        /// </summary>
        /// <param name="warning">The warning message to add.</param>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a summary report of the cleanup operation.
        /// </summary>
        /// <returns>A formatted multi-line string suitable for logging or display.</returns>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Database Cleanup Report ===",
                $"Status: {(Success ? "Success" : HasErrors ? "Failed" : "Unknown")}",
                $"Performed At: {PerformedAt:yyyy-MM-dd HH:mm:ss UTC}",
                $"Duration: {Duration.TotalSeconds:F2} seconds",
                "",
                "--- Articles ---",
                $"Articles Deleted: {ArticleCleanup?.ArticlesDeleted ?? 0}",
                $"Orphaned Records Removed: {OrphanCleanup.TotalRecordsRemoved}",
                "",
                "--- Space ---",
                $"Space Freed: {SpaceFreedFormatted}",
                "",
                "--- Errors & Warnings ---",
                $"Errors: {Errors.Count}",
                $"Warnings: {Warnings.Count}"
            };

            if (HasErrors)
            {
                lines.Add("");
                lines.Add("Error Details:");
                lines.AddRange(Errors.Select(e => $"  - {e}"));
            }

            if (HasWarnings)
            {
                lines.Add("");
                lines.Add("Warning Details:");
                lines.AddRange(Warnings.Select(w => $"  - {w}"));
            }

            lines.Add("==============================");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of the cleanup result.
        /// </summary>
        public override string ToString()
        {
            if (Skipped)
                return $"Cleanup skipped at {PerformedAt:yyyy-MM-dd HH:mm}";

            var status = Success ? "Success" : "Failed";
            return $"{status}: {TotalArticlesDeleted} articles deleted, {SpaceFreedFormatted} freed in {Duration.TotalSeconds:F1}s";
        }

        private static string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            return bytes switch
            {
                >= GB => $"{bytes / (double)GB:F2} GB",
                >= MB => $"{bytes / (double)MB:F2} MB",
                >= KB => $"{bytes / (double)KB:F2} KB",
                _ => $"{bytes} B"
            };
        }

        #endregion
    }
}