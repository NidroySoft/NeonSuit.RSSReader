using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeonSuit.RSSReader.Core.Models
{
    /// <summary>
    /// Represents a category (folder/group) for organizing subscribed feeds.
    /// Supports hierarchical nesting via parent-child relationships and custom sorting.
    /// Optimized for fast tree traversal, sorting and filtering in UI components.
    /// </summary>
    [Table("Categories")]
    public class Category
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Category"/> class.
        /// Sets default values for timestamps and UI state.
        /// </summary>
        public Category()
        {
            CreatedAt = DateTime.UtcNow;
            SortOrder = 0;
            IsExpanded = true;
            Feeds = new HashSet<Feed>();
            Subcategories = new HashSet<Category>();
        }

        #region Primary Key

        /// <summary>
        /// Internal auto-increment primary key.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        #endregion

        #region Hierarchy & Identity

        /// <summary>
        /// Foreign key to the parent category (null for root categories).
        /// </summary>
        public int? ParentCategoryId { get; set; }

        /// <summary>
        /// Navigation property to the parent category (null for root-level categories).
        /// </summary>
        [ForeignKey(nameof(ParentCategoryId))]
        public virtual Category? ParentCategory { get; set; }

        /// <summary>
        /// Collection of child categories (subfolders).
        /// </summary>
        [InverseProperty(nameof(ParentCategory))]
        public virtual ICollection<Category> Subcategories { get; set; }

        /// <summary>
        /// Display name of the category.
        /// Unique within the same parent to prevent naming conflicts.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description or notes about the category purpose.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        #endregion

        #region Ordering & UI State

        /// <summary>
        /// Custom sort order within the parent category (ascending).
        /// Lower values appear first in lists and trees.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Indicates whether this category is expanded in the UI tree view.
        /// Transient state — not persisted in database.
        /// </summary>
        [NotMapped]
        public bool IsExpanded { get; set; }

        /// <summary>
        /// Optional accent color for visual differentiation in UI (hex format, e.g., "#FF5733").
        /// </summary>
        [MaxLength(7)] // Hex color format: #RRGGBB (7 characters including #)
        public string? Color { get; set; }

        #endregion

        #region Audit & Timestamps

        /// <summary>
        /// Timestamp when the category was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp of the last modification (name, description, sort order, etc.).
        /// </summary>
        public DateTime? LastModified { get; set; }

        #endregion

        #region Relationships

        /// <summary>
        /// Collection of feeds directly assigned to this category.
        /// </summary>
        public virtual ICollection<Feed> Feeds { get; set; }

        #endregion

        #region Computed / Transient Properties

        /// <summary>
        /// Full hierarchical path from root to this category (e.g., "News/Sports/Local").
        /// Computed on demand — useful for display and breadcrumbs.
        /// </summary>
        [NotMapped]
        public string FullPath
        {
            get
            {
                var pathParts = new List<string> { Name };
                var current = ParentCategory;
                while (current != null)
                {
                    pathParts.Add(current.Name);
                    current = current.ParentCategory;
                }
                pathParts.Reverse();
                return string.Join(" / ", pathParts);
            }
        }

        /// <summary>
        /// Depth level in the category tree (0 = root, 1 = first child, etc.).
        /// Computed on demand — useful for indentation in tree views.
        /// </summary>
        [NotMapped]
        public int Depth
        {
            get
            {
                int depth = 0;
                var current = ParentCategory;
                while (current != null)
                {
                    depth++;
                    current = current.ParentCategory;
                }
                return depth;
            }
        }

        /// <summary>
        /// Indicates whether this is a root-level category (no parent).
        /// </summary>
        [NotMapped]
        public bool IsRoot => ParentCategoryId == null;

        #endregion
    }
}

// ──────────────────────────────────────────────────────────────
//                         FUTURE IMPROVEMENTS
// ──────────────────────────────────────────────────────────────

// TODO (High - v1.x): Add composite unique index on (ParentCategoryId, Name) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(c => new { c.ParentCategoryId, c.Name }).IsUnique();
// Why: Enforce unique names within the same parent (prevents duplicates in UI tree)
// Estimated effort: 30 min
// Risk level: Low
// Potential impact: Prevents user confusion and data inconsistency

// TODO (High - v1.x): Add composite index on (ParentCategoryId, SortOrder) in DbContext
// What to do: In OnModelCreating, add: entity.HasIndex(c => new { c.ParentCategoryId, c.SortOrder });
// Why: Faster sorting of subcategories within each parent
// Estimated effort: 20–30 min
// Risk level: Low
// Potential impact: Smoother tree view rendering and drag-drop reordering

// TODO (Medium - v1.x): Add soft-delete/archive support
// What to do: Add bool IsArchived; DateTime? ArchivedAt;
// Why: Hide unused categories without permanent deletion
// Estimated effort: 4–6 hours
// Risk level: Low
// Potential impact: Better organization UX

// TODO (Medium - v2.0): Add category-level custom icon or color
// What to do: Add string? CustomIconUrl; string? AccentColor;
// Why: Visual distinction in sidebar/tree views
// Estimated effort: 6–8 hours (model + UI)
// Risk level: Low
// Potential impact: Improved visual hierarchy

// TODO (Low - v1.x): Consider rowversion concurrency token
// What to do: Add public byte[] RowVersion { get; set; } + .IsRowVersion() in OnModelCreating
// Why: Prevent lost updates if category edited concurrently
// Estimated effort: 1 day
// Risk level: Medium
// Potential impact: Higher consistency in multi-threaded scenarios