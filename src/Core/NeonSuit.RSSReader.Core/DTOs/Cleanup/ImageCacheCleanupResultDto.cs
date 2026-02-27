using System.Collections.Generic;

namespace NeonSuit.RSSReader.Core.DTOs.Cleanup
{
    /// <summary>
    /// Data Transfer Object for image cache cleanup results.
    /// </summary>
    public class ImageCacheCleanupResultDto
    {
        /// <summary>
        /// Number of images before cleanup.
        /// </summary>
        public int ImagesBeforeCleanup { get; set; }

        /// <summary>
        /// Number of images after cleanup.
        /// </summary>
        public int ImagesRemaining { get; set; }

        /// <summary>
        /// Number of images deleted during cleanup.
        /// </summary>
        public int ImagesDeleted { get; set; }

        /// <summary>
        /// Cache size in bytes before cleanup.
        /// </summary>
        public long CacheSizeBeforeBytes { get; set; }

        /// <summary>
        /// Cache size in MB before cleanup (calculated).
        /// </summary>
        public double CacheSizeBeforeMB => CacheSizeBeforeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Cache size in bytes after cleanup.
        /// </summary>
        public long CacheSizeAfterBytes { get; set; }

        /// <summary>
        /// Cache size in MB after cleanup (calculated).
        /// </summary>
        public double CacheSizeAfterMB => CacheSizeAfterBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Space freed in bytes.
        /// </summary>
        public long SpaceFreedBytes { get; set; }

        /// <summary>
        /// Space freed in MB (calculated).
        /// </summary>
        public double SpaceFreedMB => SpaceFreedBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Warning messages encountered during cleanup.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }
}