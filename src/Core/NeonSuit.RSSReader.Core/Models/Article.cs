using NeonSuit.RSSReader.Core.Enums;
using SQLite;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a persistent article entity derived from an RSS feed entry.
    /// Optimized for SQLite performance and loose-coupled XML parsing.
    /// </summary>
    [Table("Articles")]
    public class Article
    {
        public Article()
        {
            AddedDate = DateTime.UtcNow;
            Status = ArticleStatus.Unread;
        }


        public virtual ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
        public virtual ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Foreign Key referencing the parent Feed.
        /// </summary>
        [Indexed, NotNull]
        public int FeedId { get; set; }

        /// <summary>
        /// Unique identifier from the source RSS feed. 
        /// Used for idempotency during synchronization.
        /// </summary>
        [Indexed(Unique = true), NotNull]
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Headline or title of the entry.
        /// </summary>
        [NotNull]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Direct canonical URL to the full article.
        /// </summary>
        [NotNull]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Full HTML content or body of the article.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Short excerpt or summary. Often used for list views.
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Credit of the content creator. Optional as many feeds omit this.
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// Original publication timestamp provided by the feed.
        /// </summary>
        [Indexed]
        public DateTime PublishedDate { get; set; }

        /// <summary>
        /// Internal timestamp for database record creation.
        /// </summary>
        [NotNull]
        public DateTime AddedDate { get; set; }

        /// <summary>
        /// Optional featured image URL.
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Current lifecycle status (Read/Unread).
        /// </summary>
        [Indexed]
        public ArticleStatus Status { get; set; }

        /// <summary>
        /// Flag for user-curated bookmarking.
        /// </summary>
        [Indexed]
        public bool IsStarred { get; set; }

        /// <summary>
        /// Metadata tags or categories provided by the feed.
        /// </summary>
        public string Categories { get; set; } = string.Empty;

        /// <summary>
        /// Content hash for duplicate detection (MD5/SHA1).
        /// </summary>
        [Indexed]
        public string ContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Whether this article has been notified to the user.
        /// </summary>
        [Indexed]
        public bool IsNotified { get; set; }

        /// <summary>
        /// Whether rules have processed this article.
        /// </summary>
        [Indexed]
        public bool ProcessedByRules { get; set; }

        /// <summary>
        /// Flag for user-curated favorite status.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Navigation property for in-memory operations. Not persisted.
        /// </summary>
        [Ignore]
        public Feed? Feed { get; set; }

        /// <summary>
        /// Tags assigned to this article.
        /// </summary>
        [Ignore]
        public List<Tag> Tags { get; set; } = new List<Tag>();

        /// <summary>
        /// Returns a clean excerpt for UI display (first 200 chars).
        /// </summary>
        [Ignore]
        public string DisplayExcerpt
        {
            get
            {
                if (!string.IsNullOrEmpty(Summary))
                    return Summary.Length > 200 ? Summary.Substring(0, 200) + "..." : Summary;

                if (!string.IsNullOrEmpty(Content))
                {
                    var plainText = System.Text.RegularExpressions.Regex.Replace(Content, "<.*?>", string.Empty);
                    return plainText.Length > 200 ? plainText.Substring(0, 200) + "..." : plainText;
                }

                return "No content available";
            }
        }
    }
}