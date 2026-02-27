namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Result of database vacuum operation.
    /// Contains statistics about space reclaimed and operation duration.
    /// </summary>
    /// <remarks>
    /// VACUUM rebuilds the database file, repacking it into a minimal amount of disk space.
    /// This operation may temporarily double the database file size during execution.
    /// </remarks>
    public class VacuumResult
    {
        /// <summary>
        /// Gets or sets the database size in bytes before vacuum operation.
        /// </summary>
        public long SizeBeforeBytes { get; set; }

        /// <summary>
        /// Gets or sets the database size in bytes after vacuum operation.
        /// </summary>
        public long SizeAfterBytes { get; set; }

        /// <summary>
        /// Gets or sets the duration of the vacuum operation.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets the space freed by the vacuum operation in bytes.
        /// </summary>
        public long SpaceFreedBytes => SizeBeforeBytes - SizeAfterBytes;

        /// <summary>
        /// Gets the space freed in a human-readable format.
        /// </summary>
        public string SpaceFreedFormatted => FormatBytes(SpaceFreedBytes);

        /// <summary>
        /// Gets the size before in a human-readable format.
        /// </summary>
        public string SizeBeforeFormatted => FormatBytes(SizeBeforeBytes);

        /// <summary>
        /// Gets the size after in a human-readable format.
        /// </summary>
        public string SizeAfterFormatted => FormatBytes(SizeAfterBytes);

        /// <summary>
        /// Gets a value indicating whether the vacuum operation was successful.
        /// </summary>
        public bool Success => SizeAfterBytes > 0 && SizeAfterBytes < SizeBeforeBytes;

        /// <summary>
        /// Gets a human-readable summary of the vacuum operation.
        /// </summary>
        public override string ToString()
        {
            return $"Database size reduced from {SizeBeforeFormatted} to {SizeAfterFormatted} " +
                   $"(freed {SpaceFreedFormatted}) in {Duration.TotalSeconds:F1}s";
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
    }
}