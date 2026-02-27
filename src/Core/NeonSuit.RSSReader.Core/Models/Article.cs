using NeonSuit.RSSReader.Core.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a single entry (article/item) from an RSS, Atom or JSON Feed source.
    /// Stores metadata, content, user interaction state and processing flags.
    /// Designed for efficient querying and low memory usage on low-end hardware.
    /// </summary>
    [Table("Articles")]
    public class Article : INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Article"/> class.
        /// Sets default values for user interaction states.
        /// </summary>
        public Article()
        {
            AddedDate = DateTime.UtcNow;
            Status = ArticleStatus.Unread;
            IsStarred = false;
            IsFavorite = false;
            IsNotified = false;
            ProcessedByRules = false;
            ReadPercentage = 0;
            ArticleTags = new HashSet<ArticleTag>();
            NotificationLogs = new HashSet<NotificationLog>();
        }

        #region Identity

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Feed-provided globally unique identifier (from &lt;guid&gt; or &lt;id&gt; element).
        /// Used as the primary key for deduplication across sync operations.
        /// </summary>
        [Required]
        [MaxLength(512)]
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// SHA-256 hash of normalized title + cleaned content.
        /// Used to detect meaningful content changes between fetches.
        /// </summary>
        [MaxLength(64)]
        public string? ContentHash { get; set; }

        #endregion

        #region Relationships & Foreign Keys

        /// <summary>
        /// Foreign key to the parent <see cref="Feed"/>.
        /// </summary>
        public int FeedId { get; set; }

        /// <summary>
        /// Navigation property to the owning <see cref="Feed"/>.
        /// </summary>
        [ForeignKey(nameof(FeedId))]
        public virtual Feed Feed { get; set; } = null!;

        /// <summary>
        /// Collection of notification audit records associated with this article.
        /// </summary>
        public virtual ICollection<NotificationLog> NotificationLogs { get; set; }

        /// <summary>
        /// Many-to-many join entities linking this article to user-defined tags.
        /// </summary>
        public virtual ICollection<ArticleTag> ArticleTags { get; set; }

        #endregion

        #region Core Metadata

        /// <summary>
        /// Article headline or title.
        /// </summary>
        [Required]
        [MaxLength(1200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Canonical URL pointing to the original article on the web.
        /// </summary>
        [Required]
        [MaxLength(2048)]
        public string Link { get; set; } = string.Empty;

        /// <summary>
        /// Short summary or description provided by the feed (usually &lt;description&gt; or &lt;summary&gt;).
        /// </summary>
        [MaxLength(4000)]
        public string? Summary { get; set; }

        /// <summary>
        /// Full article body content (HTML or plain text depending on feed and parser settings).
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Author name(s) as provided by the feed (single string representation).
        /// </summary>
        [MaxLength(512)]
        public string? Author { get; set; }

        /// <summary>
        /// Publication date and time according to the feed.
        /// </summary>
        public DateTime? PublishedDate { get; set; }

        /// <summary>
        /// Local timestamp when this article was first inserted into the database.
        /// </summary>
        public DateTime AddedDate { get; set; }

        #endregion

        #region Media & Presentation

        /// <summary>
        /// URL of the primary featured image (og:image, media:thumbnail, enclosure with image type, etc.).
        /// Used for cards, lists and grid views.
        /// </summary>
        [MaxLength(2048)]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Comma-separated list of categories or tags as declared in the feed entry.
        /// </summary>
        [MaxLength(1024)]
        public string? Categories { get; set; }

        /// <summary>
        /// Language code of the article content (ISO 639-1 or BCP 47).
        /// </summary>
        [MaxLength(16)]
        public string? Language { get; set; }

        #endregion

        #region Enclosure (Podcast / Media support - basic)

        /// <summary>
        /// URL of the media enclosure (podcast episode, video, PDF, torrent, etc.).
        /// </summary>
        [MaxLength(2048)]
        public string? EnclosureUrl { get; set; }

        /// <summary>
        /// MIME type of the enclosure (audio/mpeg, video/mp4, application/pdf, etc.).
        /// </summary>
        [MaxLength(128)]
        public string? EnclosureType { get; set; }

        /// <summary>
        /// Size of the enclosure in bytes, if provided by the feed.
        /// </summary>
        public long? EnclosureLength { get; set; }

        #endregion

        #region User Interaction State

        /// <summary>
        /// Current read/unread/star/archive status from the user's perspective.
        /// </summary>
        public ArticleStatus Status { get; set; }

        /// <summary>
        /// Indicates whether the user has starred this article.
        /// </summary>
        public bool IsStarred { get; set; }

        /// <summary>
        /// Indicates whether the user has marked this article as favorite.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Indicates whether a notification has already been generated for this article.
        /// </summary>
        public bool IsNotified { get; set; }

        /// <summary>
        /// Prevents repeated processing by the rule engine during subsequent syncs.
        /// </summary>
        public bool ProcessedByRules { get; set; }

        /// <summary>
        /// Reading progress percentage (0–100). 100 means fully read.
        /// </summary>
        public int ReadPercentage { get; set; }

        /// <summary>
        /// Timestamp of the last time the user opened or interacted with this article.
        /// </summary>
        public DateTime? LastReadAt { get; set; }

        #endregion

        #region Computed / UI Helpers

        /// <summary>
        /// Clean, truncated preview text (max 200 characters) suitable for list and card views.
        /// Prioritizes Summary → cleaned Content → fallback message.
        /// </summary>
        [NotMapped]
        public string DisplayExcerpt
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Summary))
                    return Summary.Length > 200 ? Summary.Substring(0, 197) + "…" : Summary;

                if (!string.IsNullOrWhiteSpace(Content))
                {
                    var plain = Regex.Replace(Content, @"<[^>]+>|&[^;]+;", "").Trim();
                    return plain.Length > 200 ? plain.Substring(0, 197) + "…" : plain;
                }

                return "No content available";
            }
        }

        /// <summary>
        /// In-memory collection of resolved <see cref="Tag"/> entities for UI binding.
        /// Not persisted directly — populated by services when needed.
        /// </summary>
        [NotMapped]
        public List<Tag> Tags { get; set; } = new();

        #endregion

        #region INotifyPropertyChanged (UI binding support)

        /// <summary>
        /// Occurs when a property value changes (used for data binding).
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on FeedId + PublishedDate DESC in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(a => new { a.FeedId, a.PublishedDate }).IsDescending();
// Why (benefit): Dramatically faster "latest articles per feed" and timeline queries
// Estimated effort: 30 min – 1 h
// Risk level: Low
// Potential impact: Very positive on feeds with >1k articles

// TODO (Medium - v1.x): Add full-text search support via EF.Functions.Like or raw SQL
// What to do: Use SQLite FTS5 virtual table with EF Core raw SQL queries
// Why (benefit): Enable fast title + content search without full table scans
// Estimated effort: 2–4 days
// Risk level: Medium
// Potential impact: Core feature for large libraries

// TODO (Medium - v2.0): Normalize authors to many-to-many relationship
// What to do: Create Author entity + ArticleAuthor join table
// Why (benefit): Correct handling of multi-author articles + better faceted search
// Estimated effort: 5–8 days (migration + queries + UI)
// Risk level: High
// Potential impact: Architectural improvement

// TODO (Low - v1.x): Add IsArchived flag + ArchivedAt timestamp
// What to do: Add bool IsArchived; DateTime? ArchivedAt;
// Why (benefit): Allow soft-delete/archive without physical deletion
// Estimated effort: 4–6 hours
// Risk level: Low
// Potential impact: Better data retention control

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Detect conflicting updates between background sync and user actions
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher data consistency in concurrent scenarios