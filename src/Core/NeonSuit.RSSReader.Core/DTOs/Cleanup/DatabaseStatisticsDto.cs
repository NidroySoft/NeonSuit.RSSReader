using System;

namespace NeonSuit.RSSReader.Core.DTOs.System
{
    /// <summary>
    /// Data Transfer Object for database statistics.
    /// </summary>
    public class DatabaseStatisticsDto
    {
        /// <summary>
        /// Total database size in bytes.
        /// </summary>
        public long DatabaseSizeBytes { get; set; }

        /// <summary>
        /// Total database size in MB (calculated).
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Total number of articles.
        /// </summary>
        public int TotalArticleCount { get; set; }

        /// <summary>
        /// Number of unread articles.
        /// </summary>
        public int UnreadArticleCount { get; set; }

        /// <summary>
        /// Number of read articles.
        /// </summary>
        public int ReadArticleCount { get; set; }

        /// <summary>
        /// Number of favorite articles.
        /// </summary>
        public int FavoriteArticleCount { get; set; }

        /// <summary>
        /// Total number of feeds.
        /// </summary>
        public int FeedCount { get; set; }

        /// <summary>
        /// Total number of categories.
        /// </summary>
        public int CategoryCount { get; set; }

        /// <summary>
        /// Number of images in cache.
        /// </summary>
        public int ImageCacheCount { get; set; }

        /// <summary>
        /// Size of image cache in bytes.
        /// </summary>
        public long ImageCacheSizeBytes { get; set; }

        /// <summary>
        /// Size of image cache in MB (calculated).
        /// </summary>
        public double ImageCacheSizeMB => ImageCacheSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Database fragmentation percentage (for SQLite).
        /// </summary>
        public double FragmentationPercent { get; set; }

        /// <summary>
        /// Timestamp of last cleanup operation.
        /// </summary>
        public DateTime? LastCleanupTimestamp { get; set; }

        /// <summary>
        /// Result of last cleanup operation.
        /// </summary>
        public string? LastCleanupResult { get; set; }
    }
}