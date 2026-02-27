namespace NeonSuit.RSSReader.Core.DTOs.Opml
{
    /// <summary>
    /// Data Transfer Object for information about an imported feed.
    /// </summary>
    public class ImportedFeedInfoDto
    {
        /// <summary>
        /// Title of the imported feed.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// URL of the imported feed.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Category assigned to the feed (if any).
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Database ID of the created or updated feed.
        /// </summary>
        public int FeedId { get; set; }

        /// <summary>
        /// Indicates whether this was a new feed (true) or an update to existing (false).
        /// </summary>
        public bool WasNew { get; set; }
    }
}