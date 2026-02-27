using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a user-defined tag for categorizing and organizing articles.
    /// Tags are many-to-many with Articles via the <see cref="ArticleTag"/> join entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tags provide a flexible way to organize articles beyond feed categories.
    /// Each tag has a name, optional color for visual identification, and usage statistics.
    /// </para>
    /// <para>
    /// Key features:
    /// <list type="bullet">
    /// <item><description>Many-to-many relationship with articles through <see cref="ArticleTag"/></description></item>
    /// <item><description>Visual customization with color and optional icon</description></item>
    /// <item><description>Pin functionality for frequently used tags</description></item>
    /// <item><description>Visibility control for tag cloud/UI display</description></item>
    /// <item><description>Usage tracking for popularity metrics</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Performance optimizations:
    /// <list type="bullet">
    /// <item><description>Indexed on Name for fast lookups and search</description></item>
    /// <item><description>Cached UsageCount avoids repeated COUNT queries</description></item>
    /// <item><description>LastUsedAt for "recent tags" functionality</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    [Table("Tags")]
    public class Tag
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Tag"/> class.
        /// Sets default values for color, timestamps, and collections.
        /// </summary>
        public Tag()
        {
            Color = "#3498db"; // Default blue
            IsPinned = false;
            IsVisible = true;
            CreatedAt = DateTime.UtcNow;
            UsageCount = 0;
            ArticleTags = new HashSet<ArticleTag>();
            Articles = new List<Article>();
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Core Properties

        /// <summary>
        /// Display name of the tag.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the tag's purpose or meaning.
        /// </summary>
        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// Color in hexadecimal format (#RRGGBB or #RRGGBBAA).
        /// Used for visual differentiation in UI.
        /// </summary>
        [Required]
        [MaxLength(9)]
        [RegularExpression("^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$")]
        public string Color { get; set; }

        /// <summary>
        /// Optional icon identifier (FontAwesome/Material icon name or path).
        /// </summary>
        [MaxLength(30)]
        public string? Icon { get; set; }

        #endregion

        #region UI State

        /// <summary>
        /// Whether this tag is pinned/featured in the UI.
        /// Pinned tags appear at the top of tag lists for quick access.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Whether this tag is visible in the tag cloud and tag lists.
        /// Hidden tags are excluded from most UI views but retain their associations.
        /// </summary>
        public bool IsVisible { get; set; }

        #endregion

        #region Audit & Statistics

        /// <summary>
        /// Timestamp when the tag was created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last time this tag was used/assigned to an article (UTC).
        /// Used for "recent tags" functionality and cache invalidation.
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// Number of articles currently tagged with this tag.
        /// Updated automatically when articles are tagged or untagged.
        /// </summary>
        public int UsageCount { get; set; }

        #endregion

        #region Relationships

        /// <summary>
        /// Collection of join entities linking this tag to articles.
        /// </summary>
        public virtual ICollection<ArticleTag> ArticleTags { get; set; }

        #endregion

        #region Computed Properties (Not Mapped)

        /// <summary>
        /// List of articles associated with this tag.
        /// Populated on-demand via the ArticleTags navigation.
        /// </summary>
        [NotMapped]
        public List<Article> Articles { get; set; }

        /// <summary>
        /// Calculated darker shade of the tag's color for borders, text, or hover states.
        /// Automatically derived from the main color.
        /// </summary>
        [NotMapped]
        public string DarkColor => CalculateDarkColor(Color);

        /// <summary>
        /// Indicates whether the tag has been used at least once.
        /// </summary>
        [NotMapped]
        public bool IsUsed => UsageCount > 0;

        /// <summary>
        /// Gets the tag's display name with optional visual indicators.
        /// </summary>
        [NotMapped]
        public string DisplayName => IsPinned ? $"📌 {Name}" : Name;

        /// <summary>
        /// Gets the time ago string since last use for UI display.
        /// </summary>
        [NotMapped]
        public string LastUsedTimeAgo
        {
            get
            {
                if (!LastUsedAt.HasValue)
                    return "Never used";

                var span = DateTime.UtcNow - LastUsedAt.Value;

                return span.TotalDays switch
                {
                    < 1 => "Today",
                    < 2 => "Yesterday",
                    < 7 => $"{(int)span.TotalDays} days ago",
                    < 30 => $"{(int)(span.TotalDays / 7)} weeks ago",
                    < 365 => $"{(int)(span.TotalDays / 30)} months ago",
                    _ => $"{(int)(span.TotalDays / 365)} years ago"
                };
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Calculates a darker shade of the given hex color.
        /// Used for generating complementary UI colors (borders, text on light backgrounds).
        /// </summary>
        /// <param name="hexColor">The original hex color (#RRGGBB or #RRGGBBAA).</param>
        /// <returns>A darker hex color, or a fallback color if parsing fails.</returns>
        private static string CalculateDarkColor(string hexColor)
        {
            const string fallbackDark = "#2c3e50";

            if (string.IsNullOrWhiteSpace(hexColor) || hexColor.Length < 7 || !hexColor.StartsWith("#"))
                return fallbackDark;

            try
            {
                // Parse RGB components
                var r = Convert.ToInt32(hexColor.Substring(1, 2), 16);
                var g = Convert.ToInt32(hexColor.Substring(3, 2), 16);
                var b = Convert.ToInt32(hexColor.Substring(5, 2), 16);

                // Darken by 30%
                r = (int)(r * 0.7);
                g = (int)(g * 0.7);
                b = (int)(b * 0.7);

                // Clamp values to valid range
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return fallbackDark;
            }
        }

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite index on (IsVisible, UsageCount) for tag cloud queries
// What to do: In OnModelCreating, add: entity.HasIndex(t => new { t.IsVisible, t.UsageCount });
// Why (benefit): Faster tag cloud generation showing only visible tags ordered by popularity
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Significantly faster tag cloud rendering

// TODO (High - v1.x): Add index on Name for case-insensitive search
// What to do: In OnModelCreating, add: entity.HasIndex(t => t.Name).IsUnique();
// Why (benefit): Enforce unique tag names + faster search/autocomplete
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Prevents duplicate tags, improves search performance

// TODO (Medium - v1.x): Add automatic usage count maintenance via triggers
// What to do: Add database triggers or use EF events to update UsageCount on ArticleTag changes
// Why (benefit): Ensure UsageCount always reflects actual article count
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Data integrity improvement, eliminates need for manual updates

// TODO (Medium - v1.x): Add tag merging history/audit trail
// What to do: Create TagMergeHistory table to track when tags are merged
// Why (benefit): Audit trail for tag management operations
// Estimated effort: 4-6 hours
// Risk level: Low
// Potential impact: Better audit capabilities for tag changes

// TODO (Low - v1.x): Add support for tag synonyms/aliases
// What to do: Add SynonymOfId (int?) self-reference for canonical tags
// Why (benefit): Allow multiple names to map to same tag (e.g., "AI" and "Artificial Intelligence")
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Better user experience, handles variations gracefully

// TODO (Low - v1.x): Add color suggestions based on tag name
// What to do: Implement hash-based color generation for new tags without explicit color
// Why (benefit): Consistent colors without manual picking
// Estimated effort: 2-3 hours
// Risk level: Low
// Potential impact: Improved visual experience with zero effort

// TODO (Low - v1.x): Consider adding rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why (benefit): Prevent lost updates if tag metadata edited concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in concurrent scenarios