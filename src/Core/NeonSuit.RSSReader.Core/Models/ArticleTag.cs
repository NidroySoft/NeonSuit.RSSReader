using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Join entity for the many-to-many relationship between <see cref="Article"/> and <see cref="Tag"/>.
    /// Stores metadata about when and how each tag was applied (user, rule, system).
    /// Optimized for fast lookups by article, tag, rule or application source.
    /// </summary>
    [Table("ArticleTags")]
    public class ArticleTag
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArticleTag"/> class.
        /// Sets default values for application metadata.
        /// </summary>
        public ArticleTag()
        {
            AppliedAt = DateTime.UtcNow;
            AppliedBy = "user"; // Default: manual user action
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Foreign Keys

        /// <summary>
        /// Foreign key to the associated <see cref="Article"/>.
        /// </summary>
        [Required]
        public int ArticleId { get; set; }

        /// <summary>
        /// Foreign key to the applied <see cref="Tag"/>.
        /// </summary>
        [Required]
        public int TagId { get; set; }

        #endregion

        #region Application Metadata

        /// <summary>
        /// Source that applied this tag ("user", "rule", "system", "import", etc.).
        /// Default: "user".
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string AppliedBy { get; set; }

        /// <summary>
        /// Optional foreign key to the <see cref="Rule"/> that automatically applied this tag.
        /// Null if applied manually or by system logic.
        /// </summary>
        public int? RuleId { get; set; }

        /// <summary>
        /// Timestamp when this tag was applied to the article.
        /// </summary>
        public DateTime AppliedAt { get; set; }

        /// <summary>
        /// Optional confidence score (0.0–1.0) when the tag was applied by an automated rule or ML.
        /// Null for manual/user-applied tags.
        /// </summary>
        public double? Confidence { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Navigation property to the associated <see cref="Article"/>.
        /// </summary>
        [ForeignKey(nameof(ArticleId))]
        public virtual Article? Article { get; set; }

        /// <summary>
        /// Navigation property to the applied <see cref="Tag"/>.
        /// </summary>
        [ForeignKey(nameof(TagId))]
        public virtual Tag? Tag { get; set; }

        /// <summary>
        /// Navigation property to the <see cref="Rule"/> that applied this tag (if applicable).
        /// </summary>
        [ForeignKey(nameof(RuleId))]
        public virtual Rule? Rule { get; set; }

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite unique index on (ArticleId, TagId) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(at => new { at.ArticleId, at.TagId }).IsUnique();
// Why (benefit): Enforce uniqueness (prevent duplicate tags on same article) + fast duplicate checks
// Estimated effort: 30 min (fluent API + migration)
// Risk level: Low – additive unique index, backward compatible
// Potential impact: Prevents data corruption from duplicate tags

// TODO (High - v1.x): Add composite index on (TagId, AppliedAt DESC) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(at => new { at.TagId, at.AppliedAt }).IsDescending();
// Why (benefit): Faster "recent articles with this tag" queries (popular for tag timelines)
// Estimated effort: 20–30 min
// Risk level: Low
// Potential impact: Noticeable performance gain on heavily tagged libraries

// TODO (High - v1.x): Add index on RuleId for rule-based queries
// What to do: In OnModelCreating, add: entity.HasIndex(at => at.RuleId);
// Why (benefit): Faster lookups of tags applied by specific rules (audit, cleanup)
// Estimated effort: 15 min
// Risk level: Low
// Potential impact: Improves rule management UI performance

// TODO (Medium - v1.x): Add soft-delete / removal tracking
// What to do: Add bool IsRemoved; DateTime? RemovedAt; string? RemovedBy;
// Why (benefit): Track when/why a tag was removed (audit trail, undo support)
// Estimated effort: 6–8 hours (model + UI + cleanup logic)
// Risk level: Medium – new fields + possible migration logic
// Potential impact: Stronger audit and recovery capabilities

// TODO (Medium - v2.0): Add expiration / temporary tags support
// What to do: Add DateTime? ExpiresAt;
// Why (benefit): Support time-bound tags (e.g., "read later this week")
// Estimated effort: 1–2 days (model + scheduled cleanup job)
// Risk level: Medium – adds scheduled task dependency
// Potential impact: Powerful feature for task-oriented tagging

// TODO (Low - v1.x): Consider rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent race conditions if tag assignment/removal happens concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in multi-threaded sync scenarios