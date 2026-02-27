namespace NeonSuit.RSSReader.Core.Models.Cleanup
{
    /// <summary>
    /// Represents comprehensive database statistics for monitoring and diagnostics.
    /// Provides metrics on articles, feeds, tags, and database file size.
    /// </summary>
    public class DatabaseStatistics
    {
        #region Metadata

        /// <summary>
        /// Gets or sets the UTC timestamp when these statistics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; }

        /// <summary>
        /// Gets the age of these statistics (time elapsed since generation).
        /// </summary>
        public TimeSpan Age => DateTime.UtcNow - GeneratedAt;

        /// <summary>
        /// Gets a value indicating whether these statistics are stale (older than 1 hour).
        /// </summary>
        public bool IsStale => Age.TotalHours > 1;

        #endregion

        #region Article Statistics

        /// <summary>
        /// Gets or sets the total number of articles in the database.
        /// </summary>
        public int TotalArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as read.
        /// </summary>
        public int ReadArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as unread.
        /// </summary>
        public int UnreadArticles { get; set; }

        /// <summary>
        /// Gets or sets the number of articles marked as favorite.
        /// </summary>
        public int FavoriteArticles { get; set; }

        /// <summary>
        /// Gets the read ratio (percentage of articles that have been read).
        /// </summary>
        public double ReadRatio => TotalArticles > 0 ? (double)ReadArticles / TotalArticles : 0;

        /// <summary>
        /// Gets the unread ratio (percentage of articles that are unread).
        /// </summary>
        public double UnreadRatio => TotalArticles > 0 ? (double)UnreadArticles / TotalArticles : 0;

        /// <summary>
        /// Gets the favorite ratio (percentage of articles that are favorites).
        /// </summary>
        public double FavoriteRatio => TotalArticles > 0 ? (double)FavoriteArticles / TotalArticles : 0;

        #endregion

        #region Article Age Distribution

        /// <summary>
        /// Gets or sets the number of articles older than 30 days.
        /// </summary>
        public int ArticlesOlderThan30Days { get; set; }

        /// <summary>
        /// Gets or sets the number of articles older than 60 days.
        /// </summary>
        public int ArticlesOlderThan60Days { get; set; }

        /// <summary>
        /// Gets or sets the number of articles older than 90 days.
        /// </summary>
        public int ArticlesOlderThan90Days { get; set; }

        /// <summary>
        /// Gets or sets the publication date of the oldest article in the database.
        /// </summary>
        public DateTime OldestArticleDate { get; set; }

        /// <summary>
        /// Gets or sets the publication date of the newest article in the database.
        /// </summary>
        public DateTime NewestArticleDate { get; set; }

        /// <summary>
        /// Gets the time span covered by articles (newest - oldest).
        /// </summary>
        public TimeSpan ArticleTimeSpan => NewestArticleDate - OldestArticleDate;

        #endregion

        #region Feed Statistics

        /// <summary>
        /// Gets or sets the total number of feeds.
        /// </summary>
        public int TotalFeeds { get; set; }

        /// <summary>
        /// Gets or sets the number of active feeds.
        /// </summary>
        public int ActiveFeeds { get; set; }

        /// <summary>
        /// Gets the number of inactive feeds.
        /// </summary>
        public int InactiveFeeds => TotalFeeds - ActiveFeeds;

        /// <summary>
        /// Gets the average number of articles per feed.
        /// </summary>
        public double AverageArticlesPerFeed => TotalFeeds > 0 ? (double)TotalArticles / TotalFeeds : 0;

        /// <summary>
        /// Gets the activity ratio (percentage of feeds that are active).
        /// </summary>
        public double FeedActivityRatio => TotalFeeds > 0 ? (double)ActiveFeeds / TotalFeeds : 0;

        #endregion

        #region Tag Statistics

        /// <summary>
        /// Gets or sets the total number of tags.
        /// </summary>
        public int TotalTags { get; set; }

        /// <summary>
        /// Gets the average number of articles per tag.
        /// </summary>
        public double AverageArticlesPerTag => TotalTags > 0 ? (double)TotalArticles / TotalTags : 0;

        #endregion

        #region Database File Statistics

        /// <summary>
        /// Gets or sets the physical size of the database file in bytes.
        /// </summary>
        public long DatabaseSizeBytes { get; set; }

        /// <summary>
        /// Gets the database size in kilobytes.
        /// </summary>
        public double DatabaseSizeKB => DatabaseSizeBytes / 1024.0;

        /// <summary>
        /// Gets the database size in megabytes.
        /// </summary>
        public double DatabaseSizeMB => DatabaseSizeBytes / (1024.0 * 1024.0);

        /// <summary>
        /// Gets the database size in gigabytes.
        /// </summary>
        public double DatabaseSizeGB => DatabaseSizeBytes / (1024.0 * 1024.0 * 1024.0);

        /// <summary>
        /// Gets the database size in a human-readable format.
        /// </summary>
        public string DatabaseSizeFormatted => FormatBytes(DatabaseSizeBytes);

        /// <summary>
        /// Gets the average size per article in bytes.
        /// </summary>
        public double AverageArticleSizeBytes => TotalArticles > 0 ? (double)DatabaseSizeBytes / TotalArticles : 0;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a comprehensive report of database statistics.
        /// </summary>
        public string GenerateReport()
        {
            var lines = new List<string>
            {
                "=== Database Statistics Report ===",
                $"Generated At: {GeneratedAt:yyyy-MM-dd HH:mm:ss UTC} (Age: {Age.TotalMinutes:F0} minutes)",
                "",
                "--- Articles ---",
                $"Total Articles:     {TotalArticles,10:N0}",
                $"  Read:             {ReadArticles,10:N0} ({ReadRatio:P1})",
                $"  Unread:           {UnreadArticles,10:N0} ({UnreadRatio:P1})",
                $"  Favorites:        {FavoriteArticles,10:N0} ({FavoriteRatio:P1})",
                $"",
                $"Age Distribution:",
                $"  > 30 days:        {ArticlesOlderThan30Days,10:N0}",
                $"  > 60 days:        {ArticlesOlderThan60Days,10:N0}",
                $"  > 90 days:        {ArticlesOlderThan90Days,10:N0}",
                $"  Oldest:           {OldestArticleDate:yyyy-MM-dd}",
                $"  Newest:           {NewestArticleDate:yyyy-MM-dd}",
                $"  Time Span:        {ArticleTimeSpan.TotalDays:F0} days",
                "",
                "--- Feeds ---",
                $"Total Feeds:        {TotalFeeds,10:N0}",
                $"  Active:           {ActiveFeeds,10:N0} ({FeedActivityRatio:P1})",
                $"  Inactive:         {InactiveFeeds,10:N0}",
                $"Avg Articles/Feed:  {AverageArticlesPerFeed:F1}",
                "",
                "--- Tags ---",
                $"Total Tags:         {TotalTags,10:N0}",
                $"Avg Articles/Tag:   {AverageArticlesPerTag:F1}",
                "",
                "--- Storage ---",
                $"Database Size:      {DatabaseSizeFormatted,10}",
                $"Avg Article Size:   {AverageArticleSizeBytes:F0} bytes",
                "==============================="
            };

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets a brief summary of database statistics.
        /// </summary>
        public override string ToString()
        {
            return $"{TotalArticles:N0} articles, {TotalFeeds:N0} feeds ({ActiveFeeds:N0} active), " +
                   $"{TotalTags:N0} tags, {DatabaseSizeFormatted}";
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