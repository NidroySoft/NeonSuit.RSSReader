namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Result of image cache cleanup operation.
    /// Contains statistics about images deleted and space reclaimed.
    /// </summary>
    public class ImageCacheCleanupResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCacheCleanupResult"/> class.
        /// </summary>
        public ImageCacheCleanupResult()
        {
            Warnings = new List<string>();
        }

        /// <summary>
        /// Gets or sets the number of images deleted.
        /// </summary>
        public int ImagesDeleted { get; set; }

        /// <summary>
        /// Gets or sets the number of images remaining after cleanup.
        /// </summary>
        public int ImagesRemaining { get; set; }

        /// <summary>
        /// Gets or sets the number of images before cleanup.
        /// </summary>
        public int ImagesBeforeCleanup { get; set; }

        /// <summary>
        /// Gets or sets the cache size in bytes before cleanup.
        /// </summary>
        public long CacheSizeBeforeBytes { get; set; }

        /// <summary>
        /// Gets or sets the cache size in bytes after cleanup.
        /// </summary>
        public long CacheSizeAfterBytes { get; set; }

        /// <summary>
        /// Gets or sets the space freed in bytes.
        /// </summary>
        public long SpaceFreedBytes { get; set; }

        /// <summary>
        /// Gets or sets the warning messages for files that couldn't be deleted.
        /// </summary>
        public List<string> Warnings { get; set; }

        /// <summary>
        /// Gets the cache size before cleanup in a human-readable format.
        /// </summary>
        public string CacheSizeBeforeFormatted => FormatBytes(CacheSizeBeforeBytes);

        /// <summary>
        /// Gets the cache size after cleanup in a human-readable format.
        /// </summary>
        public string CacheSizeAfterFormatted => FormatBytes(CacheSizeAfterBytes);

        /// <summary>
        /// Gets the space freed in a human-readable format.
        /// </summary>
        public string SpaceFreedFormatted => FormatBytes(SpaceFreedBytes);

        /// <summary>
        /// Gets a value indicating whether any images were deleted.
        /// </summary>
        public bool AnyDeleted => ImagesDeleted > 0;

        /// <summary>
        /// Gets a value indicating whether any warnings occurred.
        /// </summary>
        public bool HasWarnings => Warnings.Count > 0;

        /// <summary>
        /// Gets a brief summary of the image cache cleanup.
        /// </summary>
        public override string ToString()
        {
            return $"Deleted {ImagesDeleted} images, freed {SpaceFreedFormatted}, {ImagesRemaining} remaining";
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