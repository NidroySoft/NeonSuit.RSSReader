using NeonSuit.RSSReader.Core.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a subscribed RSS/Atom/JSON Feed source.
    /// Stores metadata, synchronization state, health indicators, caching info and retention rules.
    /// Optimized for efficient polling, conditional GET and low-bandwidth sync on constrained devices.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This entity is central to feed management and includes:
    /// <list type="bullet">
    /// <item><description>Core feed metadata (URL, title, description, website)</description></item>
    /// <item><description>Synchronization state (last update, next schedule, active status)</description></item>
    /// <item><description>Health tracking (failure count, last error, ETag for conditional GET)</description></item>
    /// <item><description>Content management (retention days, article counts)</description></item>
    /// <item><description>Categorization and organization</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Performance optimizations for low-resource environments:
    /// <list type="bullet">
    /// <item><description>Indexed fields: CategoryId, LastUpdated, NextUpdateSchedule</description></item>
    /// <item><description>Cached article counts to avoid repeated aggregation queries</description></item>
    /// <item><description>ETag and LastModified support for bandwidth-efficient updates</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [Table("Feeds")]
    public class Feed
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Feed"/> class.
        /// Sets default values for timestamps, frequencies, and state properties.
        /// </summary>
        public Feed()
        {
            CreatedAt = DateTime.UtcNow;
            UpdateFrequency = FeedUpdateFrequency.EveryHour;
            ArticleRetentionDays = 30;
            FailureCount = 0;
            TotalArticleCount = 0;
            IsActive = true;
            IsUpdating = false;
            LastUpdated = DateTime.MinValue;
            NextUpdateSchedule = DateTime.UtcNow;
            IsPodcastFeed = false;
            Articles = new HashSet<Article>();
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Core Metadata

        /// <summary>
        /// Canonical URL of the feed (unique across the database).
        /// </summary>
        [Required]
        [MaxLength(2048)]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable title of the feed.
        /// </summary>
        [Required]
        [MaxLength(512)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Feed description or subtitle.
        /// </summary>
        [MaxLength(2048)]
        public string? Description { get; set; }

        /// <summary>
        /// Website/homepage URL associated with the feed.
        /// </summary>
        [Required]
        [MaxLength(2048)]
        public string WebsiteUrl { get; set; } = string.Empty;

        /// <summary>
        /// URL of the feed favicon or logo (preferably 64×64 or larger).
        /// </summary>
        [MaxLength(2048)]
        public string? IconUrl { get; set; }

        /// <summary>
        /// Detected or declared language code (ISO 639-1 or BCP 47).
        /// </summary>
        [MaxLength(16)]
        public string? Language { get; set; }

        #endregion

        #region Categorization & Organization

        /// <summary>
        /// Optional foreign key to the owning Category.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Navigation property to the parent category (optional).
        /// </summary>
        [ForeignKey(nameof(CategoryId))]
        public virtual Category? Category { get; set; }

        #endregion

        #region Relationships

        /// <summary>
        /// Collection of all articles belonging to this feed.
        /// </summary>
        public virtual ICollection<Article> Articles { get; set; }

        #endregion

        #region Synchronization & Scheduling

        /// <summary>
        /// Desired refresh interval for automatic background updates.
        /// </summary>
        public FeedUpdateFrequency UpdateFrequency { get; set; }

        /// <summary>
        /// Timestamp of the last successful fetch and parse.
        /// </summary>
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// Next planned synchronization attempt (used by scheduler).
        /// </summary>
        public DateTime? NextUpdateSchedule { get; set; }

        /// <summary>
        /// Timestamp when the feed was first subscribed/added.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Whether automatic background updates are currently enabled.
        /// </summary>
        public bool IsActive { get; set; }

        #endregion

        #region Health & Error Tracking

        /// <summary>
        /// Number of consecutive failed update attempts.
        /// Used for exponential backoff and health visualization.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Last error message or exception summary from the most recent failed attempt.
        /// </summary>
        [MaxLength(2048)]
        public string? LastError { get; set; }

        /// <summary>
        /// ETag header value from the last successful HTTP response.
        /// Enables conditional GET (If-None-Match) to save bandwidth.
        /// </summary>
        [MaxLength(512)]
        public string? ETag { get; set; }

        /// <summary>
        /// Last-Modified header value from the last successful response.
        /// Enables conditional GET (If-Modified-Since).
        /// </summary>
        public DateTime? LastModifiedHeader { get; set; }

        #endregion

        #region Article & Content Management

        /// <summary>
        /// Feed-specific override for article retention period in days.
        /// Null = use global default, 0 = keep forever.
        /// </summary>
        public int? ArticleRetentionDays { get; set; }

        /// <summary>
        /// Timestamp of the last full (non-conditional) synchronization.
        /// </summary>
        public DateTime? LastFullSync { get; set; }

        /// <summary>
        /// Cached total number of articles stored for this feed.
        /// Updated after each successful sync or cleanup.
        /// </summary>
        public int TotalArticleCount { get; set; }

        #endregion

        #region Media / Podcast Detection

        /// <summary>
        /// Indicates whether this feed primarily contains podcast/audio episodes.
        /// Set by parser or user override.
        /// </summary>
        public bool IsPodcastFeed { get; set; }

        #endregion

        #region Transient / Runtime State (Not Mapped)

        /// <summary>
        /// Transient flag indicating an active sync operation is currently running.
        /// Not persisted in database.
        /// </summary>
        [NotMapped]
        public bool IsUpdating { get; set; }

        /// <summary>
        /// Cached number of unread articles (updated by business logic).
        /// Not persisted in database.
        /// </summary>
        [NotMapped]
        public int UnreadCount { get; set; }

        /// <summary>
        /// Computed health status based on recent failures.
        /// Not persisted in database.
        /// </summary>
        [NotMapped]
        public FeedHealthStatus HealthStatus
        {
            get
            {
                if (FailureCount == 0) return FeedHealthStatus.Healthy;
                if (FailureCount <= 3) return FeedHealthStatus.Warning;
                if (FailureCount <= 10) return FeedHealthStatus.Error;
                return FeedHealthStatus.Invalid;
            }
        }

        /// <summary>
        /// Effective retention period in days (feed-specific override or global default).
        /// Not persisted in database.
        /// </summary>
        [NotMapped]
        public int EffectiveRetentionDays => ArticleRetentionDays ?? 30;

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on (IsActive, NextUpdateSchedule) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(f => new { f.IsActive, f.NextUpdateSchedule });
// Why (benefit): Accelerate background scheduler queue (active feeds sorted by next update time)
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Significantly faster sync queue processing

// TODO (High - v1.x): Add composite index on (CategoryId, Title) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(f => new { f.CategoryId, f.Title });
// Why (benefit): Faster alphabetical sorting within categories for UI display
// Estimated effort: 20–30 min
// Risk level: Low
// Potential impact: Better UX in sidebar/category views

// TODO (High - v1.x): Add unique index on Url in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(f => f.Url).IsUnique();
// Why (benefit): Enforce URL uniqueness at database level (replaces SQLite-net [Unique] attribute)
// Estimated effort: 15 min
// Risk level: Low
// Potential impact: Prevents duplicate feed subscriptions

// TODO (Medium - v1.x): Add LastSyncDurationMs for performance monitoring
// What to do: Add public long? LastSyncDurationMs { get; set; }
// Why (benefit): Detect slow feeds, show diagnostics in UI, identify performance issues
// Estimated effort: 2–3 hours
// Risk level: Low
// Potential impact: Helps identify problematic feeds and optimize sync

// TODO (Medium - v2.0): Add soft-delete/archive flag
// What to do: Add bool IsArchived; DateTime? ArchivedAt;
// Why (benefit): Hide broken or unused feeds without permanent deletion
// Estimated effort: 4–6 hours
// Risk level: Low
// Potential impact: Improves troubleshooting UX and data recovery options

// TODO (Low - v1.x): Add generator metadata for debugging
// What to do: Add string? Generator; string? GeneratorVersion;
// Why (benefit): Useful for debugging feed compatibility issues with specific generators
// Estimated effort: 1 hour
// Risk level: Low
// Potential impact: Minor diagnostic improvement for support scenarios

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent lost updates if feed metadata edited while syncing concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher data consistency in concurrent scenarios